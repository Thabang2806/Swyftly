using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mabuntle.Api.Security;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Authentication;

public static class AuthEndpoints
{
    public const string RefreshTokenCookieName = "mabuntle_rt";
    public const string CsrfCookieName = "mabuntle_csrf";
    public const string CsrfHeaderName = "X-Mabuntle-CSRF";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Registers a public buyer or seller account.")
            .RequireRateLimiting(MabuntleRateLimitPolicies.Auth)
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Authenticates a user, returns a JWT access token, and sets HttpOnly refresh cookies.")
            .RequireRateLimiting(MabuntleRateLimitPolicies.Auth)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken")
            .WithSummary("Rotates a valid refresh cookie and returns a new access token.")
            .RequireRateLimiting(MabuntleRateLimitPolicies.Auth)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Revokes the refresh cookie token.")
            .RequireRateLimiting(MabuntleRateLimitPolicies.Auth)
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", CurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Returns the current authenticated user.")
            .RequireAuthorization()
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/policy-checks/admin", () => HttpResults.Ok(new PolicyCheckResponse(MabuntlePolicies.AdminOnly)))
            .WithName("CheckAdminPolicy")
            .WithSummary("Verifies the admin-only authorization policy.")
            .RequireAuthorization(MabuntlePolicies.AdminOnly)
            .Produces<PolicyCheckResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/policy-checks/seller", () => HttpResults.Ok(new PolicyCheckResponse(MabuntlePolicies.SellerOnly)))
            .WithName("CheckSellerPolicy")
            .WithSummary("Verifies the seller-only authorization policy.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces<PolicyCheckResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var role = NormalizePublicRegistrationRole(request.Role);

        if (role is null)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Identity.InvalidRegistrationRole",
                "Public registration is limited to Buyer and Seller roles.");
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Problem(
                StatusCodes.Status409Conflict,
                "Identity.EmailAlreadyRegistered",
                "An account already exists for this email address.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            LockoutEnabled = true,
            CreatedAtUtc = now
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return IdentityProblem(createResult, StatusCodes.Status400BadRequest, "Identity.RegistrationFailed");
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            return IdentityProblem(roleResult, StatusCodes.Status400BadRequest, "Identity.RoleAssignmentFailed");
        }

        SellerVerificationStatus? sellerVerificationStatus = null;
        if (string.Equals(role, MabuntleRoles.Buyer, StringComparison.Ordinal))
        {
            dbContext.BuyerProfiles.Add(new BuyerProfile(user.Id));
        }
        else
        {
            var sellerProfile = new SellerProfile(user.Id);
            sellerVerificationStatus = sellerProfile.VerificationStatus;
            dbContext.SellerProfiles.Add(sellerProfile);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = new RegisterResponse(
            user.Id,
            email,
            role,
            sellerVerificationStatus?.ToString(),
            EmailVerificationRequired: false);

        return HttpResults.Created($"/api/auth/users/{user.Id}", response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        IOptions<AuthCookieOptions> authCookieOptions,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidCredentials",
                "The email address or password is incorrect.");
        }

        if (!await userManager.GetLockoutEnabledAsync(user))
        {
            await userManager.SetLockoutEnabledAsync(user, true);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Problem(
                StatusCodes.Status423Locked,
                "Identity.AccountLocked",
                "The account is temporarily locked after too many failed login attempts.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
            {
                return Problem(
                    StatusCodes.Status423Locked,
                    "Identity.AccountLocked",
                    "The account is temporarily locked after too many failed login attempts.");
            }

            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidCredentials",
                "The email address or password is incorrect.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAtUtc = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);

        var response = await CreateAuthResponseAsync(
            user,
            userManager,
            jwtTokenService,
            dbContext,
            timeProvider,
            httpContext,
            authCookieOptions.Value,
            cancellationToken);

        return HttpResults.Ok(response);
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<AuthCookieOptions> authCookieOptions,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!TryValidateCsrf(httpContext))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidCsrfToken",
                "The refresh request is missing a valid CSRF token.");
        }

        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshTokenValue)
            || string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            ClearAuthCookies(httpContext, authCookieOptions.Value);
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidRefreshToken",
                "The refresh token is invalid or expired.");
        }

        var tokenHash = JwtTokenService.HashRefreshToken(refreshTokenValue);
        var refreshToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            ClearAuthCookies(httpContext, authCookieOptions.Value);
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidRefreshToken",
                "The refresh token is invalid or expired.");
        }

        if (refreshToken.IsRevoked)
        {
            await RevokeRefreshTokenFamilyAsync(
                refreshToken.UserId,
                refreshToken.FamilyId,
                now,
                "ReplayDetected",
                dbContext,
                cancellationToken);

            ClearAuthCookies(httpContext, authCookieOptions.Value);
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidRefreshToken",
                "The refresh token is invalid or expired.");
        }

        if (refreshToken.IsExpired(now))
        {
            ClearAuthCookies(httpContext, authCookieOptions.Value);
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidRefreshToken",
                "The refresh token is invalid or expired.");
        }

        var tokenPair = await jwtTokenService.CreateTokenPairAsync(refreshToken.User, cancellationToken);
        var replacementHash = JwtTokenService.HashRefreshToken(tokenPair.RefreshToken);
        refreshToken.Revoke(now, replacementHash, "Rotated");
        dbContext.RefreshTokens.Add(new RefreshToken(
            refreshToken.UserId,
            replacementHash,
            tokenPair.RefreshTokenExpiresAtUtc,
            now,
            refreshToken.FamilyId));

        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(refreshToken.User);
        SetAuthCookies(httpContext, tokenPair.RefreshToken, tokenPair.RefreshTokenExpiresAtUtc, authCookieOptions.Value);
        return HttpResults.Ok(ToAuthResponse(refreshToken.User, roles.ToArray(), tokenPair));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<AuthCookieOptions> authCookieOptions,
        CancellationToken cancellationToken)
    {
        if (!TryValidateCsrf(httpContext))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidCsrfToken",
                "The logout request is missing a valid CSRF token.");
        }

        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshTokenValue)
            || string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            ClearAuthCookies(httpContext, authCookieOptions.Value);
            return HttpResults.NoContent();
        }

        var tokenHash = JwtTokenService.HashRefreshToken(refreshTokenValue);
        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is not null && refreshToken.IsActive(timeProvider.GetUtcNow()))
        {
            refreshToken.Revoke(timeProvider.GetUtcNow(), revokedReason: "Logout");
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        ClearAuthCookies(httpContext, authCookieOptions.Value);
        return HttpResults.NoContent();
    }

    private static async Task<IResult> CurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.InvalidToken",
                "The access token does not identify a valid user.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Identity.UserNotFound",
                "The authenticated user no longer exists.");
        }

        var roles = await userManager.GetRolesAsync(user);
        return HttpResults.Ok(new CurrentUserResponse(user.Id, user.Email ?? string.Empty, roles.ToArray()));
    }

    private static async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        HttpContext httpContext,
        AuthCookieOptions authCookieOptions,
        CancellationToken cancellationToken)
    {
        var tokenPair = await jwtTokenService.CreateTokenPairAsync(user, cancellationToken);
        dbContext.RefreshTokens.Add(new RefreshToken(
            user.Id,
            JwtTokenService.HashRefreshToken(tokenPair.RefreshToken),
            tokenPair.RefreshTokenExpiresAtUtc,
            timeProvider.GetUtcNow()));

        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        SetAuthCookies(httpContext, tokenPair.RefreshToken, tokenPair.RefreshTokenExpiresAtUtc, authCookieOptions);
        return ToAuthResponse(user, roles.ToArray(), tokenPair);
    }

    private static async Task RevokeRefreshTokenFamilyAsync(
        Guid userId,
        Guid familyId,
        DateTimeOffset revokedAtUtc,
        string revokedReason,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var activeFamilyTokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId
                && token.FamilyId == familyId
                && token.RevokedAtUtc == null
                && token.ExpiresAtUtc > revokedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var token in activeFamilyTokens)
        {
            token.Revoke(revokedAtUtc, revokedReason: revokedReason);
        }

        if (activeFamilyTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static AuthResponse ToAuthResponse(
        ApplicationUser user,
        IReadOnlyCollection<string> roles,
        TokenPair tokenPair)
    {
        return new AuthResponse(
            user.Id,
            user.Email ?? string.Empty,
            roles,
            tokenPair.AccessToken,
            tokenPair.AccessTokenExpiresAtUtc);
    }

    private static bool TryValidateCsrf(HttpContext httpContext)
    {
        var hasCookie = httpContext.Request.Cookies.TryGetValue(CsrfCookieName, out var cookieValue);
        var headerValue = httpContext.Request.Headers[CsrfHeaderName].ToString();

        return hasCookie
            && !string.IsNullOrWhiteSpace(cookieValue)
            && string.Equals(cookieValue, headerValue, StringComparison.Ordinal);
    }

    private static void SetAuthCookies(
        HttpContext httpContext,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAtUtc,
        AuthCookieOptions authCookieOptions)
    {
        httpContext.Response.Cookies.Append(
            RefreshTokenCookieName,
            refreshToken,
            CreateCookieOptions(authCookieOptions, true, authCookieOptions.RefreshTokenPath, refreshTokenExpiresAtUtc));

        httpContext.Response.Cookies.Append(
            CsrfCookieName,
            GenerateToken(),
            CreateCookieOptions(authCookieOptions, false, authCookieOptions.CsrfPath, refreshTokenExpiresAtUtc));
    }

    private static void ClearAuthCookies(HttpContext httpContext, AuthCookieOptions authCookieOptions)
    {
        var expired = DateTimeOffset.UnixEpoch;
        httpContext.Response.Cookies.Append(
            RefreshTokenCookieName,
            string.Empty,
            CreateCookieOptions(authCookieOptions, true, authCookieOptions.RefreshTokenPath, expired));

        httpContext.Response.Cookies.Append(
            CsrfCookieName,
            string.Empty,
            CreateCookieOptions(authCookieOptions, false, authCookieOptions.CsrfPath, expired));
    }

    private static CookieOptions CreateCookieOptions(
        AuthCookieOptions authCookieOptions,
        bool httpOnly,
        string path,
        DateTimeOffset expires)
    {
        var options = new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = authCookieOptions.Secure,
            SameSite = authCookieOptions.SameSite,
            Path = string.IsNullOrWhiteSpace(path) ? "/" : path,
            Expires = expires
        };

        if (!string.IsNullOrWhiteSpace(authCookieOptions.Domain))
        {
            options.Domain = authCookieOptions.Domain.Trim();
        }

        return options;
    }

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string? NormalizePublicRegistrationRole(string role)
    {
        return MabuntleRoles.PublicRegistrationRoles
            .FirstOrDefault(publicRole => string.Equals(publicRole, role.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IResult IdentityProblem(IdentityResult result, int statusCode, string title)
    {
        return HttpResults.ValidationProblem(
            result.Errors.ToDictionary(
                error => error.Code,
                error => new[] { error.Description }),
            title: title,
            statusCode: statusCode);
    }

    private static IResult Problem(int statusCode, string title, string detail)
    {
        return HttpResults.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode);
    }
}

public sealed record RegisterRequest(
    string Email,
    string Password,
    string Role);

public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string Role,
    string? SellerVerificationStatus,
    bool EmailVerificationRequired);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshTokenRequest(
    string? RefreshToken = null);

public sealed record LogoutRequest(
    string? RefreshToken = null);

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    IReadOnlyCollection<string> Roles,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)] string? RefreshToken = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)] DateTimeOffset? RefreshTokenExpiresAtUtc = null);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    IReadOnlyCollection<string> Roles);

public sealed record PolicyCheckResponse(string Policy);
