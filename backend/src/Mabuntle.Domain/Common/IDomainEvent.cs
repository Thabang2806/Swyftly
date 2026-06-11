namespace Mabuntle.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
