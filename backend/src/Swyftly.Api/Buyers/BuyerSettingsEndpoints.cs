using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Identity;
using Swyftly.Domain.Buyers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Buyers;

public static class BuyerSettingsEndpoints
{
    public static IEndpointRouteBuilder MapBuyerSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/buyer")
            .WithTags("Buyer Settings")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        group.MapGet("/profile", GetProfileAsync)
            .WithName("GetBuyerProfile")
            .WithSummary("Returns the authenticated buyer profile settings.")
            .Produces<BuyerProfileSettingsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/profile", UpdateProfileAsync)
            .WithName("UpdateBuyerProfile")
            .WithSummary("Updates the authenticated buyer profile settings.")
            .Produces<BuyerProfileSettingsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/notification-preferences", GetNotificationPreferencesAsync)
            .WithName("GetBuyerNotificationPreferences")
            .WithSummary("Returns category-level in-app and email notification preferences.")
            .Produces<BuyerNotificationPreferencesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/notification-preferences", UpdateNotificationPreferencesAsync)
            .WithName("UpdateBuyerNotificationPreferences")
            .WithSummary("Updates category-level in-app and email notification preferences.")
            .Produces<BuyerNotificationPreferencesResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/delivery-addresses", GetDeliveryAddressesAsync)
            .WithName("GetBuyerDeliveryAddresses")
            .WithSummary("Returns saved delivery addresses for the authenticated buyer.")
            .Produces<IReadOnlyCollection<BuyerDeliveryAddressResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/delivery-addresses", CreateDeliveryAddressAsync)
            .WithName("CreateBuyerDeliveryAddress")
            .WithSummary("Creates a saved delivery address for the authenticated buyer.")
            .Produces<BuyerDeliveryAddressResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/delivery-addresses/{addressId:guid}", UpdateDeliveryAddressAsync)
            .WithName("UpdateBuyerDeliveryAddress")
            .WithSummary("Updates one saved delivery address for the authenticated buyer.")
            .Produces<BuyerDeliveryAddressResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/delivery-addresses/{addressId:guid}", DeleteDeliveryAddressAsync)
            .WithName("DeleteBuyerDeliveryAddress")
            .WithSummary("Deletes one saved delivery address for the authenticated buyer.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/delivery-addresses/{addressId:guid}/make-default", MakeDefaultDeliveryAddressAsync)
            .WithName("MakeBuyerDeliveryAddressDefault")
            .WithSummary("Marks one saved delivery address as the default.")
            .Produces<IReadOnlyCollection<BuyerDeliveryAddressResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        return buyer is null
            ? BuyerNotFound()
            : HttpResults.Ok(await MapProfileAsync(buyer, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateProfileAsync(
        BuyerProfileSettingsRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        try
        {
            buyer.UpdateSettings(request.DisplayName, request.PhoneNumber);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.ParamName ?? "profile", exception.Message);
        }

        return HttpResults.Ok(await MapProfileAsync(buyer, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetNotificationPreferencesAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        return buyer is null
            ? BuyerNotFound()
            : HttpResults.Ok(await MapNotificationPreferencesAsync(buyer.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateNotificationPreferencesAsync(
        BuyerNotificationPreferencesRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (request.Preferences is null)
        {
            return Validation("preferences", "Preferences are required.");
        }

        var errors = ValidatePreferences(request.Preferences);
        if (errors.Count > 0)
        {
            return HttpResults.ValidationProblem(errors);
        }

        var existing = await dbContext.BuyerNotificationPreferences
            .Where(preference => preference.BuyerId == buyer.Id)
            .ToListAsync(cancellationToken);

        foreach (var preferenceRequest in request.Preferences)
        {
            var category = preferenceRequest.Category.Trim();
            var preference = existing.SingleOrDefault(item => item.Category == category);
            if (preference is null)
            {
                dbContext.BuyerNotificationPreferences.Add(new BuyerNotificationPreference(
                    buyer.Id,
                    category,
                    preferenceRequest.IsEnabled,
                    preferenceRequest.EmailEnabled ?? true));
            }
            else
            {
                preference.SetChannels(
                    preferenceRequest.IsEnabled,
                    preferenceRequest.EmailEnabled ?? preference.EmailEnabled);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await MapNotificationPreferencesAsync(buyer.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetDeliveryAddressesAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        return buyer is null
            ? BuyerNotFound()
            : HttpResults.Ok(await MapDeliveryAddressesAsync(buyer.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> CreateDeliveryAddressAsync(
        BuyerDeliveryAddressRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var existing = await dbContext.BuyerDeliveryAddresses
            .Where(address => address.BuyerId == buyer.Id)
            .ToListAsync(cancellationToken);
        if (existing.Count >= BuyerDeliveryAddress.MaxAddressesPerBuyer)
        {
            return Validation("deliveryAddresses", $"A buyer can save at most {BuyerDeliveryAddress.MaxAddressesPerBuyer} delivery addresses.");
        }

        BuyerDeliveryAddress address;
        try
        {
            address = ToEntity(buyer.Id, request, request.IsDefault || existing.Count == 0);
        }
        catch (ArgumentException exception)
        {
            return Validation(ToCamelCase(exception.ParamName ?? "deliveryAddress"), exception.Message);
        }

        if (address.IsDefault)
        {
            foreach (var existingAddress in existing)
            {
                existingAddress.SetDefault(false);
            }
        }

        dbContext.BuyerDeliveryAddresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Created($"/api/buyer/delivery-addresses/{address.Id}", MapDeliveryAddress(address));
    }

    private static async Task<IResult> UpdateDeliveryAddressAsync(
        Guid addressId,
        BuyerDeliveryAddressRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var addresses = await dbContext.BuyerDeliveryAddresses
            .Where(address => address.BuyerId == buyer.Id)
            .ToListAsync(cancellationToken);
        var address = addresses.SingleOrDefault(item => item.Id == addressId);
        if (address is null)
        {
            return DeliveryAddressNotFound();
        }

        var shouldRemainDefault = request.IsDefault || address.IsDefault || addresses.Count == 1;
        try
        {
            address.Update(
                request.Label,
                request.RecipientName,
                request.PhoneNumber,
                request.AddressLine1,
                request.AddressLine2,
                request.Suburb,
                request.City,
                request.Province,
                request.PostalCode,
                request.CountryCode,
                shouldRemainDefault,
                request.DeliveryInstructions);
        }
        catch (ArgumentException exception)
        {
            return Validation(ToCamelCase(exception.ParamName ?? "deliveryAddress"), exception.Message);
        }

        if (address.IsDefault)
        {
            foreach (var otherAddress in addresses.Where(item => item.Id != address.Id))
            {
                otherAddress.SetDefault(false);
            }
        }
        else if (!addresses.Any(item => item.IsDefault))
        {
            address.SetDefault(true);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(MapDeliveryAddress(address));
    }

    private static async Task<IResult> DeleteDeliveryAddressAsync(
        Guid addressId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var addresses = await dbContext.BuyerDeliveryAddresses
            .Where(address => address.BuyerId == buyer.Id)
            .ToListAsync(cancellationToken);
        var address = addresses.SingleOrDefault(item => item.Id == addressId);
        if (address is null)
        {
            return DeliveryAddressNotFound();
        }

        var wasDefault = address.IsDefault;
        dbContext.BuyerDeliveryAddresses.Remove(address);
        if (wasDefault)
        {
            var replacement = addresses
                .Where(item => item.Id != address.Id)
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();
            replacement?.SetDefault(true);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.NoContent();
    }

    private static async Task<IResult> MakeDefaultDeliveryAddressAsync(
        Guid addressId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var addresses = await dbContext.BuyerDeliveryAddresses
            .Where(address => address.BuyerId == buyer.Id)
            .ToListAsync(cancellationToken);
        if (addresses.All(address => address.Id != addressId))
        {
            return DeliveryAddressNotFound();
        }

        foreach (var address in addresses)
        {
            address.SetDefault(address.Id == addressId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(addresses.OrderBy(address => address.CreatedAtUtc).Select(MapDeliveryAddress).ToArray());
    }

    private static Dictionary<string, string[]> ValidatePreferences(
        IReadOnlyCollection<BuyerNotificationPreferenceRequest> preferences)
    {
        var errors = new Dictionary<string, string[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var preference in preferences)
        {
            var category = preference.Category.Trim();
            if (!BuyerNotificationCategory.IsSupported(category))
            {
                errors["preferences"] = [$"Notification category '{preference.Category}' is not supported."];
                return errors;
            }

            if (!seen.Add(category))
            {
                errors["preferences"] = [$"Notification category '{category}' was provided more than once."];
                return errors;
            }
        }

        return errors;
    }

    private static async Task<BuyerProfileSettingsResponse> MapProfileAsync(
        BuyerProfile buyer,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var email = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == buyer.UserId)
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new BuyerProfileSettingsResponse(
            buyer.Id,
            buyer.UserId,
            email ?? string.Empty,
            buyer.DisplayName,
            buyer.PhoneNumber,
            buyer.CreatedAtUtc,
            buyer.UpdatedAtUtc);
    }

    private static async Task<BuyerNotificationPreferencesResponse> MapNotificationPreferencesAsync(
        Guid buyerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.BuyerNotificationPreferences
            .AsNoTracking()
            .Where(preference => preference.BuyerId == buyerId)
            .ToDictionaryAsync(preference => preference.Category, cancellationToken);

        var preferences = BuyerNotificationCategory.All
            .Select(category =>
            {
                var hasPreference = stored.TryGetValue(category, out var preference);
                return new BuyerNotificationPreferenceResponse(
                    category,
                    !hasPreference || preference!.IsEnabled,
                    !hasPreference || preference!.EmailEnabled);
            })
            .ToArray();

        return new BuyerNotificationPreferencesResponse(preferences);
    }

    private static async Task<IReadOnlyCollection<BuyerDeliveryAddressResponse>> MapDeliveryAddressesAsync(
        Guid buyerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var addresses = await dbContext.BuyerDeliveryAddresses
            .AsNoTracking()
            .Where(address => address.BuyerId == buyerId)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.Label)
            .ToListAsync(cancellationToken);

        return addresses.Select(MapDeliveryAddress).ToArray();
    }

    private static BuyerDeliveryAddress ToEntity(
        Guid buyerId,
        BuyerDeliveryAddressRequest request,
        bool isDefault) =>
        new(
            buyerId,
            request.Label,
            request.RecipientName,
            request.PhoneNumber,
            request.AddressLine1,
            request.AddressLine2,
            request.Suburb,
            request.City,
            request.Province,
            request.PostalCode,
            request.CountryCode,
            isDefault,
            request.DeliveryInstructions);

    private static BuyerDeliveryAddressResponse MapDeliveryAddress(BuyerDeliveryAddress address) =>
        new(
            address.Id,
            address.Label,
            address.RecipientName,
            address.PhoneNumber,
            address.AddressLine1,
            address.AddressLine2,
            address.Suburb,
            address.City,
            address.Province,
            address.PostalCode,
            address.CountryCode,
            address.DeliveryInstructions,
            address.IsDefault,
            address.CreatedAtUtc,
            address.UpdatedAtUtc);

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Buyer.NotFound",
            detail: "Buyer profile was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult Validation(string field, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static IResult DeliveryAddressNotFound() =>
        HttpResults.Problem(
            title: "BuyerDeliveryAddresses.NotFound",
            detail: "Delivery address was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}

public sealed record BuyerProfileSettingsRequest(
    string? DisplayName,
    string? PhoneNumber);

public sealed record BuyerProfileSettingsResponse(
    Guid BuyerId,
    Guid UserId,
    string Email,
    string? DisplayName,
    string? PhoneNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record BuyerNotificationPreferencesRequest(
    IReadOnlyCollection<BuyerNotificationPreferenceRequest> Preferences);

public sealed record BuyerNotificationPreferenceRequest(
    string Category,
    bool IsEnabled,
    bool? EmailEnabled = null);

public sealed record BuyerNotificationPreferencesResponse(
    IReadOnlyCollection<BuyerNotificationPreferenceResponse> Preferences);

public sealed record BuyerNotificationPreferenceResponse(
    string Category,
    bool IsEnabled,
    bool EmailEnabled);

public sealed record BuyerDeliveryAddressRequest(
    string Label,
    string RecipientName,
    string PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    bool IsDefault,
    string? DeliveryInstructions = null);

public sealed record BuyerDeliveryAddressResponse(
    Guid DeliveryAddressId,
    string Label,
    string RecipientName,
    string PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    string? DeliveryInstructions,
    bool IsDefault,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
