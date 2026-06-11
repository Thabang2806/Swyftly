using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Sellers;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerStorePolicyEndpoints
{
    public static IEndpointRouteBuilder MapSellerStorePolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seller/store-policy")
            .WithTags("Seller Store Policies")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapGet("", GetAsync)
            .WithName("GetSellerStorePolicy")
            .WithSummary("Returns the authenticated seller's buyer-facing store policy.")
            .Produces<SellerPolicyResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("", UpsertAsync)
            .WithName("UpdateSellerStorePolicy")
            .WithSummary("Creates or updates the authenticated seller's buyer-facing store policy.")
            .Produces<SellerPolicyResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var policy = await dbContext.SellerStorePolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SellerId == seller.Id, cancellationToken);

        return HttpResults.Ok(SellerPolicyResponseMapper.Map(policy));
    }

    private static async Task<IResult> UpsertAsync(
        SellerStorePolicyRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var policy = await dbContext.SellerStorePolicies
            .SingleOrDefaultAsync(item => item.SellerId == seller.Id, cancellationToken);
        var previousValue = policy is null ? null : Snapshot(policy);

        try
        {
            if (policy is null)
            {
                policy = new SellerStorePolicy(
                    seller.Id,
                    request.ReturnWindowDays,
                    request.ReturnPolicy,
                    request.ExchangePolicy,
                    request.FulfilmentPolicy,
                    request.SupportPolicy,
                    request.CareInstructions,
                    request.ProductDisclaimer);
                dbContext.SellerStorePolicies.Add(policy);
            }
            else
            {
                policy.Update(
                    request.ReturnWindowDays,
                    request.ReturnPolicy,
                    request.ExchangePolicy,
                    request.FulfilmentPolicy,
                    request.SupportPolicy,
                    request.CareInstructions,
                    request.ProductDisclaimer);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation(ToCamelCase((exception as ArgumentException)?.ParamName ?? "storePolicy"), exception.Message);
        }

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                MabuntleRoles.Seller,
                previousValue is null ? "SellerStorePolicyCreated" : "SellerStorePolicyUpdated",
                "SellerStorePolicy",
                policy.Id.ToString(),
                previousValue is null ? null : JsonSerializer.Serialize(previousValue),
                JsonSerializer.Serialize(Snapshot(policy))),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(SellerPolicyResponseMapper.Map(policy));
    }

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken)
            : null;
    }

    private static SellerStorePolicySnapshot Snapshot(SellerStorePolicy policy) =>
        new(
            policy.ReturnWindowDays,
            policy.ReturnPolicy,
            policy.ExchangePolicy,
            policy.FulfilmentPolicy,
            policy.SupportPolicy,
            policy.CareInstructions,
            policy.ProductDisclaimer);

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerStorePolicy.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private sealed record SellerStorePolicySnapshot(
        int? ReturnWindowDays,
        string? ReturnPolicy,
        string? ExchangePolicy,
        string? FulfilmentPolicy,
        string? SupportPolicy,
        string? CareInstructions,
        string? ProductDisclaimer);
}

public sealed record SellerStorePolicyRequest(
    int? ReturnWindowDays,
    string? ReturnPolicy,
    string? ExchangePolicy,
    string? FulfilmentPolicy,
    string? SupportPolicy,
    string? CareInstructions,
    string? ProductDisclaimer);
