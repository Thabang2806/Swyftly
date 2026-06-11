using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Carts;

public sealed class Cart : AuditableEntity
{
    private readonly List<CartItem> _items = [];

    private Cart()
    {
    }

    public Cart(Guid buyerId)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        BuyerId = buyerId;
        Status = CartStatus.Active;
    }

    public Guid BuyerId { get; private set; }

    public Guid? SellerId { get; private set; }

    public CartStatus Status { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public decimal Subtotal => _items.Sum(item => item.LineTotal);

    public int TotalQuantity => _items.Sum(item => item.Quantity);

    public void AddOrUpdateItem(
        Guid productId,
        Guid productVariantId,
        Guid sellerId,
        string? productTitle,
        string sku,
        string size,
        string colour,
        decimal unitPrice,
        int quantity,
        int availableQuantity)
    {
        ValidateActive();
        ValidateQuantity(quantity, availableQuantity);
        EnsureSeller(sellerId);

        var existing = _items.SingleOrDefault(item => item.ProductVariantId == productVariantId);
        if (existing is null)
        {
            _items.Add(new CartItem(
                Id,
                productId,
                productVariantId,
                productTitle,
                sku,
                size,
                colour,
                unitPrice,
                quantity));
            return;
        }

        var requestedQuantity = existing.Quantity + quantity;
        ValidateQuantity(requestedQuantity, availableQuantity);
        existing.UpdateQuantity(requestedQuantity, availableQuantity);
    }

    public void SetItemQuantity(Guid cartItemId, int quantity, int availableQuantity)
    {
        ValidateActive();
        var item = GetRequiredItem(cartItemId);
        item.UpdateQuantity(quantity, availableQuantity);
    }

    public void RemoveItem(Guid cartItemId)
    {
        ValidateActive();
        var item = GetRequiredItem(cartItemId);
        _items.Remove(item);
        ClearSellerIfEmpty();
    }

    public void Clear()
    {
        ValidateActive();
        _items.Clear();
        SellerId = null;
    }

    public void MarkCheckedOut()
    {
        ValidateActive();
        Status = CartStatus.CheckedOut;
    }

    private CartItem GetRequiredItem(Guid cartItemId) =>
        _items.SingleOrDefault(item => item.Id == cartItemId)
        ?? throw new InvalidOperationException("Cart item was not found.");

    private void EnsureSeller(Guid sellerId)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (SellerId.HasValue && SellerId.Value != sellerId)
        {
            throw new InvalidOperationException("Cart can contain products from only one seller.");
        }

        SellerId ??= sellerId;
    }

    private static void ValidateQuantity(int quantity, int availableQuantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (quantity > availableQuantity)
        {
            throw new InvalidOperationException("Quantity cannot exceed available stock.");
        }
    }

    private void ClearSellerIfEmpty()
    {
        if (_items.Count == 0)
        {
            SellerId = null;
        }
    }

    private void ValidateActive()
    {
        if (Status != CartStatus.Active)
        {
            throw new InvalidOperationException("Only active carts can be changed.");
        }
    }
}
