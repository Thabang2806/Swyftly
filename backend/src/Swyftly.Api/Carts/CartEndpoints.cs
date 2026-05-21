using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Identity;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Carts;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cart")
            .WithTags("Cart")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        group.MapGet("", GetCartAsync)
            .WithName("GetCart")
            .WithSummary("Returns the active cart for the authenticated buyer.")
            .Produces<CartResponse>(StatusCodes.Status200OK);

        group.MapPost("/items", AddItemAsync)
            .WithName("AddCartItem")
            .WithSummary("Adds a published product variant to the authenticated buyer's active cart.")
            .Produces<CartResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/shipping-options", GetShippingOptionsAsync)
            .WithName("GetCartShippingOptions")
            .WithSummary("Returns seller-managed delivery methods available for the active cart and selected address.")
            .Produces<CartShippingOptionsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/items/{itemId:guid}", UpdateItemAsync)
            .WithName("UpdateCartItem")
            .WithSummary("Updates a cart item quantity.")
            .Produces<CartResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/items/{itemId:guid}", DeleteItemAsync)
            .WithName("DeleteCartItem")
            .WithSummary("Removes an item from the authenticated buyer's active cart.")
            .Produces<CartResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/items/{itemId:guid}/move-to-wishlist", MoveItemToWishlistAsync)
            .WithName("MoveCartItemToWishlist")
            .WithSummary("Moves a cart item to the authenticated buyer's wishlist.")
            .Produces<CartResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("", ClearCartAsync)
            .WithName("ClearCart")
            .WithSummary("Clears the authenticated buyer's active cart.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetCartAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> AddItemAsync(
        AddCartItemRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var productVariant = await dbContext.ProductVariants.SingleOrDefaultAsync(
            variant => variant.Id == request.ProductVariantId,
            cancellationToken);
        if (productVariant is null)
        {
            return VariantNotFound();
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == productVariant.ProductId,
            cancellationToken);
        if (product is null || product.Status != ProductStatus.Published)
        {
            return ProductNotAvailable();
        }

        if (productVariant.Status != ProductVariantStatus.Active)
        {
            return Validation("productVariantId", "Product variant is not available.");
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null)
        {
            cart = new Cart(buyer.Id);
            dbContext.Carts.Add(cart);
        }

        try
        {
            cart.AddOrUpdateItem(
                product.Id,
                productVariant.Id,
                product.SellerId,
                product.Title,
                productVariant.Sku,
                productVariant.Size,
                productVariant.Colour,
                productVariant.Price,
                request.Quantity,
                productVariant.AvailableQuantity);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("cart", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetShippingOptionsAsync(
        CartShippingOptionsRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (request.CartId == Guid.Empty)
        {
            return Validation("cartId", "Cart id cannot be empty.");
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null || (request.CartId.HasValue && cart.Id != request.CartId.Value))
        {
            return CartNotFound();
        }

        if (cart.Items.Count == 0)
        {
            return Validation("cart", "Cart must contain at least one item before shipping can be quoted.");
        }

        if (!cart.SellerId.HasValue)
        {
            return Validation("cart", "Cart must be associated with a seller before shipping can be quoted.");
        }

        var deliveryAddressResult = await ResolveDeliveryAddressAsync(
            buyer.Id,
            request.DeliveryAddressId,
            request.DeliveryAddress,
            dbContext,
            cancellationToken);
        if (deliveryAddressResult.Error is not null)
        {
            return deliveryAddressResult.Error;
        }

        var deliveryAddress = deliveryAddressResult.Address!;
        var candidateMethods = await dbContext.SellerDeliveryMethods
            .AsNoTracking()
            .Where(method => method.SellerId == cart.SellerId.Value
                && method.IsActive
                && method.CountryCode == deliveryAddress.CountryCode)
            .ToListAsync(cancellationToken);

        var options = candidateMethods
            .Where(method => method.MatchesAddress(deliveryAddress.CountryCode, deliveryAddress.Province))
            .OrderBy(method => method.Province is null ? 1 : 0)
            .ThenBy(method => method.DisplayOrder)
            .ThenBy(method => method.Name)
            .Select(method =>
            {
                var shippingAmount = method.CalculateShippingAmount(cart.Subtotal);
                return new CartShippingOptionResponse(
                    method.Id,
                    method.Name,
                    method.Description,
                    method.MethodType.ToString(),
                    method.CountryCode,
                    method.Province,
                    method.BasePrice,
                    method.FreeShippingThreshold,
                    shippingAmount,
                    method.FreeShippingThreshold.HasValue && shippingAmount == 0,
                    method.EstimatedMinDays,
                    method.EstimatedMaxDays,
                    method.DisplayOrder);
            })
            .ToArray();

        if (options.Length == 0)
        {
            return Validation("deliveryAddress", "No active delivery method serves the selected delivery address.");
        }

        return HttpResults.Ok(new CartShippingOptionsResponse(
            cart.Id,
            cart.SellerId.Value,
            cart.Subtotal,
            options));
    }

    private static async Task<IResult> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null || cart.Items.All(item => item.Id != itemId))
        {
            return CartItemNotFound();
        }

        var cartItem = cart.Items.Single(item => item.Id == itemId);
        var variant = await dbContext.ProductVariants.SingleAsync(
            variant => variant.Id == cartItem.ProductVariantId,
            cancellationToken);

        try
        {
            cart.SetItemQuantity(itemId, request.Quantity, variant.AvailableQuantity);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("quantity", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> DeleteItemAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null || cart.Items.All(item => item.Id != itemId))
        {
            return CartItemNotFound();
        }

        try
        {
            cart.RemoveItem(itemId);
        }
        catch (InvalidOperationException exception)
        {
            return Validation("cart", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> MoveItemToWishlistAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null || cart.Items.All(item => item.Id != itemId))
        {
            return CartItemNotFound();
        }

        var cartItem = cart.Items.Single(item => item.Id == itemId);
        if (!await IsProductPubliclyVisibleAsync(cartItem.ProductId, dbContext, cancellationToken))
        {
            return ProductNotAvailable();
        }

        var wishlistExists = await dbContext.BuyerWishlistItems.AnyAsync(
            item => item.BuyerId == buyer.Id && item.ProductId == cartItem.ProductId,
            cancellationToken);
        if (!wishlistExists)
        {
            dbContext.BuyerWishlistItems.Add(new BuyerWishlistItem(
                buyer.Id,
                cartItem.ProductId,
                timeProvider.GetUtcNow()));
        }

        cart.RemoveItem(itemId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> ClearCartAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is not null)
        {
            dbContext.Carts.Remove(cart);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return HttpResults.NoContent();
    }

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static async Task<Cart?> GetActiveCartAsync(
        Guid buyerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.Carts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.BuyerId == buyerId && cart.Status == CartStatus.Active,
                cancellationToken);

    public static async Task<CartResponse> CreateCartResponseAsync(
        Cart? cart,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (cart is null)
        {
            return CartResponse.Empty;
        }

        var sellerStoreName = cart.SellerId.HasValue
            ? await dbContext.SellerStorefronts
                .Where(storefront => storefront.SellerId == cart.SellerId.Value)
                .Select(storefront => storefront.StoreName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var productIds = cart.Items.Select(item => item.ProductId).Distinct().ToArray();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        var primaryImages = await dbContext.ProductImages
            .AsNoTracking()
            .Where(image => productIds.Contains(image.ProductId))
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .ToListAsync(cancellationToken);
        var imageByProductId = primaryImages
            .GroupBy(image => image.ProductId)
            .ToDictionary(group => group.Key, group => group.First());

        var items = cart.Items
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item =>
            {
                products.TryGetValue(item.ProductId, out var product);
                imageByProductId.TryGetValue(item.ProductId, out var image);

                return new CartItemResponse(
                    item.Id,
                    item.ProductId,
                    item.ProductVariantId,
                    item.ProductTitle,
                    product?.Slug,
                    image?.Url,
                    image?.AltText,
                    item.Sku,
                    item.Size,
                    item.Colour,
                    item.UnitPrice,
                    item.Quantity,
                    item.LineTotal);
            })
            .ToArray();

        return new CartResponse(
            cart.Id,
            cart.BuyerId,
            cart.SellerId,
            sellerStoreName,
            items,
            cart.TotalQuantity,
            cart.Subtotal);
    }

    private static async Task<bool> IsProductPubliclyVisibleAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == productId
                && product.Status == ProductStatus.Published
                && dbContext.SellerProfiles.Any(seller =>
                    seller.Id == product.SellerId
                    && seller.VerificationStatus == SellerVerificationStatus.Verified)
                && dbContext.SellerStorefronts.Any(storefront =>
                    storefront.SellerId == product.SellerId
                    && storefront.IsPublished),
                cancellationToken);

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Cart.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult VariantNotFound() =>
        HttpResults.Problem(
            title: "Cart.ProductVariantNotFound",
            detail: "Product variant was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult CartItemNotFound() =>
        HttpResults.Problem(
            title: "Cart.ItemNotFound",
            detail: "Cart item was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ProductNotAvailable() =>
        HttpResults.Problem(
            title: "Cart.ProductNotAvailable",
            detail: "Product is not available for purchase.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult CartNotFound() =>
        HttpResults.Problem(
            title: "Cart.NotFound",
            detail: "Active cart was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static async Task<(OrderDeliveryAddress? Address, IResult? Error)> ResolveDeliveryAddressAsync(
        Guid buyerId,
        Guid? deliveryAddressId,
        CartShippingDeliveryAddressRequest? deliveryAddress,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (deliveryAddressId.HasValue && deliveryAddressId.Value == Guid.Empty)
        {
            return (null, Validation("deliveryAddressId", "Delivery address id cannot be empty."));
        }

        if (deliveryAddressId.HasValue && deliveryAddress is not null)
        {
            return (null, Validation("deliveryAddress", "Provide either a saved delivery address id or an inline delivery address, not both."));
        }

        if (!deliveryAddressId.HasValue && deliveryAddress is null)
        {
            return (null, Validation("deliveryAddress", "A delivery address is required."));
        }

        if (deliveryAddressId.HasValue)
        {
            var saved = await dbContext.BuyerDeliveryAddresses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    address => address.Id == deliveryAddressId.Value && address.BuyerId == buyerId,
                    cancellationToken);

            return saved is null
                ? (null, HttpResults.Problem(
                    title: "Cart.DeliveryAddressNotFound",
                    detail: "Delivery address was not found.",
                    statusCode: StatusCodes.Status404NotFound))
                : (ToDeliveryAddress(saved), null);
        }

        try
        {
            return (ToDeliveryAddress(buyerId, deliveryAddress!), null);
        }
        catch (ArgumentException exception)
        {
            return (null, Validation(ToCamelCase(exception.ParamName ?? "deliveryAddress"), exception.Message));
        }
    }

    private static OrderDeliveryAddress ToDeliveryAddress(BuyerDeliveryAddress address) =>
        new(
            address.RecipientName,
            address.PhoneNumber,
            address.AddressLine1,
            address.AddressLine2,
            address.Suburb,
            address.City,
            address.Province,
            address.PostalCode,
            address.CountryCode,
            address.DeliveryInstructions);

    private static OrderDeliveryAddress ToDeliveryAddress(
        Guid buyerId,
        CartShippingDeliveryAddressRequest request)
    {
        var address = new BuyerDeliveryAddress(
            buyerId,
            "Checkout",
            request.RecipientName,
            request.PhoneNumber,
            request.AddressLine1,
            request.AddressLine2,
            request.Suburb,
            request.City,
            request.Province,
            request.PostalCode,
            request.CountryCode,
            isDefault: false,
            request.DeliveryInstructions);

        return ToDeliveryAddress(address);
    }

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}

public sealed record AddCartItemRequest(
    Guid ProductVariantId,
    int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);

public sealed record CartShippingOptionsRequest(
    Guid? CartId,
    Guid? DeliveryAddressId = null,
    CartShippingDeliveryAddressRequest? DeliveryAddress = null);

public sealed record CartShippingDeliveryAddressRequest(
    string RecipientName,
    string PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    string? DeliveryInstructions = null);

public sealed record CartShippingOptionsResponse(
    Guid CartId,
    Guid SellerId,
    decimal CartSubtotal,
    IReadOnlyCollection<CartShippingOptionResponse> Options);

public sealed record CartShippingOptionResponse(
    Guid DeliveryMethodId,
    string Name,
    string? Description,
    string MethodType,
    string CountryCode,
    string? Province,
    decimal BasePrice,
    decimal? FreeShippingThreshold,
    decimal ShippingAmount,
    bool FreeShippingApplied,
    int EstimatedMinDays,
    int EstimatedMaxDays,
    int DisplayOrder);

public sealed record CartResponse(
    Guid? CartId,
    Guid? BuyerId,
    Guid? SellerId,
    string? SellerStoreName,
    IReadOnlyCollection<CartItemResponse> Items,
    int TotalQuantity,
    decimal Subtotal)
{
    public static CartResponse Empty => new(null, null, null, null, [], 0, 0);
}

public sealed record CartItemResponse(
    Guid CartItemId,
    Guid ProductId,
    Guid ProductVariantId,
    string? ProductTitle,
    string? ProductSlug,
    string? PrimaryImageUrl,
    string? PrimaryImageAltText,
    string Sku,
    string Size,
    string Colour,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
