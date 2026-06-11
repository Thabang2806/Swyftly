using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerDeliveryMethodEndpoints
{
    public static IEndpointRouteBuilder MapSellerDeliveryMethodEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seller/delivery-methods")
            .WithTags("Seller Delivery Methods")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapGet("", ListAsync)
            .WithName("ListSellerDeliveryMethods")
            .WithSummary("Lists seller-managed delivery methods.")
            .Produces<IReadOnlyCollection<SellerDeliveryMethodResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("", CreateAsync)
            .WithName("CreateSellerDeliveryMethod")
            .WithSummary("Creates a seller-managed delivery method.")
            .Produces<SellerDeliveryMethodResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{deliveryMethodId:guid}", UpdateAsync)
            .WithName("UpdateSellerDeliveryMethod")
            .WithSummary("Updates a seller-managed delivery method.")
            .Produces<SellerDeliveryMethodResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{deliveryMethodId:guid}/activate", ActivateAsync)
            .WithName("ActivateSellerDeliveryMethod")
            .WithSummary("Activates a seller-managed delivery method.")
            .Produces<SellerDeliveryMethodResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{deliveryMethodId:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivateSellerDeliveryMethod")
            .WithSummary("Deactivates a seller-managed delivery method.")
            .Produces<SellerDeliveryMethodResponse>(StatusCodes.Status200OK)
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

        var methods = await dbContext.SellerDeliveryMethods
            .AsNoTracking()
            .Where(method => method.SellerId == seller.Id)
            .OrderByDescending(method => method.IsActive)
            .ThenBy(method => method.DisplayOrder)
            .ThenBy(method => method.Name)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(methods.Select(Map).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        SellerDeliveryMethodRequest request,
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

        if (!TryParseMethodType(request.MethodType, out var methodType, out var validation))
        {
            return validation;
        }

        SellerDeliveryMethod method;
        try
        {
            method = new SellerDeliveryMethod(
                seller.Id,
                request.Name,
                request.Description,
                methodType,
                request.CountryCode,
                request.Province,
                request.BasePrice,
                request.FreeShippingThreshold,
                request.EstimatedMinDays,
                request.EstimatedMaxDays,
                request.DisplayOrder,
                request.IsActive);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation(ToCamelCase((exception as ArgumentException)?.ParamName ?? "deliveryMethod"), exception.Message);
        }

        dbContext.SellerDeliveryMethods.Add(method);
        await RecordAuditAsync(
            principal,
            auditLogService,
            "SellerDeliveryMethodCreated",
            method,
            previousValue: null,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(method));
    }

    private static async Task<IResult> UpdateAsync(
        Guid deliveryMethodId,
        SellerDeliveryMethodRequest request,
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

        var method = await dbContext.SellerDeliveryMethods.SingleOrDefaultAsync(
            item => item.Id == deliveryMethodId && item.SellerId == seller.Id,
            cancellationToken);
        if (method is null)
        {
            return DeliveryMethodNotFound();
        }

        if (!TryParseMethodType(request.MethodType, out var methodType, out var validation))
        {
            return validation;
        }

        var previousValue = Snapshot(method);
        try
        {
            method.Update(
                request.Name,
                request.Description,
                methodType,
                request.CountryCode,
                request.Province,
                request.BasePrice,
                request.FreeShippingThreshold,
                request.EstimatedMinDays,
                request.EstimatedMaxDays,
                request.DisplayOrder,
                request.IsActive);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation(ToCamelCase((exception as ArgumentException)?.ParamName ?? "deliveryMethod"), exception.Message);
        }

        await RecordAuditAsync(
            principal,
            auditLogService,
            "SellerDeliveryMethodUpdated",
            method,
            previousValue,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(method));
    }

    private static async Task<IResult> ActivateAsync(
        Guid deliveryMethodId,
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

        var method = await dbContext.SellerDeliveryMethods.SingleOrDefaultAsync(
            item => item.Id == deliveryMethodId && item.SellerId == seller.Id,
            cancellationToken);
        if (method is null)
        {
            return DeliveryMethodNotFound();
        }

        var previousValue = Snapshot(method);
        method.Activate();
        await RecordAuditAsync(
            principal,
            auditLogService,
            "SellerDeliveryMethodActivated",
            method,
            previousValue,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(method));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid deliveryMethodId,
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

        var method = await dbContext.SellerDeliveryMethods.SingleOrDefaultAsync(
            item => item.Id == deliveryMethodId && item.SellerId == seller.Id,
            cancellationToken);
        if (method is null)
        {
            return DeliveryMethodNotFound();
        }

        var previousValue = Snapshot(method);
        method.Deactivate();
        await RecordAuditAsync(
            principal,
            auditLogService,
            "SellerDeliveryMethodDeactivated",
            method,
            previousValue,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(method));
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

    private static bool TryParseMethodType(
        string value,
        out SellerDeliveryMethodType methodType,
        out IResult validation)
    {
        if (Enum.TryParse(value, ignoreCase: true, out methodType) && Enum.IsDefined(methodType))
        {
            validation = HttpResults.NoContent();
            return true;
        }

        validation = Validation("methodType", "Delivery method type must be Standard, Express, LocalCourier, or PickupPoint.");
        return false;
    }

    private static SellerDeliveryMethodResponse Map(SellerDeliveryMethod method) =>
        new(
            method.Id,
            method.SellerId,
            method.Name,
            method.Description,
            method.MethodType.ToString(),
            method.CountryCode,
            method.Province,
            method.BasePrice,
            method.FreeShippingThreshold,
            method.EstimatedMinDays,
            method.EstimatedMaxDays,
            method.DisplayOrder,
            method.IsActive,
            method.CreatedAtUtc,
            method.UpdatedAtUtc);

    private static DeliveryMethodAuditSnapshot Snapshot(SellerDeliveryMethod method) =>
        new(
            method.Name,
            method.Description,
            method.MethodType.ToString(),
            method.CountryCode,
            method.Province,
            method.BasePrice,
            method.FreeShippingThreshold,
            method.EstimatedMinDays,
            method.EstimatedMaxDays,
            method.DisplayOrder,
            method.IsActive);

    private static async Task RecordAuditAsync(
        ClaimsPrincipal principal,
        IAuditLogService auditLogService,
        string actionType,
        SellerDeliveryMethod method,
        DeliveryMethodAuditSnapshot? previousValue,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                "Seller",
                actionType,
                "SellerDeliveryMethod",
                method.Id.ToString(),
                previousValue is null ? null : JsonSerializer.Serialize(previousValue),
                JsonSerializer.Serialize(Snapshot(method)),
                null),
            cancellationToken);
    }

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerDeliveryMethods.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult DeliveryMethodNotFound() =>
        HttpResults.Problem(
            title: "SellerDeliveryMethods.NotFound",
            detail: "Delivery method was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private sealed record DeliveryMethodAuditSnapshot(
        string Name,
        string? Description,
        string MethodType,
        string CountryCode,
        string? Province,
        decimal BasePrice,
        decimal? FreeShippingThreshold,
        int EstimatedMinDays,
        int EstimatedMaxDays,
        int DisplayOrder,
        bool IsActive);
}

public sealed record SellerDeliveryMethodRequest(
    string Name,
    string? Description,
    string MethodType,
    string CountryCode,
    string? Province,
    decimal BasePrice,
    decimal? FreeShippingThreshold,
    int EstimatedMinDays,
    int EstimatedMaxDays,
    int DisplayOrder,
    bool IsActive);

public sealed record SellerDeliveryMethodResponse(
    Guid DeliveryMethodId,
    Guid SellerId,
    string Name,
    string? Description,
    string MethodType,
    string CountryCode,
    string? Province,
    decimal BasePrice,
    decimal? FreeShippingThreshold,
    int EstimatedMinDays,
    int EstimatedMaxDays,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
