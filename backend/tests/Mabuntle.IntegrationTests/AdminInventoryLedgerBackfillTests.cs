using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Admin;
using Mabuntle.Api.Authentication;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Returns;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class AdminInventoryLedgerBackfillTests
{
    [Fact]
    public async Task Buyer_CannotRunInventoryLedgerBackfill()
    {
        using var factory = new AdminInventoryLedgerBackfillTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.PostAsJsonAsync(
            "/api/admin/inventory-ledger/backfill",
            new AdminInventoryLedgerBackfillRequest(DryRun: true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Backfill_DryRunReportsMissingReservationMovementsWithoutWritingRows()
    {
        using var factory = new AdminInventoryLedgerBackfillTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedConfirmedReservationAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            "/api/admin/inventory-ledger/backfill",
            new AdminInventoryLedgerBackfillRequest(DryRun: true, SellerId: seed.SellerId));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdminInventoryLedgerBackfillResponse>();
        Assert.NotNull(result);
        Assert.True(result!.DryRun);
        Assert.Equal(2, result.ScannedCount);
        Assert.Equal(2, result.CreatedMovementCount);
        Assert.Equal(0, result.SkippedExistingCount);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Empty(await dbContext.InventoryMovements.ToArrayAsync());
        Assert.Empty(await dbContext.AuditLogs.Where(audit => audit.ActionType == "InventoryLedgerBackfilled").ToArrayAsync());
    }

    [Fact]
    public async Task Backfill_ApplyCreatesReservationMovementsAndIsIdempotent()
    {
        using var factory = new AdminInventoryLedgerBackfillTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedConfirmedReservationAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            "/api/admin/inventory-ledger/backfill",
            new AdminInventoryLedgerBackfillRequest(DryRun: false, SellerId: seed.SellerId));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdminInventoryLedgerBackfillResponse>();
        Assert.NotNull(result);
        Assert.False(result!.DryRun);
        Assert.Equal(2, result.CreatedMovementCount);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var movements = await dbContext.InventoryMovements
                .OrderBy(movement => movement.OccurredAtUtc)
                .ToArrayAsync();
            Assert.Equal(2, movements.Length);
            Assert.Contains(movements, movement =>
                movement.MovementType == InventoryMovementType.ReservationCreated
                && movement.ReservationId == seed.ReservationId
                && movement.CartId == seed.CartId
                && movement.ReservedQuantityAfter - movement.ReservedQuantityBefore == 2);
            Assert.Contains(movements, movement =>
                movement.MovementType == InventoryMovementType.ReservationConfirmed
                && movement.ReservationId == seed.ReservationId
                && movement.OrderId == seed.OrderId
                && movement.PaymentId == seed.PaymentId
                && movement.ReservedQuantityAfter - movement.ReservedQuantityBefore == 0);
            Assert.Contains(dbContext.AuditLogs, audit => audit.ActionType == "InventoryLedgerBackfilled");
        }

        using var idempotentResponse = await client.PostAsJsonAsync(
            "/api/admin/inventory-ledger/backfill",
            new AdminInventoryLedgerBackfillRequest(DryRun: false, SellerId: seed.SellerId));
        idempotentResponse.EnsureSuccessStatusCode();
        var idempotentResult = await idempotentResponse.Content.ReadFromJsonAsync<AdminInventoryLedgerBackfillResponse>();
        Assert.NotNull(idempotentResult);
        Assert.Equal(0, idempotentResult!.CreatedMovementCount);
        Assert.Equal(2, idempotentResult.SkippedExistingCount);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            Assert.Equal(2, await dbContext.InventoryMovements.CountAsync());
        }
    }

    [Fact]
    public async Task Backfill_ApplyCreatesReturnAndRefundContextMovements()
    {
        using var factory = new AdminInventoryLedgerBackfillTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReturnAndRefundAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            "/api/admin/inventory-ledger/backfill",
            new AdminInventoryLedgerBackfillRequest(DryRun: false, SellerId: seed.SellerId));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdminInventoryLedgerBackfillResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.CreatedMovementCount);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var movements = await dbContext.InventoryMovements.ToArrayAsync();
        Assert.Contains(movements, movement =>
            movement.MovementType == InventoryMovementType.ReturnRequested
            && movement.ReturnRequestId == seed.ReturnRequestId
            && movement.OrderId == seed.OrderId
            && movement.QuantityDelta == 0
            && movement.ReservedQuantityAfter - movement.ReservedQuantityBefore == 0);
        Assert.Contains(movements, movement =>
            movement.MovementType == InventoryMovementType.RefundCompleted
            && movement.RefundId == seed.RefundId
            && movement.ReturnRequestId == seed.ReturnRequestId
            && movement.PaymentId == seed.PaymentId
            && movement.QuantityDelta == 0
            && movement.ReservedQuantityAfter - movement.ReservedQuantityBefore == 0);
    }

    private static async Task<ReservationSeed> SeedConfirmedReservationAsync(AdminInventoryLedgerBackfillTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.UtcNow.AddDays(-3);

        var buyer = new BuyerProfile(Guid.NewGuid());
        var seller = CreateSeller();
        var product = CreateProduct(seller.Id);
        var variant = new ProductVariant(product.Id, "BACKFILL-DRESS-M", "M", "Black", 499m, null, 10, reservedQuantity: 2);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(product.Id, variant.Id, seller.Id, product.Title, variant.Sku, variant.Size, variant.Colour, variant.Price, 2, 8);
        var reservation = new InventoryReservation(variant.Id, buyer.Id, cart.Id, 2, now.AddMinutes(15), now);
        reservation.Confirm(now.AddMinutes(5));
        var order = new Order(buyer.Id, seller.Id, cart.Id, now.AddMinutes(1));
        order.AddItem(product.Id, variant.Id, product.Title, variant.Sku, variant.Size, variant.Colour, variant.Price, 2);
        order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(6), "Historical seed paid.");
        var payment = new Payment(order.Id, buyer.Id, "fake-pay", order.TotalAmount, "ZAR", now.AddMinutes(2));
        payment.SetProviderReference($"payment-{payment.Id:N}", now.AddMinutes(2));
        payment.MarkPaid(now.AddMinutes(6));

        dbContext.AddRange(buyer, seller, product, variant, cart, reservation, order, payment);
        await dbContext.SaveChangesAsync();

        return new ReservationSeed(seller.Id, cart.Id, reservation.Id, order.Id, payment.Id);
    }

    private static async Task<ReturnRefundSeed> SeedReturnAndRefundAsync(AdminInventoryLedgerBackfillTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.UtcNow.AddDays(-2);

        var buyer = new BuyerProfile(Guid.NewGuid());
        var seller = CreateSeller();
        var product = CreateProduct(seller.Id);
        var variant = new ProductVariant(product.Id, "BACKFILL-BAG-OS", "OS", "Champagne", 799m, null, 6);
        var order = new Order(buyer.Id, seller.Id, Guid.NewGuid(), now);
        order.AddItem(product.Id, variant.Id, product.Title, variant.Sku, variant.Size, variant.Colour, variant.Price, 1);
        var orderItem = order.Items.Single();
        order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "Paid.");
        order.ChangeStatus(OrderStatus.Processing, now.AddMinutes(2), "Processing.");
        order.ChangeStatus(OrderStatus.Shipped, now.AddMinutes(3), "Shipped.");
        order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(4), "Delivered.");

        var payment = new Payment(order.Id, buyer.Id, "fake-pay", order.TotalAmount, "ZAR", now);
        payment.SetProviderReference($"payment-{payment.Id:N}", now);
        payment.MarkPaid(now.AddMinutes(1));

        var returnRequest = new ReturnRequest(
            order.Id,
            buyer.Id,
            seller.Id,
            ReturnReason.WrongItem,
            "Wrong item received.",
            now.AddMinutes(5));
        returnRequest.AddItem(orderItem.Id, product.Id, variant.Id, 1, ReturnReason.WrongItem, false, "Wrong colour.");
        returnRequest.MarkAwaitingSellerResponse(now.AddMinutes(5));

        var refund = new Refund(
            order.Id,
            payment.Id,
            buyer.Id,
            seller.Id,
            returnRequest.Id,
            payment.Amount,
            "ZAR",
            "Approved return refund.",
            requestedAtUtc: now.AddMinutes(6));
        refund.Approve(Guid.NewGuid(), "Approved after review.", now.AddMinutes(7));
        refund.MarkProcessing(now.AddMinutes(8));
        refund.MarkRefunded("provider-refund", now.AddMinutes(9));

        dbContext.AddRange(buyer, seller, product, variant, order, payment, returnRequest, refund);
        await dbContext.SaveChangesAsync();

        return new ReturnRefundSeed(seller.Id, order.Id, payment.Id, returnRequest.Id, refund.Id);
    }

    private static SellerProfile CreateSeller()
    {
        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Backfill Seller",
            "backfill-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Backfill Trading");
        return seller;
    }

    private static Product CreateProduct(Guid sellerId)
    {
        var product = new Product(sellerId);
        product.UpdateDraftDetails(
            categoryId: null,
            brandId: null,
            "Backfill Product",
            $"backfill-product-{Guid.NewGuid():N}",
            "Backfill short description.",
            "Backfill full description.");
        return product;
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "buyer-inventory-ledger-backfill@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", MabuntleRoles.Buyer));
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminInventoryLedgerBackfillTestFactory factory,
        HttpClient client)
    {
        var email = $"admin-inventory-ledger-{Guid.NewGuid():N}@example.test";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(admin, "Password123!");
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
            var roleResult = await userManager.AddToRoleAsync(admin, MabuntleRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return await LoginAsync(client, email);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed record ReservationSeed(
        Guid SellerId,
        Guid CartId,
        Guid ReservationId,
        Guid OrderId,
        Guid PaymentId);

    private sealed record ReturnRefundSeed(
        Guid SellerId,
        Guid OrderId,
        Guid PaymentId,
        Guid ReturnRequestId,
        Guid RefundId);

    private sealed class AdminInventoryLedgerBackfillTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleAdminInventoryLedgerBackfillTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MabuntleDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MabuntleDbContext>>();

                services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
                services.AddDbContext<MabuntleDbContext>((serviceProvider, options) =>
                {
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
                });
            });
        }
    }
}
