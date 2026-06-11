using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Sellers;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using Mabuntle.Infrastructure.Storage;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerVerificationEvidenceEndpoints
{
    public static IEndpointRouteBuilder MapSellerVerificationEvidenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seller/verification-evidence")
            .WithTags("Seller Verification Evidence")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapGet("", ListAsync)
            .WithName("GetSellerVerificationEvidence")
            .WithSummary("Returns active verification evidence uploaded by the authenticated seller.")
            .Produces<IReadOnlyCollection<SellerVerificationEvidenceResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/upload", UploadAsync)
            .WithName("UploadSellerVerificationEvidence")
            .WithSummary("Uploads a private seller verification evidence file.")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery()
            .Produces<SellerVerificationEvidenceResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{evidenceId:guid}", RemoveAsync)
            .WithName("RemoveSellerVerificationEvidence")
            .WithSummary("Removes an active verification evidence file owned by the authenticated seller.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{evidenceId:guid}/download", DownloadAsync)
            .WithName("DownloadSellerVerificationEvidence")
            .WithSummary("Downloads a private verification evidence file owned by the authenticated seller.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var evidence = await dbContext.SellerVerificationEvidence
            .AsNoTracking()
            .Where(item => item.SellerId == seller.Id && item.RemovedAtUtc == null)
            .OrderBy(item => item.EvidenceType)
            .ThenByDescending(item => item.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(evidence.Select(SellerVerificationEvidenceResponseMapper.Map).ToList());
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        [FromForm] string evidenceType,
        [FromForm] string? note,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        ISellerVerificationEvidenceStorage storage,
        IAuditLogService auditLogService,
        IOptions<SellerVerificationEvidenceOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!CanMutateEvidence(seller.VerificationStatus))
        {
            return HttpResults.Problem(
                title: "SellerVerificationEvidence.ReadOnly",
                detail: "Verification evidence can be changed only before approval or after a rejection.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!Enum.TryParse<SellerVerificationEvidenceType>(evidenceType, ignoreCase: true, out var parsedEvidenceType))
        {
            return Validation("evidenceType", "Evidence type is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(note) && note.Trim().Length > SellerVerificationEvidence.NoteMaxLength)
        {
            return Validation("note", $"Note cannot exceed {SellerVerificationEvidence.NoteMaxLength} characters.");
        }

        var activeFileCount = await dbContext.SellerVerificationEvidence
            .CountAsync(item => item.SellerId == seller.Id && item.RemovedAtUtc == null, cancellationToken);
        if (activeFileCount >= options.Value.MaxActiveFilesPerSeller)
        {
            return Validation("evidence", $"A seller can have at most {options.Value.MaxActiveFilesPerSeller} active evidence files.");
        }

        SellerVerificationEvidenceStoredFile storedFile;
        await using var stream = file.OpenReadStream();
        try
        {
            storedFile = await storage.StoreAsync(
                new SellerVerificationEvidenceStorageRequest(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    seller.Id),
                cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Validation("file", exception.Message);
        }

        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            await storage.DeleteAsync(storedFile.StorageKey, cancellationToken);
            return SellerNotFound();
        }

        var evidence = new SellerVerificationEvidence(
            seller.Id,
            parsedEvidenceType,
            storedFile.StorageProvider,
            storedFile.StorageKey,
            storedFile.OriginalFileName,
            storedFile.ContentType,
            storedFile.ByteSize,
            storedFile.Sha256Hash,
            note,
            actorUserId.Value,
            timeProvider.GetUtcNow());

        dbContext.SellerVerificationEvidence.Add(evidence);
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                actorUserId.Value.ToString(),
                MabuntleRoles.Seller,
                "SellerVerificationEvidenceUploaded",
                "SellerVerificationEvidence",
                evidence.Id.ToString(),
                null,
                JsonSerializer.Serialize(Snapshot(evidence)),
                null,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(storedFile.StorageKey, cancellationToken);
            throw;
        }

        return HttpResults.Created(
            $"/api/seller/verification-evidence/{evidence.Id}",
            SellerVerificationEvidenceResponseMapper.Map(evidence));
    }

    private static async Task<IResult> RemoveAsync(
        Guid evidenceId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        ISellerVerificationEvidenceStorage storage,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!CanMutateEvidence(seller.VerificationStatus))
        {
            return HttpResults.Problem(
                title: "SellerVerificationEvidence.ReadOnly",
                detail: "Verification evidence can be changed only before approval or after a rejection.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var evidence = await dbContext.SellerVerificationEvidence
            .SingleOrDefaultAsync(item => item.Id == evidenceId && item.SellerId == seller.Id && item.RemovedAtUtc == null, cancellationToken);
        if (evidence is null)
        {
            return EvidenceNotFound();
        }

        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return SellerNotFound();
        }

        var previousValue = Snapshot(evidence);
        evidence.Remove(actorUserId.Value, timeProvider.GetUtcNow());
        await storage.DeleteAsync(evidence.StorageKey, cancellationToken);
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                actorUserId.Value.ToString(),
                MabuntleRoles.Seller,
                "SellerVerificationEvidenceRemoved",
                "SellerVerificationEvidence",
                evidence.Id.ToString(),
                JsonSerializer.Serialize(previousValue),
                JsonSerializer.Serialize(Snapshot(evidence)),
                null,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.NoContent();
    }

    private static async Task<IResult> DownloadAsync(
        Guid evidenceId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        ISellerVerificationEvidenceStorage storage,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var evidence = await dbContext.SellerVerificationEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == evidenceId && item.SellerId == seller.Id && item.RemovedAtUtc == null, cancellationToken);
        if (evidence is null)
        {
            return EvidenceNotFound();
        }

        var readFile = await storage.OpenReadAsync(
            evidence.StorageKey,
            evidence.ContentType,
            evidence.OriginalFileName,
            cancellationToken);
        if (readFile is null)
        {
            return EvidenceNotFound();
        }

        return HttpResults.File(readFile.Content, readFile.ContentType, readFile.FileName);
    }

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetActorUserId(principal);
        return userId.HasValue
            ? await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.UserId == userId.Value, cancellationToken)
            : null;
    }

    private static Guid? GetActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private static bool CanMutateEvidence(SellerVerificationStatus status) =>
        status is SellerVerificationStatus.PendingVerification or SellerVerificationStatus.UnderReview or SellerVerificationStatus.Rejected;

    private static object Snapshot(SellerVerificationEvidence evidence) =>
        new
        {
            evidence.EvidenceType,
            evidence.OriginalFileName,
            evidence.ContentType,
            evidence.ByteSize,
            evidence.Sha256Hash,
            evidence.Note,
            evidence.UploadedAtUtc,
            evidence.RemovedAtUtc
        };

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerVerificationEvidence.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult EvidenceNotFound() =>
        HttpResults.Problem(
            title: "SellerVerificationEvidence.NotFound",
            detail: "Verification evidence was not found.",
            statusCode: StatusCodes.Status404NotFound);
}
