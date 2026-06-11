using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Admin;
using Mabuntle.Api.Authentication;
using Mabuntle.Api.Sellers;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class AdminSellerApprovalTests
{
    [Fact]
    public async Task Buyer_CannotAccessAdminSellerEndpoints()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/sellers/pending");
        using var allResponse = await client.GetAsync("/api/admin/sellers");
        using var triageResponse = await client.GetAsync($"/api/admin/moderation-queue/items/Seller/{Guid.NewGuid()}/triage");
        using var viewsResponse = await client.GetAsync("/api/admin/moderation-queue/views");
        using var summaryResponse = await client.GetAsync("/api/admin/moderation-queue/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, allResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, triageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, summaryResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CanClaimAndTriageSellerQueueItem()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var claimResponse = await client.PostAsync($"/api/admin/moderation-queue/items/Seller/{seller.SellerId}/claim", null);
        claimResponse.EnsureSuccessStatusCode();
        var claimed = await claimResponse.Content.ReadFromJsonAsync<AdminQueueTriageResponse>();
        Assert.NotNull(claimed);
        Assert.Equal("Normal", claimed!.Priority);
        Assert.NotNull(claimed.AssignedToUserId);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/moderation-queue/items/Seller/{seller.SellerId}/triage",
            new AdminQueueTriageUpdateRequest("High", "Review evidence before final approval.", null, null));
        updateResponse.EnsureSuccessStatusCode();

        using var filteredResponse = await client.GetAsync("/api/admin/sellers?assigned=Mine&priority=High&hasNotes=true");
        filteredResponse.EnsureSuccessStatusCode();
        var filtered = await filteredResponse.Content.ReadFromJsonAsync<AdminPagedResponse<AdminSellerOperationalSummaryResponse>>();
        Assert.NotNull(filtered);
        var item = Assert.Single(filtered!.Items, row => row.SellerId == seller.SellerId);
        Assert.Equal("High", item.Priority);
        Assert.Equal(1, item.TriageNoteCount);
        Assert.Equal("Review evidence before final approval.", item.LatestTriageNote);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.True(await dbContext.AuditLogs.AnyAsync(log => log.ActionType == "AdminQueueItemClaimed"));
        Assert.True(await dbContext.AuditLogs.AnyAsync(log => log.ActionType == "AdminQueueTriageUpdated"));
    }

    [Fact]
    public async Task Admin_CanSaveQueueView_AndUseSlaFiltersAndSummary()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/moderation-queue/views",
            new AdminQueueSavedViewRequest(
                "Sellers",
                "My urgent seller queue",
                true,
                new AdminQueueSavedViewFiltersRequest("NeedsAttention", "UnderReview", null, "Seller Store", null, "Any", "Normal", false, "OnTrack", "UpdatedDesc", 10)));
        createResponse.EnsureSuccessStatusCode();
        var savedView = await createResponse.Content.ReadFromJsonAsync<AdminQueueSavedViewResponse>();
        Assert.NotNull(savedView);
        Assert.True(savedView!.IsDefault);

        using var listResponse = await client.GetAsync($"/api/admin/sellers?savedViewId={savedView.ViewId}");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<AdminPagedResponse<AdminSellerOperationalSummaryResponse>>();
        Assert.NotNull(list);
        var row = Assert.Single(list!.Items, item => item.SellerId == seller.SellerId);
        Assert.Equal("OnTrack", row.SlaStatus);
        Assert.True(row.SlaDueAtUtc.HasValue);

        using var viewsResponse = await client.GetAsync("/api/admin/moderation-queue/views?queue=Sellers");
        viewsResponse.EnsureSuccessStatusCode();
        var views = await viewsResponse.Content.ReadFromJsonAsync<AdminQueueSavedViewResponse[]>();
        Assert.Contains(views!, item => item.ViewId == savedView.ViewId && item.IsDefault);

        using var summaryResponse = await client.GetAsync("/api/admin/moderation-queue/summary");
        summaryResponse.EnsureSuccessStatusCode();
        var summary = await summaryResponse.Content.ReadFromJsonAsync<AdminQueueSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Contains(summary!.ItemTypeCounts, item => item.Key == "Seller" && item.Count >= 1);
        Assert.Contains(summary.SlaCounts, item => item.Key == "OnTrack" && item.Count >= 1);
    }

    [Fact]
    public async Task Admin_CanListPendingSellers()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/sellers/pending");

        response.EnsureSuccessStatusCode();
        var sellers = await response.Content.ReadFromJsonAsync<AdminSellerSummaryResponse[]>();
        Assert.NotNull(sellers);
        var pendingSeller = Assert.Single(sellers!, s => s.SellerId == seller.SellerId);
        Assert.Equal("UnderReview", pendingSeller.VerificationStatus);
    }

    [Fact]
    public async Task Admin_CanListOperationalSellers_WithAllStateFilters()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var needsAttentionResponse = await client.GetAsync("/api/admin/sellers");
        needsAttentionResponse.EnsureSuccessStatusCode();
        var needsAttention = await needsAttentionResponse.Content.ReadFromJsonAsync<AdminPagedResponse<AdminSellerOperationalSummaryResponse>>();
        Assert.NotNull(needsAttention);
        var pendingSeller = Assert.Single(needsAttention!.Items, item => item.SellerId == seller.SellerId);
        Assert.Equal("UnderReview", pendingSeller.VerificationStatus);
        Assert.Equal($"/admin/sellers/{seller.SellerId}", pendingSeller.DetailRoute);

        using var approveResponse = await client.PostAsJsonAsync($"/api/admin/sellers/{seller.SellerId}/approve", new { });
        approveResponse.EnsureSuccessStatusCode();

        using var allStateResponse = await client.GetAsync("/api/admin/sellers?view=All&status=Verified&search=Seller%20Store&page=1&pageSize=10&sort=UpdatedDesc");
        allStateResponse.EnsureSuccessStatusCode();
        var allState = await allStateResponse.Content.ReadFromJsonAsync<AdminPagedResponse<AdminSellerOperationalSummaryResponse>>();
        Assert.NotNull(allState);
        var verifiedSeller = Assert.Single(allState!.Items, item => item.SellerId == seller.SellerId);
        Assert.Equal("Verified", verifiedSeller.VerificationStatus);
        Assert.Contains(allState.StatusCounts, count => count.Status == "Verified" && count.Count >= 1);
    }

    [Fact]
    public async Task AdminSellerDetail_IncludesStorePolicyReadinessContext()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            dbContext.SellerStorePolicies.Add(new SellerStorePolicy(
                seller.SellerId,
                14,
                "Returns are reviewed for delivered items in original condition.",
                "Exchanges depend on stock availability.",
                "Orders are usually dispatched within 2-3 business days.",
                "Message support with order issues and product questions.",
                "Follow product care notes on each item.",
                "Colour and fit may vary slightly by screen and size."));
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync($"/api/admin/sellers/{seller.SellerId}");

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminSellerDetailResponse>();
        Assert.NotNull(detail);
        Assert.True(detail!.StorePolicy.IsComplete);
        Assert.Equal(14, detail.StorePolicy.ReturnWindowDays);
    }

    [Fact]
    public async Task Approve_ChangesSellerToVerified_AndWritesAuditLog()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync($"/api/admin/sellers/{seller.SellerId}/approve", new { });

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminSellerDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Verified", detail!.VerificationStatus);
        Assert.True(detail.Payout?.IsAdminApproved);
        Assert.Contains(detail.AuditTrail, entry => entry.ActionType == "SellerApproved");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync());

        var sellerToken = await LoginAsync(client, "seller@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        using var notificationsResponse = await client.GetAsync("/api/seller/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        var notification = Assert.Single(notifications!, item => item.Type == SellerNotificationTypes.SellerVerificationApproved);

        using var countResponse = await client.GetAsync("/api/seller/notifications/unread-count");
        countResponse.EnsureSuccessStatusCode();
        var count = await countResponse.Content.ReadFromJsonAsync<SellerNotificationsUnreadCountResponse>();
        Assert.Equal(1, count!.UnreadCount);

        using var readResponse = await client.PostAsync($"/api/seller/notifications/{notification.NotificationId}/read", null);
        readResponse.EnsureSuccessStatusCode();
        var readNotification = await readResponse.Content.ReadFromJsonAsync<NotificationResult>();
        Assert.NotNull(readNotification!.ReadAtUtc);
    }

    [Fact]
    public async Task Seller_CanReadAndUpdateNotificationPreferences()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "preference-seller@example.test", MabuntleRoles.Seller);
        var sellerToken = await LoginAsync(client, "preference-seller@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);

        using var getResponse = await client.GetAsync("/api/seller/notification-preferences");
        getResponse.EnsureSuccessStatusCode();
        var initial = await getResponse.Content.ReadFromJsonAsync<SellerNotificationPreferencesResponse>();
        Assert.Equal(SellerNotificationCategory.All.Count, initial!.Preferences.Count);
        Assert.All(initial.Preferences, preference => Assert.True(preference.IsEnabled));
        Assert.All(initial.Preferences, preference => Assert.True(preference.EmailEnabled));

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/seller/notification-preferences",
            new SellerNotificationPreferencesRequest([
                new SellerNotificationPreferenceRequest(SellerNotificationCategory.Verification, true, true),
                new SellerNotificationPreferenceRequest(SellerNotificationCategory.Products, false, true),
                new SellerNotificationPreferenceRequest(SellerNotificationCategory.Revisions, true, false),
                new SellerNotificationPreferenceRequest(SellerNotificationCategory.Ads, true, true)
            ]));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<SellerNotificationPreferencesResponse>();
        Assert.False(updated!.Preferences.Single(preference => preference.Category == SellerNotificationCategory.Products).IsEnabled);
        Assert.False(updated.Preferences.Single(preference => preference.Category == SellerNotificationCategory.Revisions).EmailEnabled);
    }

    [Fact]
    public async Task SellerNotificationPreferences_RejectInvalidCategoriesAndNonSellers()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var forbiddenResponse = await client.GetAsync("/api/seller/notification-preferences");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        await RegisterAsync(client, "invalid-preference-seller@example.test", MabuntleRoles.Seller);
        var sellerToken = await LoginAsync(client, "invalid-preference-seller@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);

        using var invalidResponse = await client.PutAsJsonAsync(
            "/api/seller/notification-preferences",
            new SellerNotificationPreferencesRequest([
                new SellerNotificationPreferenceRequest("Unknown", true, true)
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task Reject_RequiresReason()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/sellers/{seller.SellerId}/reject",
            new AdminSellerReasonRequest(" "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reject_ChangesSellerToRejected_AndWritesAuditLog()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/sellers/{seller.SellerId}/reject",
            new AdminSellerReasonRequest("Documents are not clear."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminSellerDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Rejected", detail!.VerificationStatus);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "SellerRejected" && entry.Reason == "Documents are not clear.");

        var sellerToken = await LoginAsync(client, "seller@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        using var onboardingResponse = await client.GetAsync("/api/seller/onboarding");
        onboardingResponse.EnsureSuccessStatusCode();
        var onboarding = await onboardingResponse.Content.ReadFromJsonAsync<SellerOnboardingResponse>();
        Assert.NotNull(onboarding!.LatestVerificationReview);
        Assert.NotNull(onboarding.LatestVerificationReview!.SubmittedAtUtc);
        Assert.NotNull(onboarding.LatestVerificationReview.ReviewedAtUtc);
        Assert.Equal("Documents are not clear.", onboarding.LatestVerificationReview.RejectionReason);
        Assert.Null(onboarding.LatestVerificationReview.SuspensionReason);
    }

    [Fact]
    public async Task Suspend_RequiresReason_AndChangesSellerToSuspended()
    {
        using var factory = new AdminSellerApprovalTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterLoginAndSubmitSellerAsync(client);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var missingReasonResponse = await client.PostAsJsonAsync(
            $"/api/admin/sellers/{seller.SellerId}/suspend",
            new AdminSellerReasonRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, missingReasonResponse.StatusCode);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/sellers/{seller.SellerId}/suspend",
            new AdminSellerReasonRequest("Policy review required."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminSellerDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Suspended", detail!.VerificationStatus);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "SellerSuspended" && entry.Reason == "Policy review required.");

        var sellerToken = await LoginAsync(client, "seller@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        using var onboardingResponse = await client.GetAsync("/api/seller/onboarding");
        onboardingResponse.EnsureSuccessStatusCode();
        var onboarding = await onboardingResponse.Content.ReadFromJsonAsync<SellerOnboardingResponse>();
        Assert.NotNull(onboarding!.LatestVerificationReview);
        Assert.Equal("Policy review required.", onboarding.LatestVerificationReview!.SuspensionReason);
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "buyer@example.test";
        await RegisterAsync(client, email, MabuntleRoles.Buyer);
        return await LoginAsync(client, email);
    }

    private static async Task<SellerOnboardingResponse> RegisterLoginAndSubmitSellerAsync(HttpClient client)
    {
        const string email = "seller@example.test";
        await RegisterAsync(client, email, MabuntleRoles.Seller);
        var accessToken = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var profileResponse = await client.PutAsJsonAsync(
            "/api/seller/onboarding/profile",
            new UpdateSellerProfileRequest(
                "Seller Store",
                "seller@example.test",
                "+27110000000",
                "RegisteredBusiness",
                "Seller Trading"));
        profileResponse.EnsureSuccessStatusCode();

        using var storefrontResponse = await client.PutAsJsonAsync(
            "/api/seller/onboarding/storefront",
            new UpdateSellerStorefrontRequest(
                "Seller Store",
                "seller-store",
                "Seller storefront",
                null,
                null));
        storefrontResponse.EnsureSuccessStatusCode();

        using var addressResponse = await client.PutAsJsonAsync(
            "/api/seller/onboarding/address",
            new UpdateSellerAddressRequest(
                "1 Market Street",
                null,
                "Johannesburg",
                "Gauteng",
                "2000",
                "ZA"));
        addressResponse.EnsureSuccessStatusCode();

        using var payoutResponse = await client.PutAsJsonAsync(
            "/api/seller/onboarding/payout",
            new UpdateSellerPayoutRequest("provider-ref-123"));
        payoutResponse.EnsureSuccessStatusCode();

        using var submitResponse = await client.PostAsync("/api/seller/onboarding/submit-verification", null);
        submitResponse.EnsureSuccessStatusCode();

        var onboarding = await submitResponse.Content.ReadFromJsonAsync<SellerOnboardingResponse>();
        Assert.NotNull(onboarding);
        return onboarding!;
    }

    private static async Task RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminSellerApprovalTestFactory factory,
        HttpClient client)
    {
        const string email = "admin@example.test";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(admin, "Password123!");
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

            var roleResult = await userManager.AddToRoleAsync(admin, MabuntleRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return await LoginAsync(client, email);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed class AdminSellerApprovalTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleAdminSellerApprovalTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MabuntleDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MabuntleDbContext>>();

                services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
                services.AddDbContext<MabuntleDbContext>((serviceProvider, options) =>
                {
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
                });
            });
        }
    }
}
