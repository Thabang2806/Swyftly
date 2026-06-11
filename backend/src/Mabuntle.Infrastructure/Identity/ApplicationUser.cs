using Microsoft.AspNetCore.Identity;

namespace Mabuntle.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
