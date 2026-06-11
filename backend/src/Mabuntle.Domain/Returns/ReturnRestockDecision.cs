using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Returns;

public sealed class ReturnRestockDecision : Entity
{
    public const int ReasonMaxLength = 1000;

    private ReturnRestockDecision()
    {
    }

    public ReturnRestockDecision(
        Guid sellerId,
        Guid returnRequestId,
        Guid returnItemId,
        Guid productId,
        Guid productVariantId,
        int quantityRestocked,
        ReturnRestockCondition condition,
        string reason,
        Guid actorUserId,
        DateTimeOffset createdAtUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (returnRequestId == Guid.Empty)
        {
            throw new ArgumentException("Return request id is required.", nameof(returnRequestId));
        }

        if (returnItemId == Guid.Empty)
        {
            throw new ArgumentException("Return item id is required.", nameof(returnItemId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (productVariantId == Guid.Empty)
        {
            throw new ArgumentException("Product variant id is required.", nameof(productVariantId));
        }

        if (quantityRestocked < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityRestocked), "Quantity restocked cannot be negative.");
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));
        }

        SellerId = sellerId;
        ReturnRequestId = returnRequestId;
        ReturnItemId = returnItemId;
        ProductId = productId;
        ProductVariantId = productVariantId;
        QuantityRestocked = quantityRestocked;
        Condition = condition;
        Reason = Required(reason, nameof(reason), ReasonMaxLength);
        ActorUserId = actorUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid SellerId { get; private set; }

    public Guid ReturnRequestId { get; private set; }

    public Guid ReturnItemId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public int QuantityRestocked { get; private set; }

    public ReturnRestockCondition Condition { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid ActorUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}
