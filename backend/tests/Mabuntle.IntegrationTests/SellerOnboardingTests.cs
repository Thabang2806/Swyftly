using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
using Mabuntle.Application.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public class SellerOnboardingTests
{
    private const string TestPassword = "Password123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Buyer_CannotAccessSellerOnboarding()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var auth = await RegisterAndLoginAsync(client, "buyer@example.test", MabuntleRoles.Buyer);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/seller/onboarding");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CanUpdateProfileAndReadOnlyTheirOnboardingState()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var sellerOne = await RegisterAndLoginAsync(client, "seller-one@example.test", MabuntleRoles.Seller);
        var sellerTwo = await RegisterAndLoginAsync(client, "seller-two@example.test", MabuntleRoles.Seller);

        await PutAsSellerAsync(
            client,
            sellerOne.AccessToken,
            "/api/seller/onboarding/profile",
            new UpdateSellerProfileRequest(
                "Seller One",
                "seller-one@example.test",
                "+27110000001",
                "Individual",
                null));

        var sellerOneState = await GetOnboardingAsync(client, sellerOne.AccessToken);
        var sellerTwoState = await GetOnboardingAsync(client, sellerTwo.AccessToken);

        Assert.Equal("Seller One", sellerOneState.Profile.DisplayName);
        Assert.Null(sellerTwoState.Profile.DisplayName);
    }

    [Fact]
    public async Task SubmitVerification_RejectsIncompleteOnboarding()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var seller = await RegisterAndLoginAsync(client, "seller@example.test", MabuntleRoles.Seller);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/seller/onboarding/submit-verification");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Complete seller profile", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StorefrontSlug_MustBeUnique()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var sellerOne = await RegisterAndLoginAsync(client, "seller-one@example.test", MabuntleRoles.Seller);
        var sellerTwo = await RegisterAndLoginAsync(client, "seller-two@example.test", MabuntleRoles.Seller);

        await PutAsSellerAsync(
            client,
            sellerOne.AccessToken,
            "/api/seller/onboarding/storefront",
            new UpdateSellerStorefrontRequest("Seller One", "shared-slug", null, null, null));

        using var response = await PutAsSellerAsync(
            client,
            sellerTwo.AccessToken,
            "/api/seller/onboarding/storefront",
            new UpdateSellerStorefrontRequest("Seller Two", "shared-slug", null, null, null),
            ensureSuccess: false);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CompleteSeller_CanSubmitForVerification()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var seller = await RegisterAndLoginAsync(client, "seller@example.test", MabuntleRoles.Seller);
        await CompleteRequiredOnboardingAsync(client, seller.AccessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/seller/onboarding/submit-verification");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        using var response = await client.SendAsync(request);

        await EnsureSuccessAsync(response);
        var onboarding = await ReadJsonAsync<SellerOnboardingResponse>(response);

        Assert.Equal("UnderReview", onboarding.VerificationStatus);
        Assert.True(onboarding.CanSubmitForVerification);
        Assert.NotNull(onboarding.LatestVerificationReview);
        Assert.NotNull(onboarding.LatestVerificationReview!.SubmittedAtUtc);
        Assert.Null(onboarding.LatestVerificationReview.ReviewedAtUtc);
        Assert.Null(onboarding.LatestVerificationReview.RejectionReason);
        Assert.Null(onboarding.LatestVerificationReview.SuspensionReason);
    }

    [Fact]
    public async Task Seller_CanCreateUpdateAndReadStorePolicy()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var seller = await RegisterAndLoginAsync(client, "seller-policy@example.test", MabuntleRoles.Seller);

        using var updateResponse = await PutAsSellerAsync(
            client,
            seller.AccessToken,
            "/api/seller/store-policy",
            new SellerStorePolicyRequest(
                14,
                "Returns are reviewed for delivered items in original condition.",
                "Exchanges depend on stock availability.",
                "Orders are usually dispatched within 2-3 business days.",
                "Message support with order issues and product questions.",
                "Follow product care notes on each item.",
                "Colour and fit may vary slightly by screen and size."));

        var updated = await ReadJsonAsync<SellerPolicyResponse>(updateResponse);
        Assert.True(updated.IsComplete);
        Assert.Empty(updated.MissingFields);
        Assert.Equal(14, updated.ReturnWindowDays);

        var fetched = await GetSellerStorePolicyAsync(client, seller.AccessToken);
        Assert.Equal("Returns are reviewed for delivered items in original condition.", fetched.ReturnPolicy);
        Assert.Equal("Exchanges depend on stock availability.", fetched.ExchangePolicy);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "SellerStorePolicyCreated"));
    }

    [Fact]
    public async Task SellerStorePolicy_RejectsInvalidDataAndNonSellerAccess()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var seller = await RegisterAndLoginAsync(client, "seller-policy-invalid@example.test", MabuntleRoles.Seller);
        var buyer = await RegisterAndLoginAsync(client, "buyer-policy-invalid@example.test", MabuntleRoles.Buyer);

        using var invalidResponse = await PutAsSellerAsync(
            client,
            seller.AccessToken,
            "/api/seller/store-policy",
            new SellerStorePolicyRequest(-1, null, null, null, null, null, null),
            ensureSuccess: false);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        using var buyerResponse = await PutAsSellerAsync(
            client,
            buyer.AccessToken,
            "/api/seller/store-policy",
            new SellerStorePolicyRequest(14, null, null, null, null, null, null),
            ensureSuccess: false);
        Assert.Equal(HttpStatusCode.Forbidden, buyerResponse.StatusCode);
    }

    [Fact]
    public async Task Seller_CanUploadDownloadAndRemoveVerificationEvidence()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var seller = await RegisterAndLoginAsync(client, "seller-evidence@example.test", MabuntleRoles.Seller);

        using var uploadResponse = await UploadEvidenceAsync(client, seller.AccessToken);
        var uploaded = await ReadJsonAsync<SellerVerificationEvidenceResponse>(uploadResponse);

        Assert.Equal("BusinessRegistration", uploaded.EvidenceType);
        Assert.Equal("registration.pdf", uploaded.OriginalFileName);
        Assert.Equal("application/pdf", uploaded.ContentType);
        Assert.Null(uploaded.RemovedAtUtc);

        var listed = await GetEvidenceAsync(client, seller.AccessToken);
        var listedItem = Assert.Single(listed);
        Assert.Equal(uploaded.EvidenceId, listedItem.EvidenceId);

        using var downloadResponse = await GetAsSellerAsync(
            client,
            seller.AccessToken,
            $"/api/seller/verification-evidence/{uploaded.EvidenceId}/download");
        await EnsureSuccessAsync(downloadResponse);
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);

        using var removeResponse = await DeleteAsSellerAsync(
            client,
            seller.AccessToken,
            $"/api/seller/verification-evidence/{uploaded.EvidenceId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        Assert.Empty(await GetEvidenceAsync(client, seller.AccessToken));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "SellerVerificationEvidenceUploaded"));
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "SellerVerificationEvidenceRemoved"));
    }

    [Fact]
    public async Task VerificationEvidence_IsSellerScopedAndValidated()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var sellerOne = await RegisterAndLoginAsync(client, "seller-evidence-one@example.test", MabuntleRoles.Seller);
        var sellerTwo = await RegisterAndLoginAsync(client, "seller-evidence-two@example.test", MabuntleRoles.Seller);
        var buyer = await RegisterAndLoginAsync(client, "buyer-evidence@example.test", MabuntleRoles.Buyer);

        using var uploadResponse = await UploadEvidenceAsync(client, sellerOne.AccessToken);
        var uploaded = await ReadJsonAsync<SellerVerificationEvidenceResponse>(uploadResponse);

        using var otherSellerResponse = await GetAsSellerAsync(
            client,
            sellerTwo.AccessToken,
            $"/api/seller/verification-evidence/{uploaded.EvidenceId}/download",
            ensureSuccess: false);
        Assert.Equal(HttpStatusCode.NotFound, otherSellerResponse.StatusCode);

        using var buyerResponse = await GetAsSellerAsync(
            client,
            buyer.AccessToken,
            "/api/seller/verification-evidence",
            ensureSuccess: false);
        Assert.Equal(HttpStatusCode.Forbidden, buyerResponse.StatusCode);

        using var invalidResponse = await UploadEvidenceAsync(
            client,
            sellerOne.AccessToken,
            content: "not a pdf"u8.ToArray(),
            contentType: "application/pdf",
            ensureSuccess: false);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task AdminSellerDetail_IncludesEvidenceAndAdminCanDownload()
    {
        await using var factory = new SellerOnboardingTestFactory();
        using var client = factory.CreateClient();

        var seller = await RegisterAndLoginAsync(client, "seller-admin-evidence@example.test", MabuntleRoles.Seller);
        var adminAccessToken = await CreateAndLoginAdminAsync(factory, client, "admin-evidence@example.test");
        var onboarding = await GetOnboardingAsync(client, seller.AccessToken);

        using var uploadResponse = await UploadEvidenceAsync(
            client,
            seller.AccessToken,
            evidenceType: "FulfilmentAddress",
            note: "Lease document for fulfilment address.");
        var uploaded = await ReadJsonAsync<SellerVerificationEvidenceResponse>(uploadResponse);

        using var detailRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/sellers/{onboarding.SellerId}");
        detailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);
        using var detailResponse = await client.SendAsync(detailRequest);
        await EnsureSuccessAsync(detailResponse);
        var detail = await ReadJsonAsync<AdminSellerDetailResponse>(detailResponse);

        var evidence = Assert.Single(detail.VerificationEvidence);
        Assert.Equal(uploaded.EvidenceId, evidence.EvidenceId);
        Assert.Equal("FulfilmentAddress", evidence.EvidenceType);

        using var downloadRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/sellers/{onboarding.SellerId}/verification-evidence/{uploaded.EvidenceId}/download");
        downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);
        using var downloadResponse = await client.SendAsync(downloadRequest);
        await EnsureSuccessAsync(downloadResponse);
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "SellerVerificationEvidenceDownloaded"));
    }

    private static async Task CompleteRequiredOnboardingAsync(HttpClient client, string accessToken)
    {
        await PutAsSellerAsync(
            client,
            accessToken,
            "/api/seller/onboarding/profile",
            new UpdateSellerProfileRequest(
                "Seller Store",
                "seller@example.test",
                "+27110000000",
                "RegisteredBusiness",
                "Seller Trading"));

        await PutAsSellerAsync(
            client,
            accessToken,
            "/api/seller/onboarding/storefront",
            new UpdateSellerStorefrontRequest(
                "Seller Store",
                "seller-store",
                "Seller storefront",
                null,
                null));

        await PutAsSellerAsync(
            client,
            accessToken,
            "/api/seller/onboarding/address",
            new UpdateSellerAddressRequest(
                "1 Market Street",
                null,
                "Johannesburg",
                "Gauteng",
                "2000",
                "ZA"));

        await PutAsSellerAsync(
            client,
            accessToken,
            "/api/seller/onboarding/payout",
            new UpdateSellerPayoutRequest("provider-ref-123"));
    }

    private static async Task<SellerOnboardingResponse> GetOnboardingAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/seller/onboarding");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request);

        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<SellerOnboardingResponse>(response);
    }

    private static async Task<SellerPolicyResponse> GetSellerStorePolicyAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/seller/store-policy");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request);

        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<SellerPolicyResponse>(response);
    }

    private static async Task<IReadOnlyCollection<SellerVerificationEvidenceResponse>> GetEvidenceAsync(
        HttpClient client,
        string accessToken)
    {
        using var response = await GetAsSellerAsync(client, accessToken, "/api/seller/verification-evidence");
        return await ReadJsonAsync<IReadOnlyCollection<SellerVerificationEvidenceResponse>>(response);
    }

    private static async Task<HttpResponseMessage> UploadEvidenceAsync(
        HttpClient client,
        string accessToken,
        string evidenceType = "BusinessRegistration",
        string? note = "CIPC registration document",
        byte[]? content = null,
        string contentType = "application/pdf",
        bool ensureSuccess = true)
    {
        var bytes = content ?? "%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray();
        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(file, "file", "registration.pdf");
        multipart.Add(new StringContent(evidenceType), "evidenceType");
        if (note is not null)
        {
            multipart.Add(new StringContent(note), "note");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/seller/verification-evidence/upload")
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        if (ensureSuccess)
        {
            await EnsureSuccessAsync(response);
        }

        return response;
    }

    private static async Task<HttpResponseMessage> GetAsSellerAsync(
        HttpClient client,
        string accessToken,
        string uri,
        bool ensureSuccess = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        if (ensureSuccess)
        {
            await EnsureSuccessAsync(response);
        }

        return response;
    }

    private static async Task<HttpResponseMessage> DeleteAsSellerAsync(
        HttpClient client,
        string accessToken,
        string uri,
        bool ensureSuccess = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        if (ensureSuccess)
        {
            await EnsureSuccessAsync(response);
        }

        return response;
    }

    private static async Task<HttpResponseMessage> PutAsSellerAsync<T>(
        HttpClient client,
        string accessToken,
        string uri,
        T requestBody,
        bool ensureSuccess = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);

        if (ensureSuccess)
        {
            await EnsureSuccessAsync(response);
        }

        return response;
    }

    private static async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));
        await EnsureSuccessAsync(registerResponse);

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));
        await EnsureSuccessAsync(loginResponse);

        return await ReadJsonAsync<AuthResponse>(loginResponse);
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        SellerOnboardingTestFactory factory,
        HttpClient client,
        string email)
    {
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

            var createResult = await userManager.CreateAsync(admin, TestPassword);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

            var roleResult = await userManager.AddToRoleAsync(admin, MabuntleRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));
        await EnsureSuccessAsync(loginResponse);

        return (await ReadJsonAsync<AuthResponse>(loginResponse)).AccessToken;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {content}");
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Response body could not be deserialized as {typeof(T).Name}.");
    }

    private sealed class SellerOnboardingTestFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MabuntleDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MabuntleDbContext>>();
                services.AddDbContext<MabuntleDbContext>((serviceProvider, options) =>
                {
                    options
                        .UseInMemoryDatabase(databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
                });
            });
        }
    }
}
