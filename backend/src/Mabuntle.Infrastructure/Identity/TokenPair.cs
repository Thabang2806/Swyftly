namespace Mabuntle.Infrastructure.Identity;

public sealed record TokenPair(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
