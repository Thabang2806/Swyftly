using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using Swyftly.Api.Authentication;
using Swyftly.Api.Catalog;
using Swyftly.Api.Sellers;
using Swyftly.Application.Identity;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Inventory;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class SellerProductDraftTests
{
    [Fact]
    public async Task Seller_CanCreateDraftAddVariantAndImage()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "product-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId);
        var withImage = await AddImageAsync(client, product.ProductId, isPrimary: true);

        Assert.Equal("Draft", product.Status);
        Assert.Single(withVariant.Variants);
        Assert.Single(withImage.Images);
        Assert.True(withImage.Images.Single().IsPrimary);
    }

    [Fact]
    public async Task SellerProduct_MerchandisingAndSeoFields_PersistAndReachPublicResponses()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "product-merchandising-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        var product = await CreateProductAsync(
            client,
            title: "Rose Linen Midi Dress",
            seoTitle: "Rose linen midi dress",
            seoDescription: "A breathable rose linen dress for polished warm-weather styling.",
            merchandisingLabel: "Seller pick",
            careInstructions: "Cold hand wash and dry flat.",
            productDisclaimer: "Colour may vary slightly by screen.");
        await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);

        using var listResponse = await client.GetAsync("/api/seller/products");
        listResponse.EnsureSuccessStatusCode();
        var products = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<SellerProductSummaryResponse>>();
        var summary = Assert.Single(products!);

        Assert.Equal("Seller pick", product.MerchandisingLabel);
        Assert.Equal("Seller pick", summary.MerchandisingLabel);
        Assert.Equal(10, summary.TotalStockQuantity);
        Assert.Equal(10, summary.AvailableQuantity);
        Assert.Equal("https://example.test/summer-dress.jpg", summary.PrimaryImageUrl);

        using var publicResponse = await client.GetAsync($"/api/products/{product.Slug}");
        publicResponse.EnsureSuccessStatusCode();
        var detail = await publicResponse.Content.ReadFromJsonAsync<PublicProductDetailResponse>();

        Assert.NotNull(detail);
        Assert.Equal("Seller pick", detail!.Product.MerchandisingLabel);
        Assert.Equal("Rose linen midi dress", detail.SeoTitle);
        Assert.Equal("A breathable rose linen dress for polished warm-weather styling.", detail.SeoDescription);
        Assert.Equal("Cold hand wash and dry flat.", detail.CareInstructions);
        Assert.Equal("Colour may vary slightly by screen.", detail.ProductDisclaimer);
    }

    [Fact]
    public async Task Seller_CanUploadLocalProductImage()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "product-upload-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);

        var uploaded = await UploadImageAsync(client, product.ProductId, isPrimary: true);

        var image = Assert.Single(uploaded.Images);
        Assert.True(image.IsPrimary);
        Assert.StartsWith("/media/product-images/", image.Url, StringComparison.Ordinal);
        Assert.EndsWith(".webp", image.StorageKey, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var persistedImage = await dbContext.ProductImages.SingleAsync(item => item.Id == image.ImageId);
        Assert.NotNull(persistedImage.MediaAssetId);
        Assert.Equal(3, await dbContext.MediaAssetVariants.CountAsync(item => item.MediaAssetId == persistedImage.MediaAssetId));
    }

    [Fact]
    public async Task PublishedProductRevision_Submit_DoesNotChangeLiveProduct()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "published-revision-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client, title: "Live Product Title");
        await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/revision",
            new UpsertSellerProductRevisionRequest(
                CatalogSeedData.WomenDresses,
                null,
                "Proposed Product Title",
                $"proposed-{Guid.NewGuid():N}",
                "Updated short description",
                "Updated full description for the product revision.",
                null,
                null,
                null,
                null,
                null,
                ["proposed"],
                new Dictionary<string, JsonElement>
                {
                    ["size"] = JsonDocument.Parse("\"M\"").RootElement.Clone(),
                    ["colour"] = JsonDocument.Parse("\"Black\"").RootElement.Clone()
                }));
        updateResponse.EnsureSuccessStatusCode();

        using var submitResponse = await client.PostAsync($"/api/seller/products/{product.ProductId}/revision/submit-review", null);
        submitResponse.EnsureSuccessStatusCode();
        var revision = await submitResponse.Content.ReadFromJsonAsync<SellerProductRevisionResponse>();

        Assert.NotNull(revision);
        Assert.Equal("PendingReview", revision!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var liveProduct = await dbContext.Products.SingleAsync(item => item.Id == product.ProductId);

        Assert.Equal("Live Product Title", liveProduct.Title);
    }

    [Fact]
    public async Task PublishedVariantRevision_Submit_DoesNotChangeLiveVariants()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "variant-revision-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client, title: "Live Variant Product");
        var productWithVariant = await AddVariantAsync(client, product.ProductId, sku: "LIVE-SKU-M-BLACK");
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);
        var liveVariant = productWithVariant.Variants.Single();

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/variant-revision",
            new UpsertSellerProductVariantRevisionRequest(
                "Seasonal pricing and size expansion.",
                [
                    new UpsertSellerProductVariantRevisionItemRequest(
                        "Update",
                        liveVariant.VariantId,
                        "LIVE-SKU-M-BLACK-UPDATED",
                        "M",
                        "Black",
                        599.99m,
                        799.99m,
                        null,
                        "BAR-UPDATED"),
                    new UpsertSellerProductVariantRevisionItemRequest(
                        "Add",
                        null,
                        "LIVE-SKU-L-BLACK",
                        "L",
                        "Black",
                        649.99m,
                        849.99m,
                        5,
                        "BAR-NEW")
                ]));
        updateResponse.EnsureSuccessStatusCode();

        using var submitResponse = await client.PostAsync(
            $"/api/seller/products/{product.ProductId}/variant-revision/submit-review",
            null);
        submitResponse.EnsureSuccessStatusCode();
        var revision = await submitResponse.Content.ReadFromJsonAsync<SellerProductVariantRevisionResponse>();

        Assert.NotNull(revision);
        Assert.Equal("PendingReview", revision!.Status);
        Assert.Contains(revision.ProposedFinalVariants, variant => variant.Sku == "LIVE-SKU-M-BLACK-UPDATED" && variant.Price == 599.99m);
        Assert.Contains(revision.ProposedFinalVariants, variant => variant.Sku == "LIVE-SKU-L-BLACK" && variant.StockQuantity == 5);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var liveVariants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.ProductId)
            .ToListAsync();

        var persistedLiveVariant = Assert.Single(liveVariants);
        Assert.Equal("LIVE-SKU-M-BLACK", persistedLiveVariant.Sku);
        Assert.Equal(499.99m, persistedLiveVariant.Price);
    }

    [Fact]
    public async Task PublishedVariantRevision_CsvExportAndTemplate_ReturnStableHeaders()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "variant-revision-export-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client, title: "Variant Export Product");
        await AddVariantAsync(client, product.ProductId, sku: "EXPORT-SKU-M-BLACK", barcode: "6000000000001");
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);

        using var exportResponse = await client.GetAsync($"/api/seller/products/{product.ProductId}/variant-revision/export.csv");
        exportResponse.EnsureSuccessStatusCode();
        var exportCsv = await exportResponse.Content.ReadAsStringAsync();

        const string variantRevisionCsvHeader = "\"operation\",\"sourceVariantId\",\"sku\",\"size\",\"colour\",\"price\",\"compareAtPrice\",\"initialStockQuantity\",\"barcode\"";
        Assert.Contains(variantRevisionCsvHeader, exportCsv);
        Assert.Contains("EXPORT-SKU-M-BLACK", exportCsv);
        Assert.Contains("6000000000001", exportCsv);

        using var templateResponse = await client.GetAsync($"/api/seller/products/{product.ProductId}/variant-revision/import-template.csv");
        templateResponse.EnsureSuccessStatusCode();
        var templateCsv = (await templateResponse.Content.ReadAsStringAsync()).Trim();

        Assert.Equal(variantRevisionCsvHeader, templateCsv);
    }

    [Fact]
    public async Task PublishedVariantRevision_ImportPreview_DoesNotPersistRevisionData()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "variant-revision-import-preview@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client, title: "Variant Import Preview Product");
        var productWithVariant = await AddVariantAsync(client, product.ProductId, sku: "PREVIEW-SKU-M-BLACK", barcode: "6000000000002");
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);
        var liveVariant = productWithVariant.Variants.Single();
        var csv = string.Join(
            "\n",
            "operation,sourceVariantId,sku,size,colour,price,compareAtPrice,initialStockQuantity,barcode",
            $"Update,{liveVariant.VariantId},PREVIEW-SKU-M-BLACK,M,Black,599.99,799.99,,6000000000003",
            "Add,,PREVIEW-SKU-L-BLACK,L,Black,649.99,849.99,5,6000000000004");

        using var response = await PostInventoryCsvAsync(
            client,
            $"/api/seller/products/{product.ProductId}/variant-revision/import/preview",
            csv);
        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<SellerProductVariantRevisionBulkImportResponse>();

        Assert.NotNull(preview);
        Assert.Equal(2, preview!.TotalRows);
        Assert.Equal(2, preview.ChangedRows);
        Assert.Equal(0, preview.ErrorRows);
        Assert.Contains(preview.ProposedFinalVariants, variant => variant.Sku == "PREVIEW-SKU-M-BLACK" && variant.Price == 599.99m);
        Assert.Contains(preview.ProposedFinalVariants, variant => variant.Sku == "PREVIEW-SKU-L-BLACK" && variant.StockQuantity == 5);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var revisionCount = await dbContext.ProductVariantRevisions.CountAsync(revision => revision.ProductId == product.ProductId);

        Assert.Equal(0, revisionCount);
    }

    [Fact]
    public async Task PublishedVariantRevision_BulkStage_ReplacesCurrentDraftItems()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "variant-revision-bulk-stage@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client, title: "Variant Bulk Stage Product");
        var productWithVariant = await AddVariantAsync(client, product.ProductId, sku: "BULK-SKU-M-BLACK", barcode: "6000000000005");
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);
        var liveVariant = productWithVariant.Variants.Single();

        using var initialStageResponse = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/variant-revision",
            new UpsertSellerProductVariantRevisionRequest(
                "Initial manual draft.",
                [
                    new UpsertSellerProductVariantRevisionItemRequest(
                        "Add",
                        null,
                        "BULK-SKU-L-BLACK",
                        "L",
                        "Black",
                        649.99m,
                        849.99m,
                        5,
                        "6000000000006")
                ]));
        initialStageResponse.EnsureSuccessStatusCode();

        using var bulkResponse = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/variant-revision/bulk-stage",
            new BulkStageSellerProductVariantRevisionRequest(
                "Seasonal CSV pricing update.",
                [
                    new UpsertSellerProductVariantRevisionItemRequest(
                        "Update",
                        liveVariant.VariantId,
                        "BULK-SKU-M-BLACK",
                        null,
                        null,
                        579.99m,
                        699.99m,
                        null,
                        "6000000000007")
                ]));
        bulkResponse.EnsureSuccessStatusCode();
        var preview = await bulkResponse.Content.ReadFromJsonAsync<SellerProductVariantRevisionBulkImportResponse>();

        Assert.NotNull(preview);
        Assert.Equal(1, preview!.ChangedRows);
        Assert.Equal(0, preview.ErrorRows);

        using var revisionResponse = await client.GetAsync($"/api/seller/products/{product.ProductId}/variant-revision");
        revisionResponse.EnsureSuccessStatusCode();
        var revision = await revisionResponse.Content.ReadFromJsonAsync<SellerProductVariantRevisionResponse>();

        Assert.NotNull(revision);
        var item = Assert.Single(revision!.Items);
        Assert.Equal("Update", item.Operation);
        Assert.Equal("BULK-SKU-M-BLACK", item.Sku);
        Assert.Equal(579.99m, item.Price);
        Assert.Equal("6000000000007", item.Barcode);
    }

    [Fact]
    public async Task PublishedVariantRevision_BulkStage_RejectsPendingReviewRevision()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "variant-revision-bulk-pending@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client, title: "Variant Bulk Pending Product");
        var productWithVariant = await AddVariantAsync(client, product.ProductId, sku: "PENDING-SKU-M-BLACK", barcode: "6000000000008");
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);
        var liveVariant = productWithVariant.Variants.Single();

        using var stageResponse = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/variant-revision",
            new UpsertSellerProductVariantRevisionRequest(
                "Submit current revision.",
                [
                    new UpsertSellerProductVariantRevisionItemRequest(
                        "Update",
                        liveVariant.VariantId,
                        "PENDING-SKU-M-BLACK",
                        "M",
                        "Black",
                        529.99m,
                        699.99m,
                        null,
                        "6000000000008")
                ]));
        stageResponse.EnsureSuccessStatusCode();

        using var submitResponse = await client.PostAsync(
            $"/api/seller/products/{product.ProductId}/variant-revision/submit-review",
            null);
        submitResponse.EnsureSuccessStatusCode();

        using var bulkResponse = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/variant-revision/bulk-stage",
            new BulkStageSellerProductVariantRevisionRequest(
                "Attempt to mutate pending revision.",
                [
                    new UpsertSellerProductVariantRevisionItemRequest(
                        "Update",
                        liveVariant.VariantId,
                        "PENDING-SKU-M-BLACK",
                        null,
                        null,
                        549.99m,
                        699.99m,
                        null,
                        "6000000000008")
                ]));

        Assert.Equal(HttpStatusCode.BadRequest, bulkResponse.StatusCode);
    }

    [Fact]
    public async Task PublishedVariantRevision_DeactivateRejectsActiveCartItems()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "variant-revision-cart-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client, title: "Cart Guard Product");
        var productWithVariant = await AddVariantAsync(client, product.ProductId, sku: "CART-GUARD-M-BLACK");
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);
        var liveVariant = productWithVariant.Variants.Single();
        await AddActiveCartItemAsync(factory, product.ProductId, liveVariant.VariantId);

        using var response = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/variant-revision",
            new UpsertSellerProductVariantRevisionRequest(
                "Remove unavailable size.",
                [
                    new UpsertSellerProductVariantRevisionItemRequest(
                        "Deactivate",
                        liveVariant.VariantId,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null)
                ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CannotAccessAnotherSellersProduct()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterAndLoginSellerAsync(client, "seller-one-products@example.test");
        var sellerTwo = await RegisterAndLoginSellerAsync(client, "seller-two-products@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var product = await CreateProductAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.AccessToken);
        using var response = await client.GetAsync($"/api/seller/products/{product.ProductId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SellerInventory_List_ReturnsOnlyOwnedVariants()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterAndLoginSellerAsync(client, "inventory-seller-one@example.test");
        var sellerTwo = await RegisterAndLoginSellerAsync(client, "inventory-seller-two@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var productOne = await CreateProductAsync(client, title: "Owned Inventory Dress");
        var productOneWithVariant = await AddVariantAsync(client, productOne.ProductId);
        await AddImageAsync(client, productOne.ProductId, isPrimary: true);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.AccessToken);
        var productTwo = await CreateProductAsync(client, title: "Other Seller Belt");
        await AddVariantAsync(client, productTwo.ProductId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        using var response = await client.GetAsync("/api/seller/inventory");

        response.EnsureSuccessStatusCode();
        var inventory = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<SellerInventoryItemResponse>>();

        Assert.NotNull(inventory);
        var item = Assert.Single(inventory!);
        Assert.Equal(productOneWithVariant.Variants.Single().VariantId, item.VariantId);
        Assert.Equal("Owned Inventory Dress", item.ProductTitle);
        Assert.Equal("https://example.test/summer-dress.jpg", item.PrimaryImageUrl);
    }

    [Fact]
    public async Task SellerInventory_Adjust_AllowsPublishedProductAndWritesAuditLog()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "inventory-adjust-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);
        var variantId = withVariant.Variants.Single().VariantId;

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/inventory/{variantId}/adjust",
            new AdjustSellerInventoryRequest(8, "OutOfStock", "Seasonal stocktake"));

        response.EnsureSuccessStatusCode();
        var adjusted = await response.Content.ReadFromJsonAsync<SellerInventoryItemResponse>();

        Assert.NotNull(adjusted);
        Assert.Equal(8, adjusted!.StockQuantity);
        Assert.Equal("OutOfStock", adjusted.VariantStatus);
        Assert.Equal("Published", adjusted.ProductStatus);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var auditLog = await dbContext.AuditLogs.SingleAsync(audit => audit.ActionType == "SellerInventoryAdjusted");

        Assert.Equal("ProductVariant", auditLog.EntityType);
        Assert.Equal(variantId.ToString(), auditLog.EntityId);
        Assert.Equal("Seasonal stocktake", auditLog.Reason);

        var movement = await dbContext.InventoryMovements.SingleAsync(item => item.ProductVariantId == variantId);
        Assert.Equal(InventoryMovementType.SellerAdjustment, movement.MovementType);
        Assert.Equal(10, movement.StockQuantityBefore);
        Assert.Equal(8, movement.StockQuantityAfter);
        Assert.Equal(-2, movement.QuantityDelta);
        Assert.Equal(ProductVariantStatus.Active, movement.StatusBefore);
        Assert.Equal(ProductVariantStatus.OutOfStock, movement.StatusAfter);
        Assert.Equal("Seasonal stocktake", movement.Reason);
    }

    [Fact]
    public async Task SellerInventory_Adjust_RejectsStockBelowReservedQuantity()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "inventory-reserved-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId);
        var variantId = withVariant.Variants.Single().VariantId;
        await ReserveVariantAsync(factory, variantId, 4);

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/inventory/{variantId}/adjust",
            new AdjustSellerInventoryRequest(3, "Active", "Trying to reduce below reservations"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SellerInventory_Adjust_RejectsAnotherSellersVariant()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterAndLoginSellerAsync(client, "inventory-owner@example.test");
        var sellerTwo = await RegisterAndLoginSellerAsync(client, "inventory-not-owner@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.AccessToken);
        using var response = await client.PostAsJsonAsync(
            $"/api/seller/inventory/{withVariant.Variants.Single().VariantId}/adjust",
            new AdjustSellerInventoryRequest(8, "Active", "Wrong seller attempt"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SellerInventory_Adjust_RequiresReasonAndValidStatus()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "inventory-validation-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId);
        var variantId = withVariant.Variants.Single().VariantId;

        using var missingReasonResponse = await client.PostAsJsonAsync(
            $"/api/seller/inventory/{variantId}/adjust",
            new AdjustSellerInventoryRequest(8, "Active", " "));
        using var invalidStatusResponse = await client.PostAsJsonAsync(
            $"/api/seller/inventory/{variantId}/adjust",
            new AdjustSellerInventoryRequest(8, "Discontinued", "Status correction"));

        Assert.Equal(HttpStatusCode.BadRequest, missingReasonResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatusResponse.StatusCode);
    }

    [Fact]
    public async Task SellerInventory_ExportAndTemplate_ReturnCsvForOwnedVariants()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "inventory-export-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client, title: "Export Dress");
        var withVariant = await AddVariantAsync(client, product.ProductId, sku: "EXPORT-DRESS-M-BLACK");

        using var exportResponse = await client.GetAsync("/api/seller/inventory/export.csv");
        using var templateResponse = await client.GetAsync("/api/seller/inventory/import-template.csv");

        exportResponse.EnsureSuccessStatusCode();
        templateResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", exportResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("text/csv", templateResponse.Content.Headers.ContentType?.MediaType);

        var exportCsv = await exportResponse.Content.ReadAsStringAsync();
        var templateCsv = await templateResponse.Content.ReadAsStringAsync();

        Assert.Contains("\"variantId\",\"sku\",\"barcode\",\"productTitle\"", exportCsv);
        Assert.Contains(withVariant.Variants.Single().VariantId.ToString(), exportCsv);
        Assert.Contains("EXPORT-DRESS-M-BLACK", exportCsv);
        Assert.Contains("\"stockQuantity\",\"status\"", templateCsv);
    }

    [Fact]
    public async Task SellerInventory_ImportPreview_ValidatesRowsWithoutChangingInventory()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "inventory-preview-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId, sku: "PREVIEW-DRESS-M-BLACK");
        var variantId = withVariant.Variants.Single().VariantId;

        using var response = await PostInventoryCsvAsync(
            client,
            "/api/seller/inventory/import/preview",
            "sku,stockQuantity,status\nPREVIEW-DRESS-M-BLACK,12,Inactive\n");

        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<SellerInventoryBulkAdjustmentResponse>();

        Assert.NotNull(preview);
        Assert.Equal(1, preview!.TotalRows);
        Assert.Equal(1, preview.ChangedRows);
        Assert.Equal(0, preview.ErrorRows);
        Assert.Equal(12, preview.Rows.Single().ProposedStockQuantity);
        Assert.Equal("Changed", preview.Rows.Single().RowStatus);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var variant = await dbContext.ProductVariants.SingleAsync(item => item.Id == variantId);
        Assert.Equal(10, variant.StockQuantity);
        Assert.Equal(ProductVariantStatus.Active, variant.Status);
    }

    [Fact]
    public async Task SellerInventory_BulkAdjust_UpdatesMultipleVariantsAndWritesAuditLogs()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "inventory-bulk-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var productOne = await CreateProductAsync(client, title: "Bulk Dress");
        var productOneWithVariant = await AddVariantAsync(client, productOne.ProductId, sku: "BULK-DRESS-M-BLACK");
        var productTwo = await CreateProductAsync(client, title: "Bulk Belt");
        var productTwoWithVariant = await AddVariantAsync(
            client,
            productTwo.ProductId,
            sku: "BULK-BELT-OS-BROWN",
            size: "OS",
            colour: "Brown",
            stockQuantity: 5);

        using var response = await client.PostAsJsonAsync(
            "/api/seller/inventory/bulk-adjust",
            new BulkAdjustSellerInventoryRequest(
                "Monthly stocktake",
                [
                    new BulkAdjustSellerInventoryItemRequest(
                        productOneWithVariant.Variants.Single().VariantId,
                        "BULK-DRESS-M-BLACK",
                        12,
                        "Inactive"),
                    new BulkAdjustSellerInventoryItemRequest(
                        null,
                        "BULK-BELT-OS-BROWN",
                        9,
                        "OutOfStock")
                ]));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SellerInventoryBulkAdjustmentResponse>();

        Assert.NotNull(result);
        Assert.Equal(2, result!.ChangedRows);
        Assert.Equal(0, result.ErrorRows);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var variantOne = await dbContext.ProductVariants.SingleAsync(item => item.Id == productOneWithVariant.Variants.Single().VariantId);
        var variantTwo = await dbContext.ProductVariants.SingleAsync(item => item.Id == productTwoWithVariant.Variants.Single().VariantId);
        Assert.Equal(12, variantOne.StockQuantity);
        Assert.Equal(ProductVariantStatus.Inactive, variantOne.Status);
        Assert.Equal(9, variantTwo.StockQuantity);
        Assert.Equal(ProductVariantStatus.OutOfStock, variantTwo.Status);
        Assert.Equal(2, await dbContext.AuditLogs.CountAsync(audit => audit.ActionType == "SellerInventoryBulkAdjusted"));
        Assert.Equal(2, await dbContext.InventoryMovements.CountAsync(movement => movement.MovementType == InventoryMovementType.BulkImportAdjustment));
        Assert.Single(await dbContext.InventoryMovements
            .Where(movement => movement.ProductVariantId == productOneWithVariant.Variants.Single().VariantId)
            .Select(movement => movement.BatchReference)
            .ToArrayAsync());
    }

    [Fact]
    public async Task SellerInventory_History_IsScopedAndFiltersBySkuBarcodeAndMovementType()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterAndLoginSellerAsync(client, "inventory-history-owner@example.test");
        var sellerTwo = await RegisterAndLoginSellerAsync(client, "inventory-history-other@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var product = await CreateProductAsync(client, title: "History Dress");
        var withVariant = await AddVariantAsync(client, product.ProductId, sku: "HISTORY-DRESS-M-BLACK", barcode: "6001000000012");
        var variantId = withVariant.Variants.Single().VariantId;

        using var adjustResponse = await client.PostAsJsonAsync(
            $"/api/seller/inventory/{variantId}/adjust",
            new AdjustSellerInventoryRequest(7, "Inactive", "Cycle count correction"));
        adjustResponse.EnsureSuccessStatusCode();

        using var skuHistoryResponse = await client.GetAsync(
            "/api/seller/inventory/history?sku=HISTORY-DRESS-M-BLACK&movementType=SellerAdjustment");
        using var barcodeHistoryResponse = await client.GetAsync(
            "/api/seller/inventory/history?barcode=6001000000012");
        using var variantHistoryResponse = await client.GetAsync($"/api/seller/inventory/{variantId}/history");

        skuHistoryResponse.EnsureSuccessStatusCode();
        barcodeHistoryResponse.EnsureSuccessStatusCode();
        variantHistoryResponse.EnsureSuccessStatusCode();
        var skuHistory = await skuHistoryResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<SellerInventoryMovementResponse>>();
        var barcodeHistory = await barcodeHistoryResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<SellerInventoryMovementResponse>>();
        var variantHistory = await variantHistoryResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<SellerInventoryMovementResponse>>();

        Assert.NotNull(skuHistory);
        var movement = Assert.Single(skuHistory!);
        Assert.Equal(variantId, movement.VariantId);
        Assert.Equal("6001000000012", movement.Barcode);
        Assert.Equal("SellerAdjustment", movement.MovementType);
        Assert.Equal("Cycle count correction", movement.Reason);
        Assert.Single(barcodeHistory!);
        Assert.Single(variantHistory!);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.AccessToken);
        using var otherSellerHistoryResponse = await client.GetAsync($"/api/seller/inventory/{variantId}/history");
        Assert.Equal(HttpStatusCode.NotFound, otherSellerHistoryResponse.StatusCode);
    }

    [Fact]
    public async Task SellerInventory_BulkAdjust_DoesNotCreateMovementForUnchangedRows()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "inventory-bulk-unchanged-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId, sku: "BULK-UNCHANGED-DRESS", barcode: "6001000000013");
        var variantId = withVariant.Variants.Single().VariantId;

        using var response = await client.PostAsJsonAsync(
            "/api/seller/inventory/bulk-adjust",
            new BulkAdjustSellerInventoryRequest(
                "No-op stocktake",
                [
                    new BulkAdjustSellerInventoryItemRequest(null, null, 10, "Active", "6001000000013")
                ]));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SellerInventoryBulkAdjustmentResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result!.ChangedRows);
        Assert.Equal(1, result.UnchangedRows);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        Assert.Empty(await dbContext.InventoryMovements.Where(movement => movement.ProductVariantId == variantId).ToArrayAsync());
    }

    [Fact]
    public async Task SellerInventory_BulkAdjust_RejectsInvalidRowsAndDoesNotPartiallyUpdate()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "inventory-bulk-invalid-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var withVariant = await AddVariantAsync(client, product.ProductId, sku: "BULK-INVALID-DRESS");
        var variantId = withVariant.Variants.Single().VariantId;

        using var response = await client.PostAsJsonAsync(
            "/api/seller/inventory/bulk-adjust",
            new BulkAdjustSellerInventoryRequest(
                "Invalid stocktake",
                [
                    new BulkAdjustSellerInventoryItemRequest(variantId, "BULK-INVALID-DRESS", 12, "Inactive"),
                    new BulkAdjustSellerInventoryItemRequest(null, "UNKNOWN-SKU", 8, "Active")
                ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SellerInventoryBulkAdjustmentResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result!.ErrorRows);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var variant = await dbContext.ProductVariants.SingleAsync(item => item.Id == variantId);
        Assert.Equal(10, variant.StockQuantity);
        Assert.Equal(ProductVariantStatus.Active, variant.Status);
        Assert.Empty(await dbContext.AuditLogs.Where(audit => audit.ActionType == "SellerInventoryBulkAdjusted").ToArrayAsync());
    }

    [Fact]
    public async Task Product_PrimaryImageAttach_ClearsPreviousPrimary()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "primary-image-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/images",
            new AttachSellerProductImageRequest(
                "second-image",
                "https://example.test/second.jpg",
                "Second image",
                1,
                IsPrimary: true));

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();

        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Images.Count);
        Assert.Equal("second-image", Assert.Single(updated.Images, image => image.IsPrimary).StorageKey);
    }

    [Fact]
    public async Task Seller_CanUpdateProductImageMetadataAndPrimaryStatus()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "image-update-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var withPrimaryImage = await AddImageAsync(client, product.ProductId, isPrimary: true);
        var withSecondImage = await AddImageAsync(
            client,
            product.ProductId,
            isPrimary: false,
            storageKey: "summer-dress-side",
            url: "https://example.test/summer-dress-side.jpg",
            altText: "Side image",
            sortOrder: 1);
        var firstImageId = withPrimaryImage.Images.Single().ImageId;
        var secondImageId = withSecondImage.Images.Single(image => image.ImageId != firstImageId).ImageId;

        using var response = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/images/{secondImageId}",
            new UpdateSellerProductImageRequest("Updated side image", 7, IsPrimary: true));

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();

        Assert.NotNull(updated);
        var firstImage = updated!.Images.Single(image => image.ImageId == firstImageId);
        var secondImage = updated.Images.Single(image => image.ImageId == secondImageId);
        Assert.False(firstImage.IsPrimary);
        Assert.True(secondImage.IsPrimary);
        Assert.Equal("Updated side image", secondImage.AltText);
        Assert.Equal(7, secondImage.SortOrder);
    }

    [Fact]
    public async Task Seller_CannotUpdateAnotherSellersProductImage()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterAndLoginSellerAsync(client, "image-owner@example.test");
        var sellerTwo = await RegisterAndLoginSellerAsync(client, "image-not-owner@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var product = await CreateProductAsync(client);
        var withImage = await AddImageAsync(client, product.ProductId, isPrimary: true);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.AccessToken);
        using var response = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/images/{withImage.Images.Single().ImageId}",
            new UpdateSellerProductImageRequest("Wrong owner update", 0, IsPrimary: true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CannotUpdateProductImageOnPublishedProduct()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "published-image-update-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        await AddVariantAsync(client, product.ProductId);
        var withImage = await AddImageAsync(client, product.ProductId, isPrimary: true);
        await SubmitAndPublishProductAsync(client, factory, product.ProductId);

        using var response = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/images/{withImage.Images.Single().ImageId}",
            new UpdateSellerProductImageRequest("Published image update", 0, IsPrimary: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SellerImageUpdate_RejectsNegativeSortOrder()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "negative-image-sort-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var withImage = await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PutAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/images/{withImage.Images.Single().ImageId}",
            new UpdateSellerProductImageRequest("Invalid sort", -1, IsPrimary: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnverifiedSeller_CannotSubmitProductForReview()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "unverified-product-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);

        using var response = await client.PostAsync($"/api/seller/products/{product.ProductId}/submit-review", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VerifiedSeller_CanSubmitCompleteProductForReview()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "verified-product-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsync($"/api/seller/products/{product.ProductId}/submit-review", null);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("PendingReview", submitted!.Status);
    }

    [Fact]
    public async Task SubmitReview_WithCounterfeitRiskTerms_StoresModerationFlagsAndNeedsAdminReview()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "counterfeit-risk-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(
            client,
            title: "Designer inspired summer dress",
            fullDescription: "A mirror quality look for evening events.");
        await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsync($"/api/seller/products/{product.ProductId}/submit-review", null);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("NeedsAdminReview", submitted!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var moderation = await dbContext.AiModerationResults.SingleAsync(result => result.ProductId == product.ProductId);

        Assert.True(moderation.NeedsAdminReview);
        Assert.Equal(AiModerationRiskLevel.High, moderation.RiskLevel);
        Assert.Contains("designer inspired", moderation.DetectedTermsJson);
        Assert.Contains("mirror quality", moderation.DetectedTermsJson);
    }

    [Fact]
    public async Task SubmitReview_ForBeautyProductMissingSafetyFields_StoresModerationFlags()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "beauty-missing-fields-seller@example.test");
        await MarkSellerVerifiedAsync(factory, seller.UserId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(
            client,
            categoryId: CatalogSeedData.BeautyFoundation,
            title: "Matte foundation",
            fullDescription: "A lightweight foundation for daily wear.",
            attributes: new Dictionary<string, object?>
            {
                ["shade"] = "Medium"
            });
        await AddVariantAsync(client, product.ProductId);
        await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsync($"/api/seller/products/{product.ProductId}/submit-review", null);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("NeedsAdminReview", submitted!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var moderation = await dbContext.AiModerationResults.SingleAsync(result => result.ProductId == product.ProductId);

        Assert.Contains("ingredients", moderation.MissingFieldsJson);
        Assert.Contains("expiry date", moderation.MissingFieldsJson);
        Assert.Contains("batch number", moderation.MissingFieldsJson);
        Assert.Contains("sealed/unsealed status", moderation.MissingFieldsJson);
    }

    [Fact]
    public async Task AiSuggestion_RejectsUnauthenticatedAccess()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{Guid.NewGuid()}/ai-suggestions",
            CreateAiSuggestionRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AiSuggestion_RejectsWrongSeller()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await RegisterAndLoginSellerAsync(client, "ai-seller-one@example.test");
        var sellerTwo = await RegisterAndLoginSellerAsync(client, "ai-seller-two@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var product = await CreateProductAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerTwo.AccessToken);
        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions",
            CreateAiSuggestionRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AiSuggestion_GeneratesAndPersistsSuggestionWithFakeProvider()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "ai-generation-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var productWithImage = await AddImageAsync(client, product.ProductId, isPrimary: true);

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions",
            CreateAiSuggestionRequest(productWithImage.Images.Select(image => image.ImageId).ToArray()));

        response.EnsureSuccessStatusCode();
        var suggestion = await response.Content.ReadFromJsonAsync<SellerAiSuggestionResponse>();
        Assert.NotNull(suggestion);
        Assert.Equal("AI-assisted product title", suggestion!.RecommendedTitle);
        Assert.Contains("brand", suggestion.MissingFields);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var savedSuggestion = await dbContext.AiProductSuggestions.SingleAsync();
        var usageLog = await dbContext.AiUsageLogs.SingleAsync();
        var sellerProfile = await dbContext.SellerProfiles.SingleAsync(profile => profile.UserId == seller.UserId);

        Assert.Equal(product.ProductId, savedSuggestion.ProductId);
        Assert.Equal(sellerProfile.Id, savedSuggestion.SellerId);
        Assert.True(usageLog.Success);
        Assert.Equal(seller.UserId.ToString(), usageLog.UserId);
    }

    [Fact]
    public async Task AiSuggestionApply_CanApplyPartialEditedValuesAndWritesAudits()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "ai-apply-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var productWithImage = await AddImageAsync(client, product.ProductId, isPrimary: true);
        var imageId = productWithImage.Images.Single().ImageId;

        using var generateResponse = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions",
            CreateAiSuggestionRequest([imageId]));
        generateResponse.EnsureSuccessStatusCode();
        var suggestion = await generateResponse.Content.ReadFromJsonAsync<SellerAiSuggestionResponse>();
        Assert.NotNull(suggestion);

        using var applyResponse = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions/{suggestion!.SuggestionId}/apply",
            new
            {
                fieldsToApply = new[] { "title", "tags", "imageAltText" },
                editedValues = new
                {
                    title = "Seller reviewed AI title",
                    tags = new[] { "summer", "reviewed" },
                    imageAltText = new Dictionary<string, string?>
                    {
                        [imageId.ToString()] = "Model wearing a black summer dress"
                    }
                }
            });

        applyResponse.EnsureSuccessStatusCode();
        var updatedProduct = await applyResponse.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(updatedProduct);
        Assert.Equal("Seller reviewed AI title", updatedProduct!.Title);
        Assert.Contains("summer", updatedProduct.Tags);
        Assert.Equal("Model wearing a black summer dress", updatedProduct.Images.Single().AltText);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var savedSuggestion = await dbContext.AiProductSuggestions.SingleAsync(item => item.Id == suggestion.SuggestionId);
        var audits = await dbContext.AiSuggestionFieldAudits
            .Where(audit => audit.SuggestionId == suggestion.SuggestionId)
            .ToListAsync();

        Assert.Equal("Applied", savedSuggestion.Status.ToString());
        Assert.Equal(3, audits.Count);
        Assert.Contains(audits, audit => audit.FieldName == "title" && audit.WasEdited);
        Assert.Contains(audits, audit => audit.FieldName == "tags" && audit.WasEdited);
        Assert.Contains(audits, audit => audit.FieldName == "imageAltText" && audit.WasEdited);
    }

    [Fact]
    public async Task AiSuggestionApply_RejectsInvalidAttributeValues()
    {
        using var factory = new SellerProductDraftTestFactory();
        using var client = factory.CreateClient();
        var seller = await RegisterAndLoginSellerAsync(client, "ai-invalid-attribute-seller@example.test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var product = await CreateProductAsync(client);
        var suggestionId = await CreateInvalidAttributeSuggestionAsync(factory, product.ProductId, seller.UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{product.ProductId}/ai-suggestions/{suggestionId}/apply",
            new
            {
                fieldsToApply = new[] { "attributes" },
                editedValues = new { }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        Assert.Empty(await dbContext.AiSuggestionFieldAudits.Where(audit => audit.SuggestionId == suggestionId).ToListAsync());
    }

    private static async Task<AuthResponse> RegisterAndLoginSellerAsync(HttpClient client, string email)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", SwyftlyRoles.Seller));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!;
    }

    private static async Task<SellerProductDetailResponse> CreateProductAsync(
        HttpClient client,
        Guid? categoryId = null,
        string title = "Summer Dress",
        string fullDescription = "A lightweight summer dress with a relaxed fit.",
        IReadOnlyDictionary<string, object?>? attributes = null,
        string? seoTitle = null,
        string? seoDescription = null,
        string? merchandisingLabel = null,
        string? careInstructions = null,
        string? productDisclaimer = null)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/seller/products",
            new
            {
                categoryId = categoryId ?? CatalogSeedData.WomenDresses,
                brandId = (Guid?)null,
                title,
                slug = $"product-{Guid.NewGuid():N}",
                shortDescription = "A lightweight summer dress.",
                fullDescription,
                seoTitle,
                seoDescription,
                merchandisingLabel,
                careInstructions,
                productDisclaimer,
                attributes = attributes ?? new Dictionary<string, object?>
                {
                    ["size"] = "M",
                    ["colour"] = "Black"
                }
            });
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(product);
        return product!;
    }

    private static async Task<SellerProductDetailResponse> AddVariantAsync(
        HttpClient client,
        Guid productId,
        string sku = "SUMMER-DRESS-M-BLACK",
        string size = "M",
        string colour = "Black",
        int stockQuantity = 10,
        string? barcode = null)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{productId}/variants",
            new UpsertSellerProductVariantRequest(
                sku,
                size,
                colour,
                499.99m,
                699.99m,
                stockQuantity,
                0,
                "Active",
                barcode));
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(product);
        return product!;
    }

    private static async Task<HttpResponseMessage> PostInventoryCsvAsync(
        HttpClient client,
        string requestUri,
        string csv)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "inventory.csv");

        return await client.PostAsync(requestUri, content);
    }

    private static async Task<SellerProductDetailResponse> AddImageAsync(
        HttpClient client,
        Guid productId,
        bool isPrimary,
        string storageKey = "summer-dress-primary",
        string url = "https://example.test/summer-dress.jpg",
        string altText = "Summer dress",
        int sortOrder = 0)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/seller/products/{productId}/images",
            new AttachSellerProductImageRequest(
                storageKey,
                url,
                altText,
                sortOrder,
                isPrimary));
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(product);
        return product!;
    }

    private static async Task<SellerProductDetailResponse> UploadImageAsync(
        HttpClient client,
        Guid productId,
        bool isPrimary,
        string altText = "Uploaded dress image",
        int sortOrder = 0)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(CreateTinyPngBytes());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "dress.png");
        content.Add(new StringContent(altText), "altText");
        content.Add(new StringContent(sortOrder.ToString()), "sortOrder");
        content.Add(new StringContent(isPrimary.ToString()), "isPrimary");

        using var response = await client.PostAsync($"/api/seller/products/{productId}/images/upload", content);
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<SellerProductDetailResponse>();
        Assert.NotNull(product);
        return product!;
    }

    private static byte[] CreateTinyPngBytes()
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Plum);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task MarkSellerVerifiedAsync(
        SellerProductDraftTestFactory factory,
        Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var seller = await dbContext.SellerProfiles.SingleAsync(profile => profile.UserId == userId);

        seller.UpdateProfile(
            "Verified Seller",
            "verified-product-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Verified Trading");

        var storefront = new SellerStorefront(seller.Id, "Verified Seller", $"verified-{seller.Id:N}");
        storefront.Publish();

        var address = new SellerAddress(
            seller.Id,
            "1 Market Street",
            null,
            "Johannesburg",
            "Gauteng",
            "2000",
            "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref-123");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);

        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        seller.MarkVerified(storefront, address, payout);

        await dbContext.SaveChangesAsync();
    }

    private static async Task SubmitAndPublishProductAsync(
        HttpClient client,
        SellerProductDraftTestFactory factory,
        Guid productId)
    {
        using var response = await client.PostAsync($"/api/seller/products/{productId}/submit-review", null);
        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var product = await dbContext.Products.SingleAsync(item => item.Id == productId);
        product.Publish(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    private static async Task ReserveVariantAsync(
        SellerProductDraftTestFactory factory,
        Guid variantId,
        int quantity)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var variant = await dbContext.ProductVariants.SingleAsync(item => item.Id == variantId);
        variant.Reserve(quantity);
        await dbContext.SaveChangesAsync();
    }

    private static async Task AddActiveCartItemAsync(
        SellerProductDraftTestFactory factory,
        Guid productId,
        Guid variantId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var product = await dbContext.Products.SingleAsync(item => item.Id == productId);
        var variant = await dbContext.ProductVariants.SingleAsync(item => item.Id == variantId);
        var buyer = new BuyerProfile(Guid.NewGuid());
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(
            product.Id,
            variant.Id,
            product.SellerId,
            product.Title,
            variant.Sku,
            variant.Size,
            variant.Colour,
            variant.Price,
            1,
            variant.AvailableQuantity);

        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
    }

    private static GenerateSellerAiSuggestionRequest CreateAiSuggestionRequest(
        IReadOnlyCollection<Guid>? imageIds = null) =>
        new(
            "Lightweight summer dress; brand is not confirmed.",
            "Dress",
            CatalogSeedData.WomenDresses,
            new Dictionary<string, JsonElement>(),
            imageIds ?? []);

    private static async Task<Guid> CreateInvalidAttributeSuggestionAsync(
        SellerProductDraftTestFactory factory,
        Guid productId,
        Guid sellerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var seller = await dbContext.SellerProfiles.SingleAsync(profile => profile.UserId == sellerUserId);
        var suggestion = new AiProductSuggestion(
            seller.Id,
            productId,
            "Invalid size suggestion",
            "[]",
            "Suggested title",
            "Suggested short",
            "Suggested full",
            CatalogSeedData.WomenDresses,
            "Women > Clothing > Dresses",
            "{\"size\":\"XXL\"}",
            "[]",
            "[]",
            "[]",
            50,
            "local-test-model",
            "listing-assistant-v1",
            DateTimeOffset.UtcNow);

        dbContext.AiProductSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();
        return suggestion.Id;
    }

    private sealed class SellerProductDraftTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlySellerProductDraftTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SwyftlyDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SwyftlyDbContext>>();

                services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
                services.AddDbContext<SwyftlyDbContext>((serviceProvider, options) =>
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
