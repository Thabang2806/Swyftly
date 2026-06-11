using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Returns;

public sealed class ReturnItem : Entity
{
    private ReturnItem()
    {
    }

    public ReturnItem(
        Guid returnRequestId,
        Guid orderItemId,
        Guid productId,
        Guid productVariantId,
        int quantity,
        ReturnReason reason,
        bool isOpenedOrUnsealed,
        string? note)
    {
        if (returnRequestId == Guid.Empty)
        {
            throw new ArgumentException("Return request id is required.", nameof(returnRequestId));
        }

        if (orderItemId == Guid.Empty)
        {
            throw new ArgumentException("Order item id is required.", nameof(orderItemId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (productVariantId == Guid.Empty)
        {
            throw new ArgumentException("Product variant id is required.", nameof(productVariantId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        ReturnRequestId = returnRequestId;
        OrderItemId = orderItemId;
        ProductId = productId;
        ProductVariantId = productVariantId;
        Quantity = quantity;
        Reason = reason;
        IsOpenedOrUnsealed = isOpenedOrUnsealed;
        Note = OptionalText(note, maxLength: 1000);
    }

    public Guid ReturnRequestId { get; private set; }

    public Guid OrderItemId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public int Quantity { get; private set; }

    public ReturnReason Reason { get; private set; }

    public bool IsOpenedOrUnsealed { get; private set; }

    public string? Note { get; private set; }

    private static string? OptionalText(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
