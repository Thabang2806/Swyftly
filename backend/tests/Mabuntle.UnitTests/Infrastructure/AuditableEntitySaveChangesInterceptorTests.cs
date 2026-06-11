using Microsoft.EntityFrameworkCore;
using Mabuntle.Domain.Common;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public class AuditableEntitySaveChangesInterceptorTests
{
    [Fact]
    public async Task SaveChangesAsync_SetsCreatedAndUpdatedTimestampsForAddedEntities()
    {
        var timestamp = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateDbContext(timestamp);

        var entity = new TestAuditableEntity { Name = "Initial" };

        dbContext.TestEntities.Add(entity);
        await dbContext.SaveChangesAsync();

        Assert.Equal(timestamp, entity.CreatedAtUtc);
        Assert.Equal(timestamp, entity.UpdatedAtUtc);
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesOnlyUpdatedTimestampForModifiedEntities()
    {
        var createdAt = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(5);
        var databaseName = Guid.NewGuid().ToString("N");

        await using (var createContext = CreateDbContext(createdAt, databaseName))
        {
            createContext.TestEntities.Add(new TestAuditableEntity { IdOverride = TestAuditableEntityId, Name = "Initial" });
            await createContext.SaveChangesAsync();
        }

        await using var updateContext = CreateDbContext(updatedAt, databaseName);
        var entity = await updateContext.TestEntities.SingleAsync(entity => entity.Id == TestAuditableEntityId);

        entity.Name = "Updated";
        await updateContext.SaveChangesAsync();

        Assert.Equal(createdAt, entity.CreatedAtUtc);
        Assert.Equal(updatedAt, entity.UpdatedAtUtc);
    }

    private static readonly Guid TestAuditableEntityId = Guid.Parse("9E9EF50D-683D-47CE-BA2F-31311793399F");

    private static TestDbContext CreateDbContext(DateTimeOffset utcNow, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;

        var interceptor = new AuditableEntitySaveChangesInterceptor(new FixedTimeProvider(utcNow));

        return new TestDbContext(options, interceptor);
    }

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options,
        AuditableEntitySaveChangesInterceptor interceptor) : DbContext(options)
    {
        public DbSet<TestAuditableEntity> TestEntities => Set<TestAuditableEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
    }

    private sealed class TestAuditableEntity : AuditableEntity
    {
        public Guid IdOverride
        {
            set => Id = value;
        }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
