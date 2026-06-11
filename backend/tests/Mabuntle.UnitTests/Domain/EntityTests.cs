using Mabuntle.Domain.Common;

namespace Mabuntle.UnitTests.Domain;

public class EntityTests
{
    [Fact]
    public void DomainEvents_CanBeAddedAndCleared()
    {
        var entity = new TestEntity();
        var domainEvent = new TestDomainEvent(DateTimeOffset.UtcNow);

        entity.Raise(domainEvent);

        Assert.Single(entity.DomainEvents);
        Assert.Contains(domainEvent, entity.DomainEvents);

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }

    private sealed class TestEntity : Entity
    {
        public void Raise(IDomainEvent domainEvent)
        {
            AddDomainEvent(domainEvent);
        }
    }

    private sealed record TestDomainEvent(DateTimeOffset OccurredAtUtc) : DomainEvent(OccurredAtUtc);
}
