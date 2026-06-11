using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Authentication;
using Mabuntle.Api.Admin;
using Mabuntle.Api.Support;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class SupportTicketTests
{
    private const string TestPassword = "Password123!";

    [Fact]
    public async Task BuyerCanCreateTicket_AndInternalNotesAreHiddenFromBuyer()
    {
        using var factory = new SupportTicketTestFactory();
        using var buyerClient = factory.CreateClient();
        using var supportClient = factory.CreateClient();
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-support@example.test", MabuntleRoles.Buyer);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            "/api/buyer/support-tickets",
            new CreateSupportTicketRequest(
                "OrderIssue",
                "Order arrived damaged",
                "The box arrived damaged.",
                null,
                null,
                null,
                null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(created);
        Assert.Equal("Open", created!.Status);

        supportClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, supportClient, "support-agent@example.test", MabuntleRoles.SupportAgent));

        using var internalNoteResponse = await supportClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.SupportTicketId}/internal-notes",
            new SupportMessageRequest("Check recent refund history before replying."));
        internalNoteResponse.EnsureSuccessStatusCode();

        using var supportMessageResponse = await supportClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.SupportTicketId}/messages",
            new SupportMessageRequest("Please upload a photo of the damage."));
        supportMessageResponse.EnsureSuccessStatusCode();
        var supportView = await supportMessageResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(supportView);
        Assert.Contains(supportView!.Messages, message => message.IsInternal);
        Assert.Equal("WaitingForCustomer", supportView.Status);

        using var supportDetailResponse = await supportClient.GetAsync($"/api/support/tickets/{created.SupportTicketId}");
        supportDetailResponse.EnsureSuccessStatusCode();
        var supportDetail = await supportDetailResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(supportDetail);
        Assert.NotNull(supportDetail!.CustomerContext?.Buyer);

        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerAuth.AccessToken);
        using var buyerViewResponse = await buyerClient.GetAsync($"/api/buyer/support-tickets/{created.SupportTicketId}");
        buyerViewResponse.EnsureSuccessStatusCode();
        var buyerView = await buyerViewResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(buyerView);
        Assert.Null(buyerView!.CustomerContext);
        Assert.DoesNotContain(buyerView.Messages, message => message.IsInternal);
        Assert.Contains(buyerView.Messages, message => message.Message == "Please upload a photo of the damage.");

        using var notificationsResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "SupportReply" && notification.RelatedEntityId == created.SupportTicketId);
    }

    [Fact]
    public async Task SellerCanCreateTicket_AndAdminCanRespond()
    {
        using var factory = new SupportTicketTestFactory();
        using var sellerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        await RegisterAndLoginAsync(sellerClient, "seller-support@example.test", MabuntleRoles.Seller);

        using var createResponse = await sellerClient.PostAsJsonAsync(
            "/api/seller/support-tickets",
            new CreateSupportTicketRequest(
                "PaymentIssue",
                "Payout question",
                "My payout is still pending.",
                null,
                null,
                null,
                null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(created);
        Assert.Equal("Seller", created!.CreatedByRole);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, adminClient, "admin-support@example.test", MabuntleRoles.Admin));

        using var response = await adminClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.SupportTicketId}/messages",
            new SupportMessageRequest("The payout is waiting for dispute clearance."));
        response.EnsureSuccessStatusCode();
        var supportView = await response.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(supportView);
        Assert.Equal("WaitingForSeller", supportView!.Status);
        Assert.Contains(supportView.Messages, message => message.SenderRole == "Admin");
    }

    [Fact]
    public async Task SupportQueue_ReturnsSlaPriorityAssignment_AndSupportsTriageActions()
    {
        using var factory = new SupportTicketTestFactory();
        using var sellerClient = factory.CreateClient();
        using var supportClient = factory.CreateClient();
        await RegisterAndLoginAsync(sellerClient, "seller-support-queue@example.test", MabuntleRoles.Seller);
        var supportToken = await CreateAndLoginUserInRoleAsync(factory, supportClient, "support-queue@example.test", MabuntleRoles.SupportAgent);
        supportClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supportToken);

        using var createResponse = await sellerClient.PostAsJsonAsync(
            "/api/seller/support-tickets",
            new CreateSupportTicketRequest(
                "TechnicalIssue",
                "Checkout is blocked",
                "The checkout submit button is disabled.",
                null,
                null,
                null,
                null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(created);

        using var claimResponse = await supportClient.PostAsync($"/api/support/tickets/{created!.SupportTicketId}/claim", null);
        claimResponse.EnsureSuccessStatusCode();
        var claimed = await claimResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(claimed);
        Assert.NotNull(claimed!.AssignedSupportUserId);

        using var triageResponse = await supportClient.PutAsJsonAsync(
            $"/api/support/tickets/{created.SupportTicketId}/triage",
            new SupportTicketTriageRequest("Urgent", "Checkout blockers are urgent."));
        triageResponse.EnsureSuccessStatusCode();
        var triaged = await triageResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(triaged);
        Assert.Equal("Urgent", triaged!.Priority);
        Assert.Contains(triaged.Messages, message => message.IsInternal && message.Message == "Checkout blockers are urgent.");

        using var escalateResponse = await supportClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.SupportTicketId}/escalate",
            new SupportTicketEscalationRequest("Payment-adjacent checkout issue."));
        escalateResponse.EnsureSuccessStatusCode();
        var escalated = await escalateResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(escalated);
        Assert.Equal("Escalated", escalated!.Status);
        Assert.Equal("Payment-adjacent checkout issue.", escalated.EscalationReason);

        using var queueResponse = await supportClient.GetAsync("/api/support/tickets/queue?priority=Urgent&assigned=Mine");
        queueResponse.EnsureSuccessStatusCode();
        var queue = await queueResponse.Content.ReadFromJsonAsync<SupportTicketQueueResponse>();
        Assert.NotNull(queue);
        var item = Assert.Single(queue!.Items);
        Assert.Equal(created.SupportTicketId, item.SupportTicketId);
        Assert.Equal("Urgent", item.Priority);
        Assert.Equal("Escalated", item.Status);
        Assert.Contains(item.SlaStatus, new[] { "OnTrack", "DueSoon" });
        Assert.NotNull(item.AssignedSupportDisplayName);

        using var summaryResponse = await supportClient.GetAsync("/api/support/tickets/summary");
        summaryResponse.EnsureSuccessStatusCode();
        var summary = await summaryResponse.Content.ReadFromJsonAsync<SupportTicketSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.OpenTicketCount);
        Assert.Equal(1, summary.EscalatedTicketCount);
        Assert.Equal(1, summary.MyOpenTicketCount);
        Assert.Equal(0, summary.UnassignedOpenTicketCount);

        using var createViewResponse = await supportClient.PostAsJsonAsync(
            "/api/support/tickets/views",
            new AdminQueueSavedViewRequest(
                "Support",
                "My urgent technical queue",
                true,
                new AdminQueueSavedViewFiltersRequest("NeedsAttention", null, "TechnicalIssue", null, null, "Mine", "Urgent", null, null, "PriorityDesc", 25)));
        createViewResponse.EnsureSuccessStatusCode();
        var savedView = await createViewResponse.Content.ReadFromJsonAsync<AdminQueueSavedViewResponse>();
        Assert.NotNull(savedView);
        Assert.True(savedView!.IsDefault);
        Assert.Equal("TechnicalIssue", savedView.Filters.Category);

        using var savedQueueResponse = await supportClient.GetAsync($"/api/support/tickets/queue?savedViewId={savedView.ViewId}");
        savedQueueResponse.EnsureSuccessStatusCode();
        var savedQueue = await savedQueueResponse.Content.ReadFromJsonAsync<SupportTicketQueueResponse>();
        Assert.NotNull(savedQueue);
        Assert.Single(savedQueue!.Items);

        using var exportResponse = await supportClient.GetAsync($"/api/support/tickets/queue/export.csv?savedViewId={savedView.ViewId}");
        exportResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", exportResponse.Content.Headers.ContentType?.MediaType);
        var csv = await exportResponse.Content.ReadAsStringAsync();
        Assert.Contains("supportTicketId", csv);
        Assert.Contains("Checkout is blocked", csv);

        using var qualityResponse = await supportClient.GetAsync("/api/support/tickets/quality-report?category=TechnicalIssue&priority=Urgent&createdByRole=Seller");
        qualityResponse.EnsureSuccessStatusCode();
        var quality = await qualityResponse.Content.ReadFromJsonAsync<SupportTicketQualityReportResponse>();
        Assert.NotNull(quality);
        Assert.Equal(1, quality!.Summary.CreatedCount);
        Assert.Equal(1, quality.Summary.EscalatedCount);
        Assert.Null(quality.Summary.AverageFirstResponseHours);
        Assert.Contains(quality.CategoryBreakdown, item => item.Key == "TechnicalIssue" && item.CreatedCount == 1);

        using var qualityExportResponse = await supportClient.GetAsync("/api/support/tickets/quality-report/export.csv?category=TechnicalIssue&priority=Urgent");
        qualityExportResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", qualityExportResponse.Content.Headers.ContentType?.MediaType);
        var qualityCsv = await qualityExportResponse.Content.ReadAsStringAsync();
        Assert.Contains("section,key,createdCount", qualityCsv);
        Assert.Contains("TechnicalIssue", qualityCsv);
    }

    [Fact]
    public async Task Claim_ReturnsConflictWhenAnotherSupportAgentOwnsTicket_UnlessAdminForcesOverride()
    {
        using var factory = new SupportTicketTestFactory();
        using var buyerClient = factory.CreateClient();
        using var firstSupportClient = factory.CreateClient();
        using var secondSupportClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        await RegisterAndLoginAsync(buyerClient, "buyer-claim@example.test", MabuntleRoles.Buyer);
        firstSupportClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, firstSupportClient, "support-one@example.test", MabuntleRoles.SupportAgent));
        secondSupportClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, secondSupportClient, "support-two@example.test", MabuntleRoles.SupportAgent));
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, adminClient, "support-admin@example.test", MabuntleRoles.Admin));

        using var createResponse = await buyerClient.PostAsJsonAsync(
            "/api/buyer/support-tickets",
            new CreateSupportTicketRequest("OrderIssue", "Order issue", "Need support.", null, null, null, null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(created);

        using var firstClaimResponse = await firstSupportClient.PostAsync($"/api/support/tickets/{created!.SupportTicketId}/claim", null);
        firstClaimResponse.EnsureSuccessStatusCode();

        using var secondClaimResponse = await secondSupportClient.PostAsync($"/api/support/tickets/{created.SupportTicketId}/claim", null);
        Assert.Equal(HttpStatusCode.Conflict, secondClaimResponse.StatusCode);

        using var adminOverrideResponse = await adminClient.PostAsync($"/api/support/tickets/{created.SupportTicketId}/claim?force=true", null);
        adminOverrideResponse.EnsureSuccessStatusCode();
        var overridden = await adminOverrideResponse.Content.ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(overridden);
        Assert.NotEqual(created.AssignedSupportUserId, overridden!.AssignedSupportUserId);
    }

    private static async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return auth;
    }

    private static async Task<string> CreateAndLoginUserInRoleAsync(
        SupportTicketTestFactory factory,
        HttpClient client,
        string email,
        string role)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, TestPassword);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed class SupportTicketTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleSupportTicketTests_{Guid.NewGuid():N}";

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
