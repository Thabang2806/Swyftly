namespace Mabuntle.Domain.Common;

public interface ISoftDelete
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAtUtc { get; set; }
}
