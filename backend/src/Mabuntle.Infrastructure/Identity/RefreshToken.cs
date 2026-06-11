namespace Mabuntle.Infrastructure.Identity;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc,
        Guid? familyId = null)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        FamilyId = familyId ?? Guid.NewGuid();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid UserId { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    public string TokenHash { get; private set; } = string.Empty;

    public Guid FamilyId { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public string? RevokedReason { get; private set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsExpired(DateTimeOffset utcNow) => ExpiresAtUtc <= utcNow;

    public bool IsActive(DateTimeOffset utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTimeOffset revokedAtUtc, string? replacedByTokenHash = null, string? revokedReason = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAtUtc = revokedAtUtc;
        ReplacedByTokenHash = replacedByTokenHash;
        RevokedReason = string.IsNullOrWhiteSpace(revokedReason)
            ? null
            : revokedReason.Trim();
    }
}
