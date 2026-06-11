using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mabuntle.Infrastructure.Identity;

public sealed class JwtTokenService(
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<TokenPair> CreateTokenPairAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var options = jwtOptions.Value;
        var now = timeProvider.GetUtcNow();
        var accessTokenExpiresAt = now.AddMinutes(options.AccessTokenMinutes);
        var refreshTokenExpiresAt = now.AddDays(options.RefreshTokenDays);
        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessTokenExpiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new TokenPair(
            new JwtSecurityTokenHandler().WriteToken(token),
            accessTokenExpiresAt,
            GenerateRefreshToken(),
            refreshTokenExpiresAt);
    }

    public static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(bytes);
    }
}
