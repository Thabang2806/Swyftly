using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Delivery;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminPickupPointEndpoints
{
    public static IEndpointRouteBuilder MapAdminPickupPointEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/pickup-points")
            .WithTags("Admin Pickup Points")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("", ListAsync)
            .WithName("ListAdminPickupPoints")
            .WithSummary("Lists platform-managed pickup points.")
            .Produces<IReadOnlyCollection<AdminPickupPointResponse>>(StatusCodes.Status200OK);

        group.MapPost("", CreateAsync)
            .WithName("CreateAdminPickupPoint")
            .WithSummary("Creates a platform-managed pickup point.")
            .Produces<AdminPickupPointResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{pickupPointId:guid}", UpdateAsync)
            .WithName("UpdateAdminPickupPoint")
            .WithSummary("Updates a platform-managed pickup point.")
            .Produces<AdminPickupPointResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{pickupPointId:guid}/activate", ActivateAsync)
            .WithName("ActivateAdminPickupPoint")
            .WithSummary("Activates a pickup point.")
            .Produces<AdminPickupPointResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{pickupPointId:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivateAdminPickupPoint")
            .WithSummary("Deactivates a pickup point without deleting it.")
            .Produces<AdminPickupPointResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var points = await dbContext.PickupPoints
            .AsNoTracking()
            .OrderByDescending(point => point.IsActive)
            .ThenBy(point => point.CountryCode)
            .ThenBy(point => point.Province)
            .ThenBy(point => point.Name)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(points.Select(Map).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        AdminPickupPointRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        PickupPoint point;
        try
        {
            point = ToEntity(request);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation(ToCamelCase((exception as ArgumentException)?.ParamName ?? "pickupPoint"), exception.Message);
        }

        if (await CodeExistsAsync(point.ProviderName, point.Code, null, dbContext, cancellationToken))
        {
            return Conflict("AdminPickupPoints.DuplicateCode", "A pickup point with this provider and code already exists.");
        }

        dbContext.PickupPoints.Add(point);
        await RecordAuditAsync(principal, auditLogService, "PickupPointCreated", point, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Created($"/api/admin/pickup-points/{point.Id}", Map(point));
    }

    private static async Task<IResult> UpdateAsync(
        Guid pickupPointId,
        AdminPickupPointRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var point = await dbContext.PickupPoints.SingleOrDefaultAsync(item => item.Id == pickupPointId, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        var previousValue = Snapshot(point);
        try
        {
            point.Update(
                request.ProviderName,
                request.Code,
                request.Name,
                request.AddressLine1,
                request.AddressLine2,
                request.Suburb,
                request.City,
                request.Province,
                request.PostalCode,
                request.CountryCode,
                request.Latitude,
                request.Longitude,
                request.OpeningHours,
                request.IsActive);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation(ToCamelCase((exception as ArgumentException)?.ParamName ?? "pickupPoint"), exception.Message);
        }

        if (await CodeExistsAsync(point.ProviderName, point.Code, point.Id, dbContext, cancellationToken))
        {
            return Conflict("AdminPickupPoints.DuplicateCode", "A pickup point with this provider and code already exists.");
        }

        await RecordAuditAsync(principal, auditLogService, "PickupPointUpdated", point, previousValue, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(point));
    }

    private static async Task<IResult> ActivateAsync(
        Guid pickupPointId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var point = await dbContext.PickupPoints.SingleOrDefaultAsync(item => item.Id == pickupPointId, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        var previousValue = Snapshot(point);
        point.Activate();
        await RecordAuditAsync(principal, auditLogService, "PickupPointActivated", point, previousValue, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(point));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid pickupPointId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var point = await dbContext.PickupPoints.SingleOrDefaultAsync(item => item.Id == pickupPointId, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        var previousValue = Snapshot(point);
        point.Deactivate();
        await RecordAuditAsync(principal, auditLogService, "PickupPointDeactivated", point, previousValue, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(point));
    }

    private static PickupPoint ToEntity(AdminPickupPointRequest request) =>
        new(
            request.ProviderName,
            request.Code,
            request.Name,
            request.AddressLine1,
            request.AddressLine2,
            request.Suburb,
            request.City,
            request.Province,
            request.PostalCode,
            request.CountryCode,
            request.Latitude,
            request.Longitude,
            request.OpeningHours,
            request.IsActive);

    private static Task<bool> CodeExistsAsync(
        string providerName,
        string code,
        Guid? excludingId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.PickupPoints.AnyAsync(
            point => point.ProviderName == providerName
                && point.Code == code
                && (!excludingId.HasValue || point.Id != excludingId.Value),
            cancellationToken);

    private static AdminPickupPointResponse Map(PickupPoint point) =>
        new(
            point.Id,
            point.ProviderName,
            point.Code,
            point.Name,
            point.AddressLine1,
            point.AddressLine2,
            point.Suburb,
            point.City,
            point.Province,
            point.PostalCode,
            point.CountryCode,
            point.Latitude,
            point.Longitude,
            point.OpeningHours,
            point.IsActive,
            point.CreatedAtUtc,
            point.UpdatedAtUtc);

    private static PickupPointAuditSnapshot Snapshot(PickupPoint point) =>
        new(
            point.ProviderName,
            point.Code,
            point.Name,
            point.AddressLine1,
            point.AddressLine2,
            point.Suburb,
            point.City,
            point.Province,
            point.PostalCode,
            point.CountryCode,
            point.Latitude,
            point.Longitude,
            point.OpeningHours,
            point.IsActive);

    private static async Task RecordAuditAsync(
        ClaimsPrincipal principal,
        IAuditLogService auditLogService,
        string actionType,
        PickupPoint point,
        PickupPointAuditSnapshot? previousValue,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                principal.IsInRole(MabuntleRoles.SuperAdmin) ? MabuntleRoles.SuperAdmin : MabuntleRoles.Admin,
                actionType,
                "PickupPoint",
                point.Id.ToString(),
                previousValue is null ? null : JsonSerializer.Serialize(previousValue),
                JsonSerializer.Serialize(Snapshot(point)),
                null),
            cancellationToken);
    }

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult Conflict(string title, string detail) =>
        HttpResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);

    private static IResult NotFound() =>
        HttpResults.Problem(
            title: "AdminPickupPoints.NotFound",
            detail: "Pickup point was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private sealed record PickupPointAuditSnapshot(
        string ProviderName,
        string Code,
        string Name,
        string AddressLine1,
        string? AddressLine2,
        string? Suburb,
        string City,
        string Province,
        string PostalCode,
        string CountryCode,
        decimal? Latitude,
        decimal? Longitude,
        string? OpeningHours,
        bool IsActive);
}

public sealed record AdminPickupPointRequest(
    string ProviderName,
    string Code,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    decimal? Latitude,
    decimal? Longitude,
    string? OpeningHours,
    bool IsActive);

public sealed record AdminPickupPointResponse(
    Guid PickupPointId,
    string ProviderName,
    string Code,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    decimal? Latitude,
    decimal? Longitude,
    string? OpeningHours,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
