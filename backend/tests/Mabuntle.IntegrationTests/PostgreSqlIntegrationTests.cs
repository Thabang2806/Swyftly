using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;
using Mabuntle.Application.Inventory;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Inventory;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public class PostgreSqlIntegrationTests
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=mabuntle_integration_tests;Username=mabuntle;Password=mabuntle_dev_password";

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("MABUNTLE_TEST_POSTGRES_CONNECTION")
        ?? DefaultConnectionString;

    [PostgreSqlFact]
    public async Task PostgreSql_MigrationsCanBeApplied()
    {
        await ApplyMigrationsAsync();

        await using var dbContext = CreateDbContext();
        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [PostgreSqlFact]
    public async Task PostgreSql_ReadinessEndpointReturnsHealthy()
    {
        await ApplyMigrationsAsync();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = ConnectionString
                    });
                });
            });

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"postgresql\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [PostgreSqlFact]
    public async Task PostgreSql_ConcurrentReservationsCannotOversellLastUnit()
    {
        await ApplyMigrationsAsync();
        var seed = await SeedCompetingReservationCartsAsync();
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        var firstService = new EfInventoryReservationService(firstContext);
        var secondService = new EfInventoryReservationService(secondContext);
        var startedAt = DateTimeOffset.UtcNow;

        var results = await Task.WhenAll(
            firstService.ReserveCartAsync(new ReserveCartInventoryRequest(
                seed.FirstBuyerId,
                seed.FirstCartId,
                startedAt,
                TimeSpan.FromMinutes(15))),
            secondService.ReserveCartAsync(new ReserveCartInventoryRequest(
                seed.SecondBuyerId,
                seed.SecondCartId,
                startedAt,
                TimeSpan.FromMinutes(15))));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.IsFailure);

        await using var verifyContext = CreateDbContext();
        var variant = await verifyContext.ProductVariants.SingleAsync(variant => variant.Id == seed.VariantId);
        var activeReservations = await verifyContext.InventoryReservations.CountAsync(
            reservation => reservation.ProductVariantId == seed.VariantId
                && reservation.Status == InventoryReservationStatus.Active);

        Assert.Equal(1, variant.ReservedQuantity);
        Assert.Equal(1, activeReservations);
    }

    private static MabuntleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions => npgsqlOptions.UseVector())
            .Options;

        return new MabuntleDbContext(options);
    }

    private static async Task<ReservationSeed> SeedCompetingReservationCartsAsync()
    {
        await using var dbContext = CreateDbContext();
        var sellerUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"seller-{Guid.NewGuid():N}@example.test",
            Email = $"seller-{Guid.NewGuid():N}@example.test",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var firstBuyerUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"buyer-one-{Guid.NewGuid():N}@example.test",
            Email = $"buyer-one-{Guid.NewGuid():N}@example.test",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var secondBuyerUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"buyer-two-{Guid.NewGuid():N}@example.test",
            Email = $"buyer-two-{Guid.NewGuid():N}@example.test",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var seller = new SellerProfile(sellerUser.Id);
        var firstBuyer = new BuyerProfile(firstBuyerUser.Id);
        var secondBuyer = new BuyerProfile(secondBuyerUser.Id);
        var product = new Product(seller.Id);
        var variant = new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            499m,
            null,
            stockQuantity: 1);
        var firstCart = new Cart(firstBuyer.Id);
        firstCart.AddOrUpdateItem(
            product.Id,
            variant.Id,
            seller.Id,
            "Concurrent reservation product",
            variant.Sku,
            variant.Size,
            variant.Colour,
            variant.Price,
            quantity: 1,
            availableQuantity: variant.AvailableQuantity);
        var secondCart = new Cart(secondBuyer.Id);
        secondCart.AddOrUpdateItem(
            product.Id,
            variant.Id,
            seller.Id,
            "Concurrent reservation product",
            variant.Sku,
            variant.Size,
            variant.Colour,
            variant.Price,
            quantity: 1,
            availableQuantity: variant.AvailableQuantity);

        dbContext.Users.AddRange(sellerUser, firstBuyerUser, secondBuyerUser);
        dbContext.SellerProfiles.Add(seller);
        dbContext.BuyerProfiles.AddRange(firstBuyer, secondBuyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.AddRange(firstCart, secondCart);
        await dbContext.SaveChangesAsync();

        return new ReservationSeed(firstBuyer.Id, firstCart.Id, secondBuyer.Id, secondCart.Id, variant.Id);
    }

    private static async Task ApplyMigrationsAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    private sealed record ReservationSeed(
        Guid FirstBuyerId,
        Guid FirstCartId,
        Guid SecondBuyerId,
        Guid SecondCartId,
        Guid VariantId);
}
