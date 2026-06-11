using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Delivery;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

var options = SeedOptions.Parse(args);

if (options is null)
{
    return args.Any(argument =>
        argument is "--help" or "-h" or "/?")
            ? 0
            : 2;
}

var services = new ServiceCollection();

services.AddLogging();

services.AddDbContext<MabuntleDbContext>(builder =>
{
    builder.UseNpgsql(options.ConnectionString, npgsql => npgsql.UseVector());
});

services
    .AddIdentityCore<ApplicationUser>(identityOptions =>
    {
        identityOptions.Password.RequiredLength = 8;
        identityOptions.Password.RequireDigit = true;
        identityOptions.Password.RequireLowercase = true;
        identityOptions.Password.RequireUppercase = true;
        identityOptions.Password.RequireNonAlphanumeric = true;
        identityOptions.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<MabuntleDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

if (options.ApplyMigrations)
{
    await dbContext.Database.MigrateAsync();
}

await EnsureRolesAsync(roleManager);

var admin = await EnsureUserAsync(
    userManager,
    "admin@mabuntle.local",
    options.Password,
    options.ResetPasswords,
    [
        MabuntleRoles.SuperAdmin,
        MabuntleRoles.Admin
    ]);

var financeOperator = await EnsureUserAsync(
    userManager,
    "finance.operator@mabuntle.local",
    options.Password,
    options.ResetPasswords,
    [MabuntleRoles.FinanceOperator]);

var financeApprover = await EnsureUserAsync(
    userManager,
    "finance.approver@mabuntle.local",
    options.Password,
    options.ResetPasswords,
    [MabuntleRoles.FinanceApprover]);

var supportAgent = await EnsureUserAsync(
    userManager,
    "support@mabuntle.local",
    options.Password,
    options.ResetPasswords,
    [MabuntleRoles.SupportAgent]);

var buyer = await EnsureUserAsync(
    userManager,
    "buyer@mabuntle.local",
    options.Password,
    options.ResetPasswords,
    [MabuntleRoles.Buyer]);

var sellerUser = await EnsureUserAsync(
    userManager,
    "seller@mabuntle.local",
    options.Password,
    options.ResetPasswords,
    [MabuntleRoles.Seller]);

var pendingSellerUser = options.SeedSellerFlowDemo
    ? await EnsureUserAsync(
        userManager,
        "seller.pending@mabuntle.local",
        options.Password,
        options.ResetPasswords,
        [MabuntleRoles.Seller])
    : null;

var buyerProfile = await EnsureBuyerProfileAsync(dbContext, buyer.Id);
var seller = await EnsureVerifiedSellerProfileAsync(dbContext, sellerUser.Id, admin.Id);

if (options.SeedSampleProducts || options.SeedSellerFlowDemo)
{
    await EnsureSampleProductsAsync(dbContext, seller.Id);
    await dbContext.SaveChangesAsync();
}

if (options.SeedSampleProducts)
{
    await EnsureBuyerDemoAddressAsync(dbContext, buyerProfile.Id);
}

if (options.SeedSellerFlowDemo && pendingSellerUser is not null)
{
    await EnsureSellerFlowDemoAsync(dbContext, pendingSellerUser.Id, seller.Id);
}

await dbContext.SaveChangesAsync();

Console.WriteLine("Mabuntle development users are ready.");
Console.WriteLine();
Console.WriteLine("Email                         Roles");
Console.WriteLine("admin@mabuntle.local           SuperAdmin, Admin");
Console.WriteLine("finance.operator@mabuntle.local FinanceOperator");
Console.WriteLine("finance.approver@mabuntle.local FinanceApprover");
Console.WriteLine("support@mabuntle.local         SupportAgent");
Console.WriteLine("buyer@mabuntle.local           Buyer");
Console.WriteLine("seller@mabuntle.local          Seller; verified seller profile; published storefront");
if (options.SeedSellerFlowDemo)
{
    Console.WriteLine("seller.pending@mabuntle.local  Seller; complete onboarding; pending admin review");
}

if (options.SeedSampleProducts)
{
    Console.WriteLine();
    Console.WriteLine("Sample buyer flow data seeded: default buyer address and 8 published products.");
}

if (options.SeedSellerFlowDemo)
{
    Console.WriteLine();
    Console.WriteLine("Seller flow demo data seeded: pending seller, pending product review, and pending ad review.");
}

return 0;

static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
{
    foreach (var role in MabuntleRoles.All)
    {
        if (await roleManager.RoleExistsAsync(role))
        {
            continue;
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        ThrowIfFailed(result, $"create role {role}");
    }
}

static async Task<ApplicationUser> EnsureUserAsync(
    UserManager<ApplicationUser> userManager,
    string email,
    string password,
    bool resetPassword,
    IReadOnlyCollection<string> roles)
{
    var user = await userManager.FindByEmailAsync(email);
    if (user is null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        ThrowIfFailed(createResult, $"create user {email}");
    }
    else if (resetPassword)
    {
        if (await userManager.HasPasswordAsync(user))
        {
            var removeResult = await userManager.RemovePasswordAsync(user);
            ThrowIfFailed(removeResult, $"remove existing password for {email}");
        }

        var addResult = await userManager.AddPasswordAsync(user, password);
        ThrowIfFailed(addResult, $"set password for {email}");
    }

    if (!user.EmailConfirmed)
    {
        user.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(user);
        ThrowIfFailed(updateResult, $"confirm email for {email}");
    }

    foreach (var role in roles)
    {
        if (await userManager.IsInRoleAsync(user, role))
        {
            continue;
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        ThrowIfFailed(roleResult, $"assign {role} to {email}");
    }

    return user;
}

static async Task<BuyerProfile> EnsureBuyerProfileAsync(MabuntleDbContext dbContext, Guid userId)
{
    var profile = await dbContext.BuyerProfiles.SingleOrDefaultAsync(item => item.UserId == userId);
    if (profile is null)
    {
        profile = new BuyerProfile(userId);
        dbContext.BuyerProfiles.Add(profile);
    }

    profile.UpdateSettings("Dev Buyer", "+27110000001");

    return profile;
}

static async Task<SellerProfile> EnsureVerifiedSellerProfileAsync(MabuntleDbContext dbContext, Guid userId, Guid adminUserId)
{
    var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(item => item.UserId == userId);
    if (seller is null)
    {
        seller = new SellerProfile(userId);
        dbContext.SellerProfiles.Add(seller);
        await dbContext.SaveChangesAsync();
    }

    seller.UpdateProfile(
        "Dev Seller",
        "seller@mabuntle.local",
        "+27110000002",
        SellerBusinessType.Individual,
        null);

    var storefront = await dbContext.SellerStorefronts.SingleOrDefaultAsync(item => item.SellerId == seller.Id);
    if (storefront is null)
    {
        var slug = await CreateUniqueStorefrontSlugAsync(dbContext, seller.Id);
        storefront = new SellerStorefront(
            seller.Id,
            "Mabuntle Dev Store",
            slug,
            "A local development storefront for manual testing.",
            null,
            null);
        dbContext.SellerStorefronts.Add(storefront);
    }
    else
    {
        storefront.Update(
            "Mabuntle Dev Store",
            storefront.Slug,
            "A local development storefront for manual testing.",
            storefront.LogoUrl,
            storefront.BannerUrl);
    }

    storefront.Publish();

    var address = await dbContext.SellerAddresses.SingleOrDefaultAsync(item => item.SellerId == seller.Id);
    if (address is null)
    {
        address = new SellerAddress(
            seller.Id,
            "10 Market Street",
            "Suite 4",
            "Johannesburg",
            "Gauteng",
            "2196",
            "ZA");
        dbContext.SellerAddresses.Add(address);
    }
    else
    {
        address.Update(
            "10 Market Street",
            "Suite 4",
            "Johannesburg",
            "Gauteng",
            "2196",
            "ZA");
    }

    var payout = await dbContext.SellerPayoutProfiles.SingleOrDefaultAsync(item => item.SellerId == seller.Id);
    if (payout is null)
    {
        payout = new SellerPayoutProfilePlaceholder(seller.Id, "dev-payout-reference");
        dbContext.SellerPayoutProfiles.Add(payout);
    }
    else if (!string.Equals(payout.PayoutProviderReference, "dev-payout-reference", StringComparison.Ordinal))
    {
        payout.UpdateProviderReference("dev-payout-reference");
    }

    payout.MarkAdminApproved(adminUserId, DateTimeOffset.UtcNow);
    seller.MarkVerified(storefront, address, payout);

    if (!await dbContext.SellerDeliveryMethods.AnyAsync(item => item.SellerId == seller.Id))
    {
        dbContext.SellerDeliveryMethods.Add(new SellerDeliveryMethod(
            seller.Id,
            "Standard courier",
            "Door-to-door delivery within South Africa.",
            SellerDeliveryMethodType.Standard,
            "ZA",
            null,
            75m,
            1000m,
            2,
            5,
            10,
            true));
    }

    if (!await dbContext.SellerBalances.AnyAsync(item => item.SellerId == seller.Id && item.Currency == "ZAR"))
    {
        dbContext.SellerBalances.Add(new SellerBalance(seller.Id, "ZAR"));
    }

    return seller;
}

static async Task EnsureBuyerDemoAddressAsync(MabuntleDbContext dbContext, Guid buyerId)
{
    var address = await dbContext.BuyerDeliveryAddresses
        .SingleOrDefaultAsync(item => item.BuyerId == buyerId && item.Label == "Dev Home");

    if (address is null)
    {
        address = new BuyerDeliveryAddress(
            buyerId,
            "Dev Home",
            "Dev Buyer",
            "+27110000001",
            "10 Market Street",
            "Apartment 4",
            "Rosebank",
            "Johannesburg",
            "Gauteng",
            "2196",
            "ZA",
            true,
            "Call on arrival and leave with reception if unavailable.");
        dbContext.BuyerDeliveryAddresses.Add(address);
    }
    else
    {
        address.Update(
            "Dev Home",
            "Dev Buyer",
            "+27110000001",
            "10 Market Street",
            "Apartment 4",
            "Rosebank",
            "Johannesburg",
            "Gauteng",
            "2196",
            "ZA",
            true,
            "Call on arrival and leave with reception if unavailable.");
    }

    var defaults = await dbContext.BuyerDeliveryAddresses
        .Where(item => item.BuyerId == buyerId && item.Id != address.Id && item.IsDefault)
        .ToListAsync();

    foreach (var existingDefault in defaults)
    {
        existingDefault.SetDefault(false);
    }

    address.SetVerification(AddressVerificationStatus.Verified, "LocalRules", "[]", DateTimeOffset.UtcNow);
}

static async Task EnsureSampleProductsAsync(MabuntleDbContext dbContext, Guid sellerId)
{
    var now = DateTimeOffset.UtcNow;
    var seeds = GetSampleProducts();

    foreach (var seed in seeds)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(item => item.SellerId == sellerId && item.Slug == seed.Slug);

        if (product is null)
        {
            product = new Product(sellerId);
            dbContext.Products.Add(product);
        }

        EnsureSeedListing(product, seed);
        await EnsureSeedAttributesAsync(dbContext, product.Id, seed.Attributes);
        await EnsureSeedVariantsAsync(dbContext, product.Id, seed.Variants);
        await EnsureSeedImageAsync(dbContext, product.Id, seed);

        if (product.CanSellerEdit)
        {
            product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
            product.Publish(now);
        }
        else if (product.Status is ProductStatus.PendingReview or ProductStatus.NeedsAdminReview)
        {
            product.Publish(now);
        }
    }
}

static async Task EnsureSellerFlowDemoAsync(
    MabuntleDbContext dbContext,
    Guid pendingSellerUserId,
    Guid verifiedSellerId)
{
    await EnsurePendingSellerProfileAsync(dbContext, pendingSellerUserId);
    var pendingProduct = await EnsurePendingReviewProductAsync(dbContext, verifiedSellerId);
    var promotedProduct = await dbContext.Products
        .SingleOrDefaultAsync(item => item.SellerId == verifiedSellerId && item.Slug == "rose-linen-midi-dress");

    if (promotedProduct is null)
    {
        promotedProduct = pendingProduct;
    }

    await EnsurePendingAdCampaignAsync(dbContext, verifiedSellerId, promotedProduct.Id);
}

static async Task<SellerProfile> EnsurePendingSellerProfileAsync(MabuntleDbContext dbContext, Guid userId)
{
    var now = DateTimeOffset.UtcNow;
    var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(item => item.UserId == userId);
    if (seller is null)
    {
        seller = new SellerProfile(userId);
        dbContext.SellerProfiles.Add(seller);
    }

    seller.UpdateProfile(
        "Demo Pending Seller",
        "seller.pending@mabuntle.local",
        "+27110000003",
        SellerBusinessType.RegisteredBusiness,
        "Demo Pending Trading");

    var storefront = await dbContext.SellerStorefronts.SingleOrDefaultAsync(item => item.SellerId == seller.Id);
    if (storefront is null)
    {
        storefront = new SellerStorefront(
            seller.Id,
            "Demo Pending Atelier",
            "demo-pending-atelier",
            "A complete seller profile waiting for admin verification in local demo flows.",
            null,
            null);
        dbContext.SellerStorefronts.Add(storefront);
    }
    else
    {
        storefront.Update(
            "Demo Pending Atelier",
            "demo-pending-atelier",
            "A complete seller profile waiting for admin verification in local demo flows.",
            storefront.LogoUrl,
            storefront.BannerUrl);
    }

    storefront.Publish();

    var address = await dbContext.SellerAddresses.SingleOrDefaultAsync(item => item.SellerId == seller.Id);
    if (address is null)
    {
        address = new SellerAddress(
            seller.Id,
            "22 Atelier Lane",
            "Studio 2",
            "Cape Town",
            "Western Cape",
            "8001",
            "ZA");
        dbContext.SellerAddresses.Add(address);
    }
    else
    {
        address.Update(
            "22 Atelier Lane",
            "Studio 2",
            "Cape Town",
            "Western Cape",
            "8001",
            "ZA");
    }

    var payout = await dbContext.SellerPayoutProfiles.SingleOrDefaultAsync(item => item.SellerId == seller.Id);
    if (payout is null)
    {
        payout = new SellerPayoutProfilePlaceholder(seller.Id, "demo-pending-payout-reference");
        dbContext.SellerPayoutProfiles.Add(payout);
    }
    else
    {
        payout.UpdateProviderReference("demo-pending-payout-reference");
    }

    seller.SubmitForVerification(storefront, address, payout);

    var latestVerification = await dbContext.SellerVerifications
        .Where(item => item.SellerId == seller.Id)
        .OrderByDescending(item => item.SubmittedAtUtc)
        .FirstOrDefaultAsync();

    if (latestVerification?.Status != SellerVerificationStatus.UnderReview)
    {
        dbContext.SellerVerifications.Add(new SellerVerification(seller.Id, now));
    }

    return seller;
}

static async Task<Product> EnsurePendingReviewProductAsync(MabuntleDbContext dbContext, Guid sellerId)
{
    var seed = GetSellerFlowPendingProduct();
    var product = await dbContext.Products
        .SingleOrDefaultAsync(item => item.SellerId == sellerId && item.Slug == seed.Slug);

    if (product is null)
    {
        product = new Product(sellerId);
        dbContext.Products.Add(product);
    }

    if (!product.CanSellerEdit
        && product.Status is not ProductStatus.Published
        && product.Status is not ProductStatus.PendingReview
        && product.Status is not ProductStatus.NeedsAdminReview)
    {
        ResetProductStatusForDemo(dbContext, product, ProductStatus.Draft);
    }

    EnsureSeedListing(product, seed);
    await EnsureSeedAttributesAsync(dbContext, product.Id, seed.Attributes);
    await EnsureSeedVariantsAsync(dbContext, product.Id, seed.Variants);
    await EnsureSeedImageAsync(dbContext, product.Id, seed);

    if (product.CanSellerEdit)
    {
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
    }
    else if (product.Status is not (ProductStatus.PendingReview or ProductStatus.NeedsAdminReview))
    {
        ResetProductStatusForDemo(dbContext, product, ProductStatus.PendingReview);
    }

    return product;
}

static async Task EnsurePendingAdCampaignAsync(MabuntleDbContext dbContext, Guid sellerId, Guid productId)
{
    var now = DateTimeOffset.UtcNow;
    const string campaignName = "Demo Review Campaign - Rose Linen Dress";
    var startsAtUtc = now.AddDays(1);
    var endsAtUtc = now.AddDays(15);

    var campaign = await dbContext.AdCampaigns
        .Include(item => item.Products)
        .SingleOrDefaultAsync(item => item.SellerId == sellerId && item.Name == campaignName);

    if (campaign is null)
    {
        campaign = new AdCampaign(
            sellerId,
            campaignName,
            AdCampaignType.FeaturedProduct,
            startsAtUtc,
            endsAtUtc,
            now);
        campaign.ReplaceProducts([productId], now);
        dbContext.AdCampaigns.Add(campaign);
    }
    else
    {
        if (!campaign.CanSellerEdit && campaign.Status != AdCampaignStatus.PendingReview)
        {
            ResetAdCampaignStatusForDemo(dbContext, campaign, AdCampaignStatus.Draft, now);
        }

        if (campaign.CanSellerEdit)
        {
            var existingProducts = campaign.Products.ToArray();
            dbContext.AdCampaignProducts.RemoveRange(existingProducts);
            campaign.UpdateDraft(campaignName, AdCampaignType.FeaturedProduct, startsAtUtc, endsAtUtc, now);
            campaign.ReplaceProducts([productId], now);
            dbContext.AdCampaignProducts.AddRange(campaign.Products);
        }
    }

    await EnsureDemoAdBudgetAsync(dbContext, campaign.Id, now);

    if (campaign.CanSellerEdit)
    {
        campaign.SubmitForReview(now);
    }
    else if (campaign.Status != AdCampaignStatus.PendingReview)
    {
        ResetAdCampaignStatusForDemo(dbContext, campaign, AdCampaignStatus.PendingReview, now);
    }
}

static async Task EnsureDemoAdBudgetAsync(MabuntleDbContext dbContext, Guid campaignId, DateTimeOffset now)
{
    var budget = await dbContext.AdBudgets.SingleOrDefaultAsync(item => item.AdCampaignId == campaignId);
    if (budget is null)
    {
        dbContext.AdBudgets.Add(new AdBudget(campaignId, "ZAR", 150m, 1500m, 6m, now));
        return;
    }

    budget.Update("ZAR", 150m, 1500m, 6m, now);
}

static void ResetProductStatusForDemo(MabuntleDbContext dbContext, Product product, ProductStatus status)
{
    dbContext.Entry(product).Property(nameof(Product.Status)).CurrentValue = status;
    dbContext.Entry(product).Property(nameof(Product.PublishedAtUtc)).CurrentValue = null;
    dbContext.Entry(product).Property(nameof(Product.RejectionReason)).CurrentValue = null;
}

static void ResetAdCampaignStatusForDemo(
    MabuntleDbContext dbContext,
    AdCampaign campaign,
    AdCampaignStatus status,
    DateTimeOffset now)
{
    dbContext.Entry(campaign).Property(nameof(AdCampaign.Status)).CurrentValue = status;
    dbContext.Entry(campaign).Property(nameof(AdCampaign.SubmittedAtUtc)).CurrentValue =
        status == AdCampaignStatus.PendingReview ? now : null;
    dbContext.Entry(campaign).Property(nameof(AdCampaign.ApprovedAtUtc)).CurrentValue = null;
    dbContext.Entry(campaign).Property(nameof(AdCampaign.ApprovedByUserId)).CurrentValue = null;
    dbContext.Entry(campaign).Property(nameof(AdCampaign.PausedAtUtc)).CurrentValue = null;
    dbContext.Entry(campaign).Property(nameof(AdCampaign.CompletedAtUtc)).CurrentValue = null;
    dbContext.Entry(campaign).Property(nameof(AdCampaign.CancelledAtUtc)).CurrentValue = null;
    dbContext.Entry(campaign).Property(nameof(AdCampaign.RejectionReason)).CurrentValue = null;
    dbContext.Entry(campaign).Property(nameof(AdCampaign.UpdatedAtUtc)).CurrentValue = now;
}

static void EnsureSeedListing(Product product, SampleProductSeed seed)
{
    var tagsJson = JsonSerializer.Serialize(seed.Tags);

    if (product.Status == ProductStatus.OutOfStock)
    {
        product.Restock();
    }

    if (product.CanSellerEdit)
    {
        product.UpdateDraftDetails(
            seed.CategoryId,
            null,
            seed.Title,
            seed.Slug,
            seed.ShortDescription,
            seed.FullDescription);
        product.UpdateTags(tagsJson);
        return;
    }

    if (product.Status == ProductStatus.Published)
    {
        product.ApplyApprovedListingRevision(
            seed.CategoryId,
            null,
            seed.Title,
            seed.Slug,
            seed.ShortDescription,
            seed.FullDescription,
            tagsJson);
    }
}

static async Task EnsureSeedAttributesAsync(
    MabuntleDbContext dbContext,
    Guid productId,
    IReadOnlyCollection<SampleProductAttributeSeed> attributes)
{
    var existing = await dbContext.ProductAttributeValues
        .Where(item => item.ProductId == productId)
        .ToDictionaryAsync(item => item.Key, StringComparer.OrdinalIgnoreCase);

    foreach (var attribute in attributes)
    {
        var valueJson = JsonSerializer.Serialize(attribute.Value);
        if (existing.TryGetValue(attribute.Key, out var row))
        {
            row.UpdateValue(valueJson);
            continue;
        }

        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(productId, attribute.Key, valueJson));
    }
}

static async Task EnsureSeedVariantsAsync(
    MabuntleDbContext dbContext,
    Guid productId,
    IReadOnlyCollection<SampleProductVariantSeed> variants)
{
    var existing = await dbContext.ProductVariants
        .Where(item => item.ProductId == productId)
        .ToListAsync();

    foreach (var seed in variants)
    {
        var variant = existing.SingleOrDefault(item => string.Equals(item.Sku, seed.Sku, StringComparison.OrdinalIgnoreCase));
        if (variant is null)
        {
            dbContext.ProductVariants.Add(new ProductVariant(
                productId,
                seed.Sku,
                seed.Size,
                seed.Colour,
                seed.Price,
                seed.CompareAtPrice,
                seed.StockQuantity));
            continue;
        }

        var stockQuantity = Math.Max(seed.StockQuantity, variant.ReservedQuantity);
        variant.Update(
            seed.Sku,
            seed.Size,
            seed.Colour,
            seed.Price,
            seed.CompareAtPrice,
            stockQuantity,
            variant.ReservedQuantity,
            ProductVariantStatus.Active,
            null);
    }
}

static async Task EnsureSeedImageAsync(MabuntleDbContext dbContext, Guid productId, SampleProductSeed seed)
{
    var images = await dbContext.ProductImages
        .Where(item => item.ProductId == productId)
        .ToListAsync();
    var storageKey = $"dev-seed/sample-products/{seed.ImageFileName}";
    var image = images.SingleOrDefault(item => string.Equals(item.StorageKey, storageKey, StringComparison.OrdinalIgnoreCase));

    foreach (var existingImage in images.Where(item => item.IsPrimary))
    {
        existingImage.ClearPrimary();
    }

    if (image is null)
    {
        image = new ProductImage(
            productId,
            $"/assets/sample-products/{seed.ImageFileName}",
            storageKey,
            seed.ImageAltText,
            0,
            true,
            DateTimeOffset.UtcNow);
        dbContext.ProductImages.Add(image);
        return;
    }

    image.UpdateMetadata(seed.ImageAltText, 0);
    image.MarkPrimary();
}

static SampleProductSeed GetSellerFlowPendingProduct() =>
    new(
        "demo-review-satin-slip-skirt",
        SampleCategoryIds.WomenDresses,
        "Demo Review Satin Slip Skirt",
        "A complete seller-submitted skirt listing waiting for admin review.",
        "This satin slip skirt is seeded specifically for the seller approval flow. It has a category, rich descriptions, product attributes, a primary image, and active variants so an admin can approve or reject it from the product moderation queue.",
        ["demo", "seller-flow", "skirt", "satin", "review"],
        [
            new("size", "M"),
            new("colour", "Plum"),
            new("material", "Satin")
        ],
        [
            new("DEMO-REVIEW-SKIRT-PLUM-S", "S", "Plum", 1099m, 1399m, 9),
            new("DEMO-REVIEW-SKIRT-PLUM-M", "M", "Plum", 1099m, 1399m, 12),
            new("DEMO-REVIEW-SKIRT-PLUM-L", "L", "Plum", 1099m, 1399m, 6)
        ],
        "rose-linen-midi-dress.svg",
        "Demo satin slip skirt awaiting product review");

static IReadOnlyList<SampleProductSeed> GetSampleProducts() =>
[
    new(
        "rose-linen-midi-dress",
        SampleCategoryIds.WomenDresses,
        "Rose Linen Midi Dress",
        "A softly tailored linen midi dress with an easy day-to-evening shape.",
        "Cut from breathable linen-blend fabric, this rose midi dress is designed for warm-weather occasions, weekend lunches, and relaxed events. The silhouette is polished without feeling formal, with clean seams, a flattering waist, and enough movement for everyday wear.",
        ["dress", "linen", "rose", "occasionwear", "summer"],
        [
            new("size", "M"),
            new("colour", "Rose"),
            new("material", "Linen blend")
        ],
        [
            new("DEV-DRESS-ROSE-S", "S", "Rose", 1299m, 1599m, 12),
            new("DEV-DRESS-ROSE-M", "M", "Rose", 1299m, 1599m, 15),
            new("DEV-DRESS-ROSE-L", "L", "Rose", 1299m, 1599m, 10),
            new("DEV-DRESS-BLACK-M", "M", "Black", 1299m, 1599m, 8)
        ],
        "rose-linen-midi-dress.svg",
        "Rose linen midi dress on an ivory editorial background"),
    new(
        "ivory-silk-wrap-blouse",
        SampleCategoryIds.WomenTops,
        "Ivory Silk Wrap Blouse",
        "An ivory wrap blouse with a fluid drape and understated sheen.",
        "This silk-feel wrap blouse pairs cleanly with tailored trousers, denim, or a midi skirt. The soft ivory colour keeps it versatile, while the wrap shape adds polish for work, dinners, and elevated everyday styling.",
        ["blouse", "ivory", "silk", "workwear", "tops"],
        [
            new("size", "S"),
            new("colour", "Ivory")
        ],
        [
            new("DEV-BLOUSE-IVORY-XS", "XS", "Ivory", 899m, null, 9),
            new("DEV-BLOUSE-IVORY-S", "S", "Ivory", 899m, null, 14),
            new("DEV-BLOUSE-IVORY-M", "M", "Ivory", 899m, null, 11),
            new("DEV-BLOUSE-IVORY-L", "L", "Ivory", 899m, null, 6)
        ],
        "ivory-silk-wrap-blouse.svg",
        "Ivory silk wrap blouse styled on a champagne background"),
    new(
        "black-structured-leather-tote",
        SampleCategoryIds.AccessoriesBags,
        "Black Structured Leather Tote",
        "A structured black tote with enough room for daily essentials.",
        "Designed for workdays, travel, and polished daily carry, this black leather tote keeps its shape while offering a generous interior. Minimal hardware and a refined finish make it easy to style with fashion or beauty purchases.",
        ["bag", "tote", "leather", "black", "workwear"],
        [
            new("material", "Leather"),
            new("colour", "Black")
        ],
        [
            new("DEV-TOTE-BLACK-OS", "One size", "Black", 1899m, 2299m, 7)
        ],
        "black-structured-leather-tote.svg",
        "Black structured leather tote with champagne accents"),
    new(
        "champagne-mini-crossbody-bag",
        SampleCategoryIds.AccessoriesBags,
        "Champagne Mini Crossbody Bag",
        "A compact champagne crossbody bag for evenings and weekend styling.",
        "This mini crossbody adds a soft metallic note without overpowering an outfit. The vegan leather finish, adjustable strap, and compact interior make it suitable for events, errands, and occasionwear testing.",
        ["bag", "crossbody", "champagne", "vegan leather", "occasion"],
        [
            new("material", "Vegan Leather"),
            new("colour", "Champagne")
        ],
        [
            new("DEV-CROSSBODY-CHAMPAGNE-OS", "One size", "Champagne", 749m, null, 13)
        ],
        "champagne-mini-crossbody-bag.svg",
        "Champagne mini crossbody bag on a warm ivory background"),
    new(
        "gold-polished-hoop-earrings",
        SampleCategoryIds.JewelleryHoopEarrings,
        "Gold Polished Hoop Earrings",
        "Polished gold hoop earrings with a clean everyday profile.",
        "These gold-tone hoop earrings are lightweight enough for daily wear and refined enough for a dressed-up look. The polished finish catches light neatly without heavy embellishment.",
        ["jewellery", "earrings", "hoops", "gold", "gift"],
        [
            new("material", "Gold"),
            new("colour", "Gold")
        ],
        [
            new("DEV-HOOPS-GOLD-OS", "One size", "Gold", 429m, null, 24)
        ],
        "gold-polished-hoop-earrings.svg",
        "Gold polished hoop earrings on a blush background"),
    new(
        "silver-stacking-ring-set",
        SampleCategoryIds.JewelleryRings,
        "Silver Stacking Ring Set",
        "A silver stacking ring set with three slim polished bands.",
        "Wear the rings together for a layered look or split them across fingers for a lighter finish. The polished silver tone is versatile enough for everyday jewellery styling and gifting.",
        ["jewellery", "rings", "silver", "stacking", "gift"],
        [
            new("material", "Silver"),
            new("ring-size", "7")
        ],
        [
            new("DEV-RING-SILVER-6", "6", "Silver", 549m, null, 8),
            new("DEV-RING-SILVER-7", "7", "Silver", 549m, null, 10),
            new("DEV-RING-SILVER-8", "8", "Silver", 549m, null, 7)
        ],
        "silver-stacking-ring-set.svg",
        "Silver stacking ring set on a deep plum background"),
    new(
        "hydrating-cream-cleanser",
        SampleCategoryIds.BeautyCleansers,
        "Hydrating Cream Cleanser",
        "A gentle cream cleanser for normal to dry skin types.",
        "This hydrating cleanser is designed for normal, dry, and sensitive-feeling skin. Ingredients are listed for testing only: water, glycerin, sunflower oil, and mild cleansing agents. Batch number DEV-CLN-001. Expiry date 2027-12. Sealed/unsealed status: sealed.",
        ["beauty", "cleanser", "hydrating", "skincare", "sealed"],
        [
            new("skin-type", "Dry"),
            new("volume-ml", 150)
        ],
        [
            new("DEV-CLEANSER-150ML", "150 ml", "Cream", 249m, null, 22)
        ],
        "hydrating-cream-cleanser.svg",
        "Hydrating cream cleanser bottle on ivory and blush background"),
    new(
        "soft-matte-foundation",
        SampleCategoryIds.BeautyFoundation,
        "Soft Matte Foundation",
        "A soft matte liquid foundation with buildable medium coverage.",
        "This foundation is suitable for normal and combination skin types with a soft matte finish. Shade: Warm Beige. Ingredients are listed for testing only: water, pigments, glycerin, and silicone elastomer. Batch number DEV-FDN-001. Expiry date 2027-10. Sealed/unsealed status: sealed.",
        ["beauty", "foundation", "makeup", "matte", "sealed"],
        [
            new("shade", "Warm Beige"),
            new("skin-type", "Combination"),
            new("volume-ml", 30)
        ],
        [
            new("DEV-FOUNDATION-WARM-BEIGE-30ML", "30 ml", "Warm Beige", 389m, null, 18)
        ],
        "soft-matte-foundation.svg",
        "Soft matte foundation bottle on champagne background")
];

static async Task<string> CreateUniqueStorefrontSlugAsync(MabuntleDbContext dbContext, Guid sellerId)
{
    const string baseSlug = "mabuntle-dev-store";
    var existing = await dbContext.SellerStorefronts.SingleOrDefaultAsync(item => item.Slug == baseSlug);
    if (existing is null || existing.SellerId == sellerId)
    {
        return baseSlug;
    }

    return $"mabuntle-dev-store-{sellerId.ToString("N")[..8]}";
}

static void ThrowIfFailed(IdentityResult result, string action)
{
    if (result.Succeeded)
    {
        return;
    }

    var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
    throw new InvalidOperationException($"Could not {action}: {errors}");
}

static class SampleCategoryIds
{
    public static readonly Guid WomenDresses = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid WomenTops = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid JewelleryHoopEarrings = Guid.Parse("20000000-0000-0000-0000-000000000010");
    public static readonly Guid JewelleryRings = Guid.Parse("20000000-0000-0000-0000-000000000011");
    public static readonly Guid AccessoriesBags = Guid.Parse("20000000-0000-0000-0000-000000000013");
    public static readonly Guid BeautyFoundation = Guid.Parse("20000000-0000-0000-0000-000000000017");
    public static readonly Guid BeautyCleansers = Guid.Parse("20000000-0000-0000-0000-000000000019");
}

sealed record SampleProductSeed(
    string Slug,
    Guid CategoryId,
    string Title,
    string ShortDescription,
    string FullDescription,
    IReadOnlyList<string> Tags,
    IReadOnlyCollection<SampleProductAttributeSeed> Attributes,
    IReadOnlyCollection<SampleProductVariantSeed> Variants,
    string ImageFileName,
    string ImageAltText);

sealed record SampleProductAttributeSeed(string Key, object Value);

sealed record SampleProductVariantSeed(
    string Sku,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity);

sealed record SeedOptions(
    string ConnectionString,
    string Password,
    bool ResetPasswords,
    bool ApplyMigrations,
    bool SeedSampleProducts,
    bool SeedSellerFlowDemo)
{
    public static SeedOptions? Parse(string[] args)
    {
        string? connectionString = null;
        string? password = null;
        var resetPasswords = false;
        var applyMigrations = false;
        var seedSampleProducts = false;
        var seedSellerFlowDemo = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--connection" when index + 1 < args.Length:
                    connectionString = args[++index];
                    break;
                case "--password" when index + 1 < args.Length:
                    password = args[++index];
                    break;
                case "--reset-passwords":
                    resetPasswords = true;
                    break;
                case "--apply-migrations":
                    applyMigrations = true;
                    break;
                case "--seed-sample-products":
                    seedSampleProducts = true;
                    break;
                case "--seed-seller-flow-demo":
                    seedSellerFlowDemo = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    PrintUsage();
                    return null;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[index]}");
                    PrintUsage();
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(password))
        {
            PrintUsage();
            return null;
        }

        return new SeedOptions(connectionString, password, resetPasswords, applyMigrations, seedSampleProducts, seedSellerFlowDemo);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project scripts/Mabuntle.DevSeed -- --connection \"Host=...\" --password \"Your_dev_password1!\"");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --reset-passwords   Replace passwords for existing seeded users.");
        Console.WriteLine("  --apply-migrations  Apply EF migrations before seeding.");
        Console.WriteLine("  --seed-sample-products  Seed demo buyer address and published product catalog.");
        Console.WriteLine("  --seed-seller-flow-demo Seed pending seller, product, and ad approval demo records.");
    }
}
