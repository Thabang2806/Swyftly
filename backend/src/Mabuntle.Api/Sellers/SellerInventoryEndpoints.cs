using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Security;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerInventoryEndpoints
{
    private const int MaxBulkInventoryRows = 500;

    private static readonly string[] InventoryCsvHeaders =
    [
        "variantId",
        "sku",
        "barcode",
        "productTitle",
        "productSlug",
        "size",
        "colour",
        "price",
        "reservedQuantity",
        "availableQuantity",
        "stockQuantity",
        "status",
        "updatedAtUtc"
    ];

    public static IEndpointRouteBuilder MapSellerInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seller/inventory")
            .WithTags("Seller Inventory")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapGet("", ListInventoryAsync)
            .WithName("ListSellerInventory")
            .WithSummary("Lists owned product variants as flattened seller inventory rows.")
            .Produces<IReadOnlyCollection<SellerInventoryItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/export.csv", ExportInventoryCsvAsync)
            .WithName("ExportSellerInventoryCsv")
            .WithSummary("Exports owned product variants as CSV for stocktake updates.")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/import-template.csv", ExportInventoryTemplateCsvAsync)
            .WithName("ExportSellerInventoryImportTemplateCsv")
            .WithSummary("Exports a blank CSV template for bulk inventory imports.")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/history", ListInventoryHistoryAsync)
            .WithName("ListSellerInventoryHistory")
            .WithSummary("Lists seller inventory movement history across owned variants.")
            .Produces<IReadOnlyCollection<SellerInventoryMovementResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/import/preview", PreviewInventoryImportAsync)
            .WithName("PreviewSellerInventoryImport")
            .WithSummary("Previews a seller inventory CSV import without changing data.")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<SellerInventoryBulkAdjustmentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .DisableAntiforgery();

        group.MapPost("/bulk-adjust", BulkAdjustInventoryAsync)
            .WithName("BulkAdjustSellerInventory")
            .WithSummary("Applies validated bulk stock and status updates for owned variants.")
            .Produces<SellerInventoryBulkAdjustmentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{variantId:guid}/adjust", AdjustInventoryAsync)
            .WithName("AdjustSellerInventory")
            .WithSummary("Adjusts stock quantity and variant status for an owned product variant.")
            .Produces<SellerInventoryItemResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{variantId:guid}/history", ListVariantInventoryHistoryAsync)
            .WithName("ListSellerVariantInventoryHistory")
            .WithSummary("Lists seller inventory movement history for a single owned variant.")
            .Produces<IReadOnlyCollection<SellerInventoryMovementResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListInventoryAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var items = await CreateInventoryQuery(dbContext, seller.Id)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(items);
    }

    private static async Task<IResult> ExportInventoryCsvAsync(
        ClaimsPrincipal principal,
        HttpResponse response,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var items = await CreateInventoryQuery(dbContext, seller.Id)
            .ToListAsync(cancellationToken);

        response.Headers.ContentDisposition = "attachment; filename=\"mabuntle-inventory-export.csv\"";
        return HttpResults.Text(BuildInventoryCsv(items), "text/csv", Encoding.UTF8);
    }

    private static async Task<IResult> ExportInventoryTemplateCsvAsync(
        ClaimsPrincipal principal,
        HttpResponse response,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        response.Headers.ContentDisposition = "attachment; filename=\"mabuntle-inventory-import-template.csv\"";
        return HttpResults.Text(BuildInventoryCsv([]), "text/csv", Encoding.UTF8);
    }

    private static async Task<IResult> ListInventoryHistoryAsync(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? variantId,
        [FromQuery] string? sku,
        [FromQuery] string? barcode,
        [FromQuery] string? movementType,
        [FromQuery] Guid? orderId,
        [FromQuery] Guid? cartId,
        [FromQuery] Guid? reservationId,
        [FromQuery] Guid? paymentId,
        [FromQuery] Guid? returnRequestId,
        [FromQuery] Guid? refundId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
        {
            return Validation("dateRange", "fromUtc must be earlier than toUtc.");
        }

        InventoryMovementType? parsedMovementType = null;
        if (!string.IsNullOrWhiteSpace(movementType))
        {
            if (!Enum.TryParse<InventoryMovementType>(movementType, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Validation("movementType", $"Movement type must be one of: {string.Join(", ", Enum.GetNames<InventoryMovementType>())}.");
            }

            parsedMovementType = parsed;
        }

        var items = await CreateInventoryHistoryQuery(
                dbContext,
                seller.Id,
                productId,
                variantId,
                sku,
                barcode,
                parsedMovementType,
                orderId,
                cartId,
                reservationId,
                paymentId,
                returnRequestId,
                refundId,
                fromUtc,
                toUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(items.Select(MapMovementResponse).ToArray());
    }

    private static async Task<IResult> ListVariantInventoryHistoryAsync(
        Guid variantId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var ownsVariant = await CreateInventoryQuery(dbContext, seller.Id, variantId)
            .AnyAsync(cancellationToken);
        if (!ownsVariant)
        {
            return VariantNotFound();
        }

        var items = await CreateInventoryHistoryQuery(
                dbContext,
                seller.Id,
                productId: null,
                variantId,
                sku: null,
                barcode: null,
                movementType: null,
                orderId: null,
                cartId: null,
                reservationId: null,
                paymentId: null,
                returnRequestId: null,
                refundId: null,
                fromUtc: null,
                toUtc: null)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(items.Select(MapMovementResponse).ToArray());
    }

    private static async Task<IResult> PreviewInventoryImportAsync(
        IFormFile file,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (file.Length == 0)
        {
            return Validation("file", "Inventory import CSV cannot be empty.");
        }

        IReadOnlyCollection<BulkAdjustSellerInventoryItemRequest> items;
        try
        {
            items = await ParseInventoryCsvAsync(file, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return Validation("file", exception.Message);
        }

        if (items.Count > MaxBulkInventoryRows)
        {
            return Validation("file", $"Inventory import cannot contain more than {MaxBulkInventoryRows} rows.");
        }

        var response = await BuildBulkAdjustmentPreviewAsync(
            seller.Id,
            items,
            dbContext,
            cancellationToken);

        return HttpResults.Ok(response);
    }

    private static async Task<IResult> AdjustInventoryAsync(
        Guid variantId,
        AdjustSellerInventoryRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Validation("reason", "Inventory adjustment reason is required.");
        }

        if (!Enum.TryParse<ProductVariantStatus>(request.Status, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            return Validation("status", "Variant status must be Active, Inactive, or OutOfStock.");
        }

        var variant = await dbContext.ProductVariants
            .SingleOrDefaultAsync(
                item => item.Id == variantId
                    && dbContext.Products.Any(product => product.Id == item.ProductId && product.SellerId == seller.Id),
                cancellationToken);
        if (variant is null)
        {
            return VariantNotFound();
        }

        var previousValue = CreateAuditSnapshot(variant);
        var previousStockQuantity = variant.StockQuantity;
        var previousReservedQuantity = variant.ReservedQuantity;
        var previousStatus = variant.Status;

        try
        {
            variant.AdjustInventory(request.StockQuantity, status);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("inventory", exception.Message);
        }

        dbContext.InventoryMovements.Add(new InventoryMovement(
            seller.Id,
            variant.ProductId,
            variant.Id,
            InventoryMovementType.SellerAdjustment,
            previousStockQuantity,
            variant.StockQuantity,
            previousReservedQuantity,
            variant.ReservedQuantity,
            previousStatus,
            variant.Status,
            "SellerInventoryAdjust",
            reason,
            GetActorUserId(principal),
            batchReference: null,
            timeProvider.GetUtcNow()));

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                "Seller",
                "SellerInventoryAdjusted",
                "ProductVariant",
                variant.Id.ToString(),
                JsonSerializer.Serialize(previousValue),
                JsonSerializer.Serialize(CreateAuditSnapshot(variant)),
                reason),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await CreateInventoryQuery(dbContext, seller.Id, variant.Id)
            .SingleAsync(cancellationToken);

        return HttpResults.Ok(response);
    }

    private static async Task<IResult> BulkAdjustInventoryAsync(
        BulkAdjustSellerInventoryRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Validation("reason", "Bulk inventory adjustment reason is required.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Validation("items", "At least one inventory row is required.");
        }

        if (request.Items.Count > MaxBulkInventoryRows)
        {
            return Validation("items", $"Bulk inventory adjustment cannot contain more than {MaxBulkInventoryRows} rows.");
        }

        var preview = await BuildBulkAdjustmentPreviewAsync(
            seller.Id,
            request.Items,
            dbContext,
            cancellationToken);

        if (preview.ErrorRows > 0)
        {
            return HttpResults.BadRequest(preview);
        }

        var changeRows = preview.Rows
            .Where(row => row.RowStatus == InventoryImportRowStatus.Changed)
            .ToArray();

        if (changeRows.Length == 0)
        {
            return HttpResults.Ok(preview);
        }

        var variantIds = changeRows
            .Select(row => row.VariantId!.Value)
            .ToArray();
        var variants = await dbContext.ProductVariants
            .Where(variant => variantIds.Contains(variant.Id))
            .ToDictionaryAsync(variant => variant.Id, cancellationToken);
        var batchReference = $"bulk-{Guid.NewGuid():N}";

        foreach (var row in changeRows)
        {
            var variant = variants[row.VariantId!.Value];
            var previousValue = CreateAuditSnapshot(variant);
            var previousStockQuantity = variant.StockQuantity;
            var previousReservedQuantity = variant.ReservedQuantity;
            var previousStatus = variant.Status;
            variant.AdjustInventory(row.ProposedStockQuantity!.Value, Enum.Parse<ProductVariantStatus>(row.ProposedStatus!, ignoreCase: true));

            dbContext.InventoryMovements.Add(new InventoryMovement(
                seller.Id,
                variant.ProductId,
                variant.Id,
                InventoryMovementType.BulkImportAdjustment,
                previousStockQuantity,
                variant.StockQuantity,
                previousReservedQuantity,
                variant.ReservedQuantity,
                previousStatus,
                variant.Status,
                "SellerInventoryBulkImport",
                reason,
                GetActorUserId(principal),
                batchReference,
                timeProvider.GetUtcNow()));

            await auditLogService.RecordAsync(
                new CreateAuditLogEntry(
                    principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    "Seller",
                    "SellerInventoryBulkAdjusted",
                    "ProductVariant",
                    variant.Id.ToString(),
                    JsonSerializer.Serialize(previousValue),
                    JsonSerializer.Serialize(CreateAuditSnapshot(variant)),
                    reason),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(preview);
    }

    private static async Task<SellerInventoryBulkAdjustmentResponse> BuildBulkAdjustmentPreviewAsync(
        Guid sellerId,
        IReadOnlyCollection<BulkAdjustSellerInventoryItemRequest> items,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var inventory = await CreateInventoryQuery(dbContext, sellerId)
            .ToListAsync(cancellationToken);
        var byVariantId = inventory.ToDictionary(item => item.VariantId);
        var bySku = inventory
            .GroupBy(item => item.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var byBarcode = inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .GroupBy(item => item.Barcode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var seenTargets = new HashSet<Guid>();
        var rows = new List<SellerInventoryBulkAdjustmentRowResponse>();
        var rowNumber = 1;

        foreach (var item in items)
        {
            var messages = new List<string>();
            SellerInventoryItemResponse? target = null;
            rowNumber++;

            if (item.VariantId.HasValue)
            {
                byVariantId.TryGetValue(item.VariantId.Value, out target);
                if (target is null)
                {
                    messages.Add("Variant id was not found for this seller.");
                }
            }

            var sku = item.Sku?.Trim();
            var barcode = item.Barcode?.Trim();
            if (!string.IsNullOrWhiteSpace(sku))
            {
                if (!bySku.TryGetValue(sku, out var skuMatches))
                {
                    messages.Add("SKU was not found for this seller.");
                }
                else if (skuMatches.Length > 1)
                {
                    messages.Add("SKU matches multiple variants; use variantId for this row.");
                }
                else if (target is not null && skuMatches[0].VariantId != target.VariantId)
                {
                    messages.Add("Variant id and SKU refer to different variants.");
                }
                else
                {
                    target ??= skuMatches[0];
                }
            }

            if (!string.IsNullOrWhiteSpace(barcode))
            {
                if (!byBarcode.TryGetValue(barcode, out var barcodeMatches))
                {
                    messages.Add("Barcode was not found for this seller.");
                }
                else if (barcodeMatches.Length > 1)
                {
                    messages.Add("Barcode matches multiple variants; use variantId for this row.");
                }
                else if (target is not null && barcodeMatches[0].VariantId != target.VariantId)
                {
                    messages.Add("Variant id, SKU, and barcode must refer to the same variant.");
                }
                else
                {
                    target ??= barcodeMatches[0];
                }
            }

            if (!item.VariantId.HasValue && string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(barcode))
            {
                messages.Add("Either variantId, SKU, or barcode is required.");
            }

            if (!Enum.TryParse<ProductVariantStatus>(item.Status, ignoreCase: true, out var parsedStatus)
                || !Enum.IsDefined(parsedStatus))
            {
                messages.Add("Status must be Active, Inactive, or OutOfStock.");
            }

            if (item.StockQuantity < 0)
            {
                messages.Add("Stock quantity cannot be negative.");
            }

            if (target is not null)
            {
                if (!seenTargets.Add(target.VariantId))
                {
                    messages.Add("This variant appears more than once in the import.");
                }

                if (item.StockQuantity < target.ReservedQuantity)
                {
                    messages.Add($"Stock quantity cannot be lower than reserved quantity ({target.ReservedQuantity}).");
                }
            }

            var rowStatus = InventoryImportRowStatus.Error;
            if (messages.Count == 0 && target is not null)
            {
                rowStatus = target.StockQuantity == item.StockQuantity
                    && string.Equals(target.VariantStatus, parsedStatus.ToString(), StringComparison.OrdinalIgnoreCase)
                        ? InventoryImportRowStatus.Unchanged
                        : InventoryImportRowStatus.Changed;
            }

            rows.Add(new SellerInventoryBulkAdjustmentRowResponse(
                rowNumber,
                target?.VariantId ?? item.VariantId,
                string.IsNullOrWhiteSpace(sku) ? target?.Sku : sku,
                string.IsNullOrWhiteSpace(barcode) ? target?.Barcode : barcode,
                target?.ProductId,
                target?.ProductTitle,
                target?.ProductSlug,
                target?.Size,
                target?.Colour,
                target?.StockQuantity,
                target?.ReservedQuantity,
                target?.VariantStatus,
                item.StockQuantity,
                item.Status,
                rowStatus,
                messages));
        }

        var errorRows = rows.Count(row => row.RowStatus == InventoryImportRowStatus.Error);
        var changedRows = rows.Count(row => row.RowStatus == InventoryImportRowStatus.Changed);
        var unchangedRows = rows.Count(row => row.RowStatus == InventoryImportRowStatus.Unchanged);

        return new SellerInventoryBulkAdjustmentResponse(
            rows.Count,
            rows.Count - errorRows,
            errorRows,
            changedRows,
            unchangedRows,
            rows);
    }

    private static IQueryable<SellerInventoryItemResponse> CreateInventoryQuery(
        MabuntleDbContext dbContext,
        Guid sellerId,
        Guid? variantId = null)
    {
        var query = dbContext.ProductVariants
            .AsNoTracking()
            .Join(
                dbContext.Products.AsNoTracking(),
                variant => variant.ProductId,
                product => product.Id,
                (variant, product) => new { Variant = variant, Product = product })
            .Where(item => item.Product.SellerId == sellerId);

        if (variantId.HasValue)
        {
            query = query.Where(item => item.Variant.Id == variantId.Value);
        }

        return query
            .OrderBy(item => item.Product.Title)
            .ThenBy(item => item.Variant.Sku)
            .Select(item => new SellerInventoryItemResponse(
                item.Product.Id,
                item.Variant.Id,
                item.Product.Title ?? string.Empty,
                item.Product.Slug ?? string.Empty,
                item.Product.Status.ToString(),
                dbContext.ProductImages
                    .Where(image => image.ProductId == item.Product.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault(),
                dbContext.ProductImages
                    .Where(image => image.ProductId == item.Product.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.AltText)
                    .FirstOrDefault(),
                item.Variant.Sku,
                item.Variant.Size,
                item.Variant.Colour,
                item.Variant.Price,
                item.Variant.StockQuantity,
                item.Variant.ReservedQuantity,
                item.Variant.StockQuantity - item.Variant.ReservedQuantity,
                item.Variant.Status.ToString(),
                item.Variant.Barcode,
                item.Variant.UpdatedAtUtc));
    }

    private static IQueryable<SellerInventoryMovementProjection> CreateInventoryHistoryQuery(
        MabuntleDbContext dbContext,
        Guid sellerId,
        Guid? productId,
        Guid? variantId,
        string? sku,
        string? barcode,
        InventoryMovementType? movementType,
        Guid? orderId,
        Guid? cartId,
        Guid? reservationId,
        Guid? paymentId,
        Guid? returnRequestId,
        Guid? refundId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        var query = dbContext.InventoryMovements
            .AsNoTracking()
            .Join(
                dbContext.ProductVariants.AsNoTracking(),
                movement => movement.ProductVariantId,
                variant => variant.Id,
                (movement, variant) => new { Movement = movement, Variant = variant })
            .Join(
                dbContext.Products.AsNoTracking(),
                item => item.Movement.ProductId,
                product => product.Id,
                (item, product) => new { item.Movement, item.Variant, Product = product })
            .Where(item => item.Movement.SellerId == sellerId);

        if (productId.HasValue)
        {
            query = query.Where(item => item.Movement.ProductId == productId.Value);
        }

        if (variantId.HasValue)
        {
            query = query.Where(item => item.Movement.ProductVariantId == variantId.Value);
        }

        var trimmedSku = sku?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSku))
        {
            query = query.Where(item => item.Variant.Sku == trimmedSku);
        }

        var trimmedBarcode = barcode?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedBarcode))
        {
            query = query.Where(item => item.Variant.Barcode == trimmedBarcode);
        }

        if (movementType.HasValue)
        {
            query = query.Where(item => item.Movement.MovementType == movementType.Value);
        }

        if (orderId.HasValue)
        {
            query = query.Where(item => item.Movement.OrderId == orderId.Value);
        }

        if (cartId.HasValue)
        {
            query = query.Where(item => item.Movement.CartId == cartId.Value);
        }

        if (reservationId.HasValue)
        {
            query = query.Where(item => item.Movement.ReservationId == reservationId.Value);
        }

        if (paymentId.HasValue)
        {
            query = query.Where(item => item.Movement.PaymentId == paymentId.Value);
        }

        if (returnRequestId.HasValue)
        {
            query = query.Where(item => item.Movement.ReturnRequestId == returnRequestId.Value);
        }

        if (refundId.HasValue)
        {
            query = query.Where(item => item.Movement.RefundId == refundId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.Movement.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.Movement.OccurredAtUtc <= toUtc.Value);
        }

        return query
            .OrderByDescending(item => item.Movement.OccurredAtUtc)
            .ThenBy(item => item.Product.Title)
            .ThenBy(item => item.Variant.Sku)
            .Select(item => new SellerInventoryMovementProjection(
                item.Movement.Id,
                item.Movement.ProductId,
                item.Movement.ProductVariantId,
                item.Product.Title ?? string.Empty,
                item.Product.Slug ?? string.Empty,
                item.Variant.Sku,
                item.Variant.Barcode,
                item.Variant.Size,
                item.Variant.Colour,
                item.Movement.MovementType.ToString(),
                item.Movement.StockQuantityBefore,
                item.Movement.StockQuantityAfter,
                item.Movement.ReservedQuantityBefore,
                item.Movement.ReservedQuantityAfter,
                item.Movement.QuantityDelta,
                item.Movement.ReservedQuantityAfter - item.Movement.ReservedQuantityBefore,
                item.Movement.StatusBefore.ToString(),
                item.Movement.StatusAfter.ToString(),
                item.Movement.Source,
                item.Movement.Reason,
                item.Movement.ActorUserId,
                item.Movement.BatchReference,
                item.Movement.CartId,
                item.Movement.OrderId,
                item.Movement.ReservationId,
                item.Movement.PaymentId,
                item.Movement.ReturnRequestId,
                item.Movement.RefundId,
                item.Movement.OccurredAtUtc));
    }

    private static SellerInventoryMovementResponse MapMovementResponse(SellerInventoryMovementProjection movement) =>
        new(
            movement.MovementId,
            movement.ProductId,
            movement.VariantId,
            movement.ProductTitle,
            movement.ProductSlug,
            movement.Sku,
            movement.Barcode,
            movement.Size,
            movement.Colour,
            movement.MovementType,
            movement.StockQuantityBefore,
            movement.StockQuantityAfter,
            movement.ReservedQuantityBefore,
            movement.ReservedQuantityAfter,
            movement.QuantityDelta,
            movement.ReservedQuantityDelta,
            movement.StatusBefore,
            movement.StatusAfter,
            movement.Source,
            movement.Reason,
            movement.ActorUserId,
            movement.BatchReference,
            movement.CartId,
            movement.OrderId,
            movement.ReservationId,
            movement.PaymentId,
            movement.ReturnRequestId,
            movement.RefundId,
            BuildRelatedRoute(movement),
            movement.OccurredAtUtc);

    private static string? BuildRelatedRoute(SellerInventoryMovementProjection movement)
    {
        if (movement.ReturnRequestId.HasValue)
        {
            return $"/seller/returns/{movement.ReturnRequestId.Value}";
        }

        return movement.OrderId.HasValue ? $"/seller/orders/{movement.OrderId.Value}" : null;
    }

    private sealed record SellerInventoryMovementProjection(
        Guid MovementId,
        Guid ProductId,
        Guid VariantId,
        string ProductTitle,
        string ProductSlug,
        string Sku,
        string? Barcode,
        string Size,
        string Colour,
        string MovementType,
        int StockQuantityBefore,
        int StockQuantityAfter,
        int ReservedQuantityBefore,
        int ReservedQuantityAfter,
        int QuantityDelta,
        int ReservedQuantityDelta,
        string StatusBefore,
        string StatusAfter,
        string Source,
        string Reason,
        Guid? ActorUserId,
        string? BatchReference,
        Guid? CartId,
        Guid? OrderId,
        Guid? ReservationId,
        Guid? PaymentId,
        Guid? ReturnRequestId,
        Guid? RefundId,
        DateTimeOffset OccurredAtUtc);

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return await dbContext.SellerProfiles
            .SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken);
    }

    private static InventoryAuditSnapshot CreateAuditSnapshot(ProductVariant variant) =>
        new(
            variant.StockQuantity,
            variant.ReservedQuantity,
            variant.AvailableQuantity,
            variant.Status.ToString());

    private static Guid? GetActorUserId(ClaimsPrincipal principal)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private static string BuildInventoryCsv(IReadOnlyCollection<SellerInventoryItemResponse> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", InventoryCsvHeaders.Select(Csv)));

        foreach (var item in items)
        {
            AppendCsvLine(
                builder,
                item.VariantId.ToString(),
                item.Sku,
                item.Barcode ?? string.Empty,
                item.ProductTitle ?? string.Empty,
                item.ProductSlug ?? string.Empty,
                item.Size,
                item.Colour,
                item.Price.ToString(CultureInfo.InvariantCulture),
                item.ReservedQuantity.ToString(CultureInfo.InvariantCulture),
                item.AvailableQuantity.ToString(CultureInfo.InvariantCulture),
                item.StockQuantity.ToString(CultureInfo.InvariantCulture),
                item.VariantStatus,
                item.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static async Task<IReadOnlyCollection<BulkAdjustSellerInventoryItemRequest>> ParseInventoryCsvAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var records = ParseCsv(content);

        if (records.Count == 0)
        {
            throw new InvalidOperationException("Inventory import CSV must include a header row.");
        }

        var headers = records[0]
            .Select((header, index) => new { Header = header.Trim(), Index = index })
            .Where(item => item.Header.Length > 0)
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        foreach (var requiredHeader in new[] { "stockQuantity", "status" })
        {
            if (!headers.ContainsKey(requiredHeader))
            {
                throw new InvalidOperationException($"Inventory import CSV is missing the '{requiredHeader}' column.");
            }
        }

        if (!headers.ContainsKey("variantId") && !headers.ContainsKey("sku") && !headers.ContainsKey("barcode"))
        {
            throw new InvalidOperationException("Inventory import CSV must include either a 'variantId', 'sku', or 'barcode' column.");
        }

        var items = new List<BulkAdjustSellerInventoryItemRequest>();
        foreach (var record in records.Skip(1))
        {
            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var variantIdText = ReadColumn(record, headers, "variantId");
            Guid? variantId = null;
            if (!string.IsNullOrWhiteSpace(variantIdText) && Guid.TryParse(variantIdText, out var parsedVariantId))
            {
                variantId = parsedVariantId;
            }

            var stockQuantityText = ReadColumn(record, headers, "stockQuantity");
            var stockQuantity = int.TryParse(stockQuantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStockQuantity)
                ? parsedStockQuantity
                : -1;

            items.Add(new BulkAdjustSellerInventoryItemRequest(
                variantId,
                ReadColumn(record, headers, "sku"),
                stockQuantity,
                ReadColumn(record, headers, "status") ?? string.Empty,
                ReadColumn(record, headers, "barcode")));
        }

        return items;
    }

    private static List<string[]> ParseCsv(string content)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < content.Length && content[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && character == ',')
            {
                row.Add(value.ToString());
                value.Clear();
                continue;
            }

            if (!inQuotes && (character == '\r' || character == '\n'))
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(value.ToString());
                value.Clear();
                rows.Add(row.ToArray());
                row.Clear();
                continue;
            }

            value.Append(character);
        }

        if (inQuotes)
        {
            throw new InvalidOperationException("Inventory import CSV contains an unterminated quoted value.");
        }

        if (value.Length > 0 || row.Count > 0)
        {
            row.Add(value.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static string? ReadColumn(
        IReadOnlyList<string> record,
        IReadOnlyDictionary<string, int> headers,
        string header)
    {
        if (!headers.TryGetValue(header, out var index) || index >= record.Count)
        {
            return null;
        }

        return record[index].Trim();
    }

    private static void AppendCsvLine(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(Csv)));
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerInventory.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult VariantNotFound() =>
        HttpResults.Problem(
            title: "SellerInventory.VariantNotFound",
            detail: "Product variant was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private sealed record InventoryAuditSnapshot(
        int StockQuantity,
        int ReservedQuantity,
        int AvailableQuantity,
        string Status);
}

public sealed record AdjustSellerInventoryRequest(
    int StockQuantity,
    string Status,
    string? Reason);

public sealed record BulkAdjustSellerInventoryRequest(
    string? Reason,
    IReadOnlyCollection<BulkAdjustSellerInventoryItemRequest>? Items);

public sealed record BulkAdjustSellerInventoryItemRequest(
    Guid? VariantId,
    string? Sku,
    int StockQuantity,
    string Status,
    string? Barcode = null);

public sealed record SellerInventoryItemResponse(
    Guid ProductId,
    Guid VariantId,
    string? ProductTitle,
    string? ProductSlug,
    string ProductStatus,
    string? PrimaryImageUrl,
    string? PrimaryImageAltText,
    string Sku,
    string Size,
    string Colour,
    decimal Price,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    string VariantStatus,
    string? Barcode,
    DateTimeOffset UpdatedAtUtc);

public sealed record SellerInventoryMovementResponse(
    Guid MovementId,
    Guid ProductId,
    Guid VariantId,
    string ProductTitle,
    string ProductSlug,
    string Sku,
    string? Barcode,
    string Size,
    string Colour,
    string MovementType,
    int StockQuantityBefore,
    int StockQuantityAfter,
    int ReservedQuantityBefore,
    int ReservedQuantityAfter,
    int QuantityDelta,
    int ReservedQuantityDelta,
    string StatusBefore,
    string StatusAfter,
    string Source,
    string Reason,
    Guid? ActorUserId,
    string? BatchReference,
    Guid? CartId,
    Guid? OrderId,
    Guid? ReservationId,
    Guid? PaymentId,
    Guid? ReturnRequestId,
    Guid? RefundId,
    string? RelatedRoute,
    DateTimeOffset OccurredAtUtc);

public sealed record SellerInventoryBulkAdjustmentResponse(
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int ChangedRows,
    int UnchangedRows,
    IReadOnlyCollection<SellerInventoryBulkAdjustmentRowResponse> Rows);

public sealed record SellerInventoryBulkAdjustmentRowResponse(
    int RowNumber,
    Guid? VariantId,
    string? Sku,
    string? Barcode,
    Guid? ProductId,
    string? ProductTitle,
    string? ProductSlug,
    string? Size,
    string? Colour,
    int? CurrentStockQuantity,
    int? CurrentReservedQuantity,
    string? CurrentStatus,
    int? ProposedStockQuantity,
    string? ProposedStatus,
    string RowStatus,
    IReadOnlyCollection<string> Messages);

public static class InventoryImportRowStatus
{
    public const string Changed = "Changed";
    public const string Unchanged = "Unchanged";
    public const string Error = "Error";
}
