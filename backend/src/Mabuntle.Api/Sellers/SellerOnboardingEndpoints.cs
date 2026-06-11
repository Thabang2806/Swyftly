using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerOnboardingEndpoints
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public static IEndpointRouteBuilder MapSellerOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seller/onboarding")
            .WithTags("Seller Onboarding")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapGet("", GetAsync)
            .WithName("GetSellerOnboarding")
            .WithSummary("Returns the current seller onboarding state.")
            .Produces<SellerOnboardingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/profile", UpdateProfileAsync)
            .WithName("UpdateSellerOnboardingProfile")
            .WithSummary("Updates current seller profile details.")
            .Produces<SellerOnboardingResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/storefront", UpdateStorefrontAsync)
            .WithName("UpdateSellerOnboardingStorefront")
            .WithSummary("Updates current seller storefront details.")
            .Produces<SellerOnboardingResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/address", UpdateAddressAsync)
            .WithName("UpdateSellerOnboardingAddress")
            .WithSummary("Updates current seller address details.")
            .Produces<SellerOnboardingResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/payout", UpdatePayoutAsync)
            .WithName("UpdateSellerOnboardingPayout")
            .WithSummary("Stores a payout provider reference placeholder for future admin approval.")
            .Produces<SellerOnboardingResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/submit-verification", SubmitVerificationAsync)
            .WithName("SubmitSellerVerification")
            .WithSummary("Submits the current seller profile for verification review.")
            .Produces<SellerOnboardingResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

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

        return HttpResults.Ok(await CreateResponseAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateSellerProfileRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateProfile(request);
        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var businessType = Enum.Parse<SellerBusinessType>(request.BusinessType, ignoreCase: true);
        seller.UpdateProfile(
            request.DisplayName,
            request.ContactEmail,
            request.PhoneNumber,
            businessType,
            request.BusinessName);

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateResponseAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateStorefrontAsync(
        UpdateSellerStorefrontRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateStorefront(request);
        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        var slugExists = await dbContext.SellerStorefronts.AnyAsync(
            storefront => storefront.Slug == normalizedSlug && storefront.SellerId != seller.Id,
            cancellationToken);

        if (slugExists)
        {
            return HttpResults.Problem(
                title: "SellerOnboarding.StorefrontSlugUnavailable",
                detail: "That storefront slug is already in use.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var storefront = await dbContext.SellerStorefronts
            .SingleOrDefaultAsync(storefront => storefront.SellerId == seller.Id, cancellationToken);

        if (storefront is null)
        {
            dbContext.SellerStorefronts.Add(new SellerStorefront(
                seller.Id,
                request.StoreName,
                request.Slug,
                request.Description,
                request.LogoUrl,
                request.BannerUrl));
        }
        else
        {
            storefront.Update(
                request.StoreName,
                request.Slug,
                request.Description,
                request.LogoUrl,
                request.BannerUrl);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateResponseAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateAddressAsync(
        UpdateSellerAddressRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateAddress(request);
        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var address = await dbContext.SellerAddresses
            .SingleOrDefaultAsync(address => address.SellerId == seller.Id, cancellationToken);

        if (address is null)
        {
            dbContext.SellerAddresses.Add(new SellerAddress(
                seller.Id,
                request.AddressLine1,
                request.AddressLine2,
                request.City,
                request.Province,
                request.PostalCode,
                request.CountryCode));
        }
        else
        {
            address.Update(
                request.AddressLine1,
                request.AddressLine2,
                request.City,
                request.Province,
                request.PostalCode,
                request.CountryCode);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateResponseAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdatePayoutAsync(
        UpdateSellerPayoutRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidatePayout(request);
        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (seller.VerificationStatus == SellerVerificationStatus.Verified)
        {
            return HttpResults.Problem(
                title: "SellerOnboarding.PayoutProfileChangeRequired",
                detail: "Verified sellers must use the payout profile change request workflow.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var payoutProfile = await dbContext.SellerPayoutProfiles
            .SingleOrDefaultAsync(profile => profile.SellerId == seller.Id, cancellationToken);

        if (payoutProfile is null)
        {
            dbContext.SellerPayoutProfiles.Add(new SellerPayoutProfilePlaceholder(
                seller.Id,
                request.PayoutProviderReference));
        }
        else
        {
            payoutProfile.UpdateProviderReference(request.PayoutProviderReference);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateResponseAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<IResult> SubmitVerificationAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (seller.VerificationStatus == SellerVerificationStatus.Verified)
        {
            return HttpResults.Problem(
                title: "SellerOnboarding.AlreadyVerified",
                detail: "This seller has already been verified.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var related = await GetRelatedSellerDataAsync(seller.Id, dbContext, cancellationToken);
        if (!seller.CanSubmitForVerification(related.Storefront, related.Address, related.PayoutProfile))
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["onboarding"] = ["Complete seller profile, storefront, address, and payout placeholder before submitting for verification."]
            });
        }

        if (seller.VerificationStatus != SellerVerificationStatus.UnderReview)
        {
            seller.SubmitForVerification(related.Storefront, related.Address, related.PayoutProfile);
            dbContext.SellerVerifications.Add(new SellerVerification(
                seller.Id,
                timeProvider.GetUtcNow()));

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return HttpResults.Ok(await CreateResponseAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return await dbContext.SellerProfiles
            .SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken);
    }

    private static async Task<SellerOnboardingResponse> CreateResponseAsync(
        SellerProfile seller,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var related = await GetRelatedSellerDataAsync(seller.Id, dbContext, cancellationToken);
        var canSubmitForVerification = seller.CanSubmitForVerification(
            related.Storefront,
            related.Address,
            related.PayoutProfile);

        var latestVerification = await dbContext.SellerVerifications
            .Where(verification => verification.SellerId == seller.Id)
            .OrderByDescending(verification => verification.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var latestSuspensionAudit = await dbContext.AuditLogs
            .Where(auditLog => auditLog.EntityType == "SellerProfile"
                && auditLog.EntityId == seller.Id.ToString()
                && auditLog.ActionType == "SellerSuspended")
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new SellerOnboardingResponse(
            seller.Id,
            seller.VerificationStatus.ToString(),
            canSubmitForVerification,
            seller.HasRequiredProfileFields(),
            related.Storefront?.HasRequiredFields() == true,
            related.Address?.HasRequiredFields() == true,
            related.PayoutProfile?.HasSubmittedPlaceholder == true,
            ToProfileResponse(seller),
            related.Storefront is null ? null : ToStorefrontResponse(related.Storefront),
            related.Address is null ? null : ToAddressResponse(related.Address),
            related.PayoutProfile is null ? null : ToPayoutResponse(related.PayoutProfile),
            ToLatestVerificationReviewResponse(latestVerification, latestSuspensionAudit?.Reason));
    }

    private static async Task<SellerRelatedData> GetRelatedSellerDataAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var storefront = await dbContext.SellerStorefronts
            .SingleOrDefaultAsync(storefront => storefront.SellerId == sellerId, cancellationToken);
        var address = await dbContext.SellerAddresses
            .SingleOrDefaultAsync(address => address.SellerId == sellerId, cancellationToken);
        var payoutProfile = await dbContext.SellerPayoutProfiles
            .SingleOrDefaultAsync(profile => profile.SellerId == sellerId, cancellationToken);

        return new SellerRelatedData(storefront, address, payoutProfile);
    }

    private static SellerProfileResponse ToProfileResponse(SellerProfile seller) =>
        new(
            seller.DisplayName,
            seller.ContactEmail,
            seller.PhoneNumber,
            seller.BusinessType?.ToString(),
            seller.BusinessName);

    private static SellerStorefrontResponse ToStorefrontResponse(SellerStorefront storefront) =>
        new(
            storefront.StoreName,
            storefront.Slug,
            storefront.Description,
            storefront.LogoUrl,
            storefront.BannerUrl,
            storefront.IsPublished);

    private static SellerAddressResponse ToAddressResponse(SellerAddress address) =>
        new(
            address.AddressLine1,
            address.AddressLine2,
            address.City,
            address.Province,
            address.PostalCode,
            address.CountryCode);

    private static SellerPayoutResponse ToPayoutResponse(SellerPayoutProfilePlaceholder payoutProfile) =>
        new(
            payoutProfile.PayoutProviderReference,
            payoutProfile.HasSubmittedPlaceholder,
            payoutProfile.IsAdminApproved);

    private static SellerVerificationReviewResponse? ToLatestVerificationReviewResponse(
        SellerVerification? verification,
        string? suspensionReason)
    {
        if (verification is null && string.IsNullOrWhiteSpace(suspensionReason))
        {
            return null;
        }

        return new SellerVerificationReviewResponse(
            verification?.SubmittedAtUtc,
            verification?.ReviewedAtUtc,
            verification?.RejectionReason,
            string.IsNullOrWhiteSpace(suspensionReason) ? null : suspensionReason);
    }

    private static Dictionary<string, string[]> ValidateProfile(UpdateSellerProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequired(errors, nameof(request.DisplayName), request.DisplayName);
        AddRequired(errors, nameof(request.ContactEmail), request.ContactEmail);
        AddRequired(errors, nameof(request.PhoneNumber), request.PhoneNumber);

        if (!string.IsNullOrWhiteSpace(request.ContactEmail) && !EmailValidator.IsValid(request.ContactEmail))
        {
            errors[nameof(request.ContactEmail)] = ["Contact email must be a valid email address."];
        }

        if (!Enum.TryParse<SellerBusinessType>(request.BusinessType, ignoreCase: true, out var businessType))
        {
            errors[nameof(request.BusinessType)] = ["Business type must be Individual or RegisteredBusiness."];
        }
        else if (businessType == SellerBusinessType.RegisteredBusiness)
        {
            AddRequired(errors, nameof(request.BusinessName), request.BusinessName);
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateStorefront(UpdateSellerStorefrontRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequired(errors, nameof(request.StoreName), request.StoreName);
        AddRequired(errors, nameof(request.Slug), request.Slug);

        if (!string.IsNullOrWhiteSpace(request.Slug)
            && NormalizeSlug(request.Slug).Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            errors[nameof(request.Slug)] = ["Slug can only contain letters, numbers, and hyphens."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateAddress(UpdateSellerAddressRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequired(errors, nameof(request.AddressLine1), request.AddressLine1);
        AddRequired(errors, nameof(request.City), request.City);
        AddRequired(errors, nameof(request.Province), request.Province);
        AddRequired(errors, nameof(request.PostalCode), request.PostalCode);
        AddRequired(errors, nameof(request.CountryCode), request.CountryCode);

        if (!string.IsNullOrWhiteSpace(request.CountryCode) && request.CountryCode.Trim().Length != 2)
        {
            errors[nameof(request.CountryCode)] = ["Country code must be a 2-letter ISO code."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidatePayout(UpdateSellerPayoutRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequired(errors, nameof(request.PayoutProviderReference), request.PayoutProviderReference);
        return errors;
    }

    private static void AddRequired(
        Dictionary<string, string[]> errors,
        string field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
        }
    }

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerOnboarding.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private sealed record SellerRelatedData(
        SellerStorefront? Storefront,
        SellerAddress? Address,
        SellerPayoutProfilePlaceholder? PayoutProfile);
}

public sealed record SellerOnboardingResponse(
    Guid SellerId,
    string VerificationStatus,
    bool CanSubmitForVerification,
    bool IsProfileComplete,
    bool IsStorefrontComplete,
    bool IsAddressComplete,
    bool IsPayoutPlaceholderComplete,
    SellerProfileResponse Profile,
    SellerStorefrontResponse? Storefront,
    SellerAddressResponse? Address,
    SellerPayoutResponse? Payout,
    SellerVerificationReviewResponse? LatestVerificationReview);

public sealed record SellerProfileResponse(
    string? DisplayName,
    string? ContactEmail,
    string? PhoneNumber,
    string? BusinessType,
    string? BusinessName);

public sealed record SellerStorefrontResponse(
    string StoreName,
    string Slug,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    bool IsPublished);

public sealed record SellerAddressResponse(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Province,
    string PostalCode,
    string CountryCode);

public sealed record SellerPayoutResponse(
    string PayoutProviderReference,
    bool HasSubmittedPlaceholder,
    bool IsAdminApproved);

public sealed record SellerVerificationReviewResponse(
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? RejectionReason,
    string? SuspensionReason);

public sealed record UpdateSellerProfileRequest(
    string DisplayName,
    string ContactEmail,
    string PhoneNumber,
    string BusinessType,
    string? BusinessName);

public sealed record UpdateSellerStorefrontRequest(
    string StoreName,
    string Slug,
    string? Description,
    string? LogoUrl,
    string? BannerUrl);

public sealed record UpdateSellerAddressRequest(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Province,
    string PostalCode,
    string CountryCode);

public sealed record UpdateSellerPayoutRequest(string PayoutProviderReference);
