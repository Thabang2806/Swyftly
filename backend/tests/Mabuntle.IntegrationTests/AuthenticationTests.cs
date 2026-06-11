using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Authentication;
using Mabuntle.Application.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public class AuthenticationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Buyer_CanRegisterAndLogin()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var registerResponse = await RegisterAsync(client, "buyer@example.test", MabuntleRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        Assert.Equal(MabuntleRoles.Buyer, registerResponse.Role);
        Assert.Null(registerResponse.SellerVerificationStatus);
        Assert.Contains(MabuntleRoles.Buyer, loginResponse.Roles);
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.AccessToken));
        Assert.Null(loginResponse.Auth.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.CsrfToken));
    }

    [Fact]
    public async Task Login_SetsHardenedRefreshAndCsrfCookies()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "cookie-buyer@example.test", MabuntleRoles.Buyer);
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("cookie-buyer@example.test", TestPassword));

        await EnsureSuccessAsync(response);

        var refreshCookie = GetSetCookieHeader(response, AuthEndpoints.RefreshTokenCookieName);
        var csrfCookie = GetSetCookieHeader(response, AuthEndpoints.CsrfCookieName);

        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", refreshCookie, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("httponly", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", csrfCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Seller_CanRegisterAndLogin_WithPendingVerification()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var registerResponse = await RegisterAsync(client, "seller@example.test", MabuntleRoles.Seller);
        var loginResponse = await LoginAsync(client, "seller@example.test");

        Assert.Equal(MabuntleRoles.Seller, registerResponse.Role);
        Assert.Equal("PendingVerification", registerResponse.SellerVerificationStatus);
        Assert.Contains(MabuntleRoles.Seller, loginResponse.Roles);
    }

    [Fact]
    public async Task Register_RejectsAdminRole()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("admin@example.test", TestPassword, MabuntleRoles.Admin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminPolicy_RejectsBuyer()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "buyer@example.test", MabuntleRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/policy-checks/admin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SellerPolicy_RejectsBuyer()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "buyer@example.test", MabuntleRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/policy-checks/seller");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesRefreshToken()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "buyer@example.test", MabuntleRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        var refreshResponse = await RefreshAsync(client, loginResponse);

        Assert.Contains(MabuntleRoles.Buyer, refreshResponse.Roles);
        Assert.NotEqual(loginResponse.RefreshToken, refreshResponse.RefreshToken);
        Assert.Null(refreshResponse.Auth.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshResponse.AccessToken));
    }

    [Fact]
    public async Task Refresh_RequiresCsrfHeaderWithCookie()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "csrf-buyer@example.test", MabuntleRoles.Buyer);
        var loginResponse = await LoginAsync(client, "csrf-buyer@example.test");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Cookie", $"{AuthEndpoints.RefreshTokenCookieName}={loginResponse.RefreshToken}; {AuthEndpoints.CsrfCookieName}={loginResponse.CsrfToken}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReplayedRevokedTokenRevokesReplacementFamilyOnly()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();
        const string email = "refresh-replay@example.test";

        await RegisterAsync(client, email, MabuntleRoles.Buyer);
        var firstLogin = await LoginAsync(client, email);
        var firstReplacement = await RefreshAsync(client, firstLogin);
        var secondLogin = await LoginAsync(client, email);

        using var replayResponse = await PostCookieRefreshAsync(client, firstLogin.RefreshToken, firstLogin.CsrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        using var revokedReplacementResponse = await PostCookieRefreshAsync(client, firstReplacement.RefreshToken, firstReplacement.CsrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedReplacementResponse.StatusCode);

        var secondFamilyReplacement = await RefreshAsync(client, secondLogin);
        Assert.False(string.IsNullOrWhiteSpace(secondFamilyReplacement.AccessToken));
    }

    [Fact]
    public async Task Login_LocksAccountAfterRepeatedFailedAttempts()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();
        const string email = "lockout-buyer@example.test";

        await RegisterAsync(client, email, MabuntleRoles.Buyer);

        HttpResponseMessage? lockedResponse = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(email, "wrong-password"));

            if (response.StatusCode == HttpStatusCode.Locked)
            {
                lockedResponse = response;
                break;
            }

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            response.Dispose();
        }

        if (lockedResponse is null)
        {
            lockedResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(email, "wrong-password"));
        }

        Assert.NotNull(lockedResponse);
        Assert.Equal(HttpStatusCode.Locked, lockedResponse!.StatusCode);
        lockedResponse.Dispose();

        using var correctPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));

        Assert.Equal(HttpStatusCode.Locked, correctPasswordResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "buyer@example.test", MabuntleRoles.Buyer);
        var loginResponse = await LoginAsync(client, "buyer@example.test");

        using var logoutResponse = await PostCookieLogoutAsync(client, loginResponse.RefreshToken, loginResponse.CsrfToken);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var refreshResponse = await PostCookieRefreshAsync(client, loginResponse.RefreshToken, loginResponse.CsrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private const string TestPassword = "Password123";

    private static async Task<RegisterResponse> RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));

        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<RegisterResponse>(response);
    }

    private static async Task<CookieAuthSession> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));

        await EnsureSuccessAsync(response);
        var auth = await ReadJsonAsync<AuthResponse>(response);
        return CreateCookieAuthSession(auth, response);
    }

    private static async Task<CookieAuthSession> RefreshAsync(HttpClient client, CookieAuthSession session)
    {
        using var response = await PostCookieRefreshAsync(client, session.RefreshToken, session.CsrfToken);

        await EnsureSuccessAsync(response);
        var auth = await ReadJsonAsync<AuthResponse>(response);
        return CreateCookieAuthSession(auth, response);
    }

    private static Task<HttpResponseMessage> PostCookieRefreshAsync(HttpClient client, string refreshToken, string csrfToken) =>
        SendCookieAuthRequestAsync(client, "/api/auth/refresh", refreshToken, csrfToken);

    private static Task<HttpResponseMessage> PostCookieLogoutAsync(HttpClient client, string refreshToken, string csrfToken) =>
        SendCookieAuthRequestAsync(client, "/api/auth/logout", refreshToken, csrfToken);

    private static async Task<HttpResponseMessage> SendCookieAuthRequestAsync(
        HttpClient client,
        string path,
        string refreshToken,
        string csrfToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Cookie", $"{AuthEndpoints.RefreshTokenCookieName}={refreshToken}; {AuthEndpoints.CsrfCookieName}={csrfToken}");
        request.Headers.Add(AuthEndpoints.CsrfHeaderName, csrfToken);
        return await client.SendAsync(request);
    }

    private static CookieAuthSession CreateCookieAuthSession(AuthResponse auth, HttpResponseMessage response) =>
        new(
            auth,
            GetSetCookie(response, AuthEndpoints.RefreshTokenCookieName),
            GetSetCookie(response, AuthEndpoints.CsrfCookieName));

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        var setCookieHeader = GetSetCookieHeader(response, cookieName);
        var cookie = setCookieHeader.Split(';', 2)[0];
        var separatorIndex = cookie.IndexOf('=');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException($"Response set malformed cookie '{cookieName}'.");
        }

        return cookie[(separatorIndex + 1)..];
    }

    private static string GetSetCookieHeader(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            throw new InvalidOperationException("Response did not include Set-Cookie headers.");
        }

        foreach (var setCookieHeader in setCookieHeaders)
        {
            var cookie = setCookieHeader.Split(';', 2)[0];
            var separatorIndex = cookie.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = cookie[..separatorIndex];
            if (string.Equals(name, cookieName, StringComparison.Ordinal))
            {
                return setCookieHeader;
            }
        }

        throw new InvalidOperationException($"Response did not set cookie '{cookieName}'.");
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

    private sealed record CookieAuthSession(
        AuthResponse Auth,
        string RefreshToken,
        string CsrfToken)
    {
        public IReadOnlyCollection<string> Roles => Auth.Roles;

        public string AccessToken => Auth.AccessToken;
    }

    private sealed class AuthTestFactory : WebApplicationFactory<Program>
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
