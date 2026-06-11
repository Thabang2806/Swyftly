namespace Mabuntle.Domain.Common;

public abstract record DomainEvent(DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    protected DomainEvent()
        : this(DateTimeOffset.UtcNow)
    {
    }
}
