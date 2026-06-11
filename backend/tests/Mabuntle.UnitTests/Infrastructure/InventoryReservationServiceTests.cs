using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mabuntle.Application.Inventory;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Infrastructure.Inventory;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public class InventoryReservationServiceTests
{
    [Fact]
    public async Task ReserveCartAsync_CreatesReservationsAndIncrementsVariantReservedQuantity()
    {
        await using var dbContext = CreateDbContext();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", 499m, 599m, 5);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(product.Id, variant.Id, product.SellerId, "Cotton Dress", variant.Sku, variant.Size, variant.Colour, variant.Price, 2, variant.AvailableQuantity);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
        var startedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var service = new EfInventoryReservationService(dbContext);

        var result = await service.ReserveCartAsync(new ReserveCartInventoryRequest(
            buyer.Id,
            cart.Id,
            startedAt,
            TimeSpan.FromMinutes(15)));

        Assert.True(result.IsSuccess);
        var reservation = Assert.Single(result.Value);
        Assert.Equal(variant.Id, reservation.ProductVariantId);
        Assert.Equal("Active", reservation.Status);
        Assert.Equal(startedAt.AddMinutes(15), reservation.ExpiresAtUtc);
        Assert.Equal(2, variant.ReservedQuantity);
        Assert.Equal(3, variant.AvailableQuantity);
        var movement = await dbContext.InventoryMovements.SingleAsync();
        Assert.Equal(InventoryMovementType.ReservationCreated, movement.MovementType);
        Assert.Equal(0, movement.ReservedQuantityBefore);
        Assert.Equal(2, movement.ReservedQuantityAfter);
        Assert.Equal(cart.Id, movement.CartId);
        Assert.Equal(reservation.ReservationId, movement.ReservationId);
    }

    [Fact]
    public async Task ReserveCartAsync_ReplacingActiveCartReservationWritesReleaseAndCreateMovements()
    {
        await using var dbContext = CreateDbContext();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", 499m, 599m, 5);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(product.Id, variant.Id, product.SellerId, "Cotton Dress", variant.Sku, variant.Size, variant.Colour, variant.Price, 2, variant.AvailableQuantity);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
        var service = new EfInventoryReservationService(dbContext);
        var startedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        await service.ReserveCartAsync(new ReserveCartInventoryRequest(buyer.Id, cart.Id, startedAt, TimeSpan.FromMinutes(15)));

        var secondResult = await service.ReserveCartAsync(new ReserveCartInventoryRequest(
            buyer.Id,
            cart.Id,
            startedAt.AddMinutes(5),
            TimeSpan.FromMinutes(15)));

        Assert.True(secondResult.IsSuccess);
        Assert.Equal(2, variant.ReservedQuantity);
        Assert.Contains(await dbContext.InventoryMovements.ToArrayAsync(), movement => movement.MovementType == InventoryMovementType.ReservationReleased);
        Assert.Equal(2, await dbContext.InventoryMovements.CountAsync(movement => movement.MovementType == InventoryMovementType.ReservationCreated));
    }

    [Fact]
    public async Task ReserveCartAsync_ReturnsValidationFailureWhenStockIsInsufficient()
    {
        await using var dbContext = CreateDbContext();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", 499m, 599m, stockQuantity: 5, reservedQuantity: 3);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(product.Id, variant.Id, product.SellerId, "Cotton Dress", variant.Sku, variant.Size, variant.Colour, variant.Price, 2, availableQuantity: 2);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
        var service = new EfInventoryReservationService(dbContext);

        cart.SetItemQuantity(Assert.Single(cart.Items).Id, 3, availableQuantity: 3);
        var result = await service.ReserveCartAsync(new ReserveCartInventoryRequest(
            buyer.Id,
            cart.Id,
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"),
            TimeSpan.FromMinutes(15)));

        Assert.True(result.IsFailure);
        Assert.Contains("Insufficient stock", result.Error.Details!["cart"].Single());
        Assert.Equal(3, variant.ReservedQuantity);
    }

    [Fact]
    public async Task ExpireReservationsAsync_ReleasesReservedQuantityAndMarksReservationExpired()
    {
        await using var dbContext = CreateDbContext();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", 499m, 599m, 5);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(product.Id, variant.Id, product.SellerId, "Cotton Dress", variant.Sku, variant.Size, variant.Colour, variant.Price, 2, variant.AvailableQuantity);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
        var service = new EfInventoryReservationService(dbContext);
        var startedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        await service.ReserveCartAsync(new ReserveCartInventoryRequest(buyer.Id, cart.Id, startedAt, TimeSpan.FromMinutes(15)));

        var result = await service.ExpireReservationsAsync(startedAt.AddMinutes(16));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Assert.Single(result.Value).Quantity);
        Assert.Equal(0, variant.ReservedQuantity);
        var reservation = await dbContext.InventoryReservations.SingleAsync();
        Assert.Equal(InventoryReservationStatus.Expired, reservation.Status);
        Assert.Equal(startedAt.AddMinutes(16), reservation.ExpiredAtUtc);
        var expiredMovement = await dbContext.InventoryMovements.SingleAsync(movement => movement.MovementType == InventoryMovementType.ReservationExpired);
        Assert.Equal(2, expiredMovement.ReservedQuantityBefore);
        Assert.Equal(0, expiredMovement.ReservedQuantityAfter);
        Assert.Equal(reservation.Id, expiredMovement.ReservationId);
    }

    [Fact]
    public async Task ExpireReservationsAsync_DoesNotDriveReservedQuantityBelowZero()
    {
        await using var dbContext = CreateDbContext();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", 499m, 599m, 5);
        var cart = new Cart(buyer.Id);
        var startedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var reservation = new InventoryReservation(variant.Id, buyer.Id, cart.Id, 2, startedAt.AddMinutes(15), startedAt);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        dbContext.InventoryReservations.Add(reservation);
        await dbContext.SaveChangesAsync();
        var service = new EfInventoryReservationService(dbContext);

        var result = await service.ExpireReservationsAsync(startedAt.AddMinutes(16));

        Assert.True(result.IsFailure);
        Assert.Equal("InventoryReservations.ReleaseConflict", result.Error.Code);
        Assert.Equal(0, variant.ReservedQuantity);
    }

    private static MabuntleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase($"InventoryReservationServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new MabuntleDbContext(options);
    }
}
