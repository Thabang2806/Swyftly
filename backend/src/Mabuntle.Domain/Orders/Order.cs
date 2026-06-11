using Mabuntle.Domain.Common;
using Mabuntle.Domain.Delivery;
using Mabuntle.Domain.Sellers;

namespace Mabuntle.Domain.Orders;

public sealed class Order : AuditableEntity
{
    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistory> _statusHistory = [];
    private readonly List<Shipment> _shipments = [];

    private Order()
    {
    }

    public Order(
        Guid buyerId,
        Guid sellerId,
        Guid cartId,
        DateTimeOffset createdAtUtc,
        decimal shippingAmount = 0,
        decimal platformFeeAmount = 0,
        decimal discountAmount = 0,
        OrderDeliveryAddress? deliveryAddress = null)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (cartId == Guid.Empty)
        {
            throw new ArgumentException("Cart id is required.", nameof(cartId));
        }

        ValidateMoney(shippingAmount, nameof(shippingAmount), allowNegative: false);
        ValidateMoney(platformFeeAmount, nameof(platformFeeAmount), allowNegative: false);
        ValidateMoney(discountAmount, nameof(discountAmount), allowNegative: false);

        BuyerId = buyerId;
        SellerId = sellerId;
        CartId = cartId;
        Status = OrderStatus.PendingPayment;
        ShippingAmount = shippingAmount;
        PlatformFeeAmount = platformFeeAmount;
        DiscountAmount = discountAmount;
        SetDeliveryAddress(deliveryAddress);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        AddStatusHistory(null, Status, createdAtUtc, "OrderCreated");
    }

    public Guid BuyerId { get; private set; }

    public Guid SellerId { get; private set; }

    public Guid CartId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal ShippingAmount { get; private set; }

    public decimal PlatformFeeAmount { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public Guid? DeliveryMethodId { get; private set; }

    public string? DeliveryMethodName { get; private set; }

    public string? DeliveryMethodType { get; private set; }

    public int? DeliveryEstimatedMinDays { get; private set; }

    public int? DeliveryEstimatedMaxDays { get; private set; }

    public string? DeliveryRecipientName { get; private set; }

    public string? DeliveryPhoneNumber { get; private set; }

    public string? DeliveryAddressLine1 { get; private set; }

    public string? DeliveryAddressLine2 { get; private set; }

    public string? DeliverySuburb { get; private set; }

    public string? DeliveryCity { get; private set; }

    public string? DeliveryProvince { get; private set; }

    public string? DeliveryPostalCode { get; private set; }

    public string? DeliveryCountryCode { get; private set; }

    public string? DeliveryInstructions { get; private set; }

    public AddressVerificationStatus DeliveryVerificationStatus { get; private set; } = AddressVerificationStatus.Unverified;

    public string? DeliveryVerificationProvider { get; private set; }

    public string? DeliveryVerificationWarningsJson { get; private set; }

    public DateTimeOffset? DeliveryVerifiedAtUtc { get; private set; }

    public Guid? PickupPointId { get; private set; }

    public string? PickupPointProviderName { get; private set; }

    public string? PickupPointCode { get; private set; }

    public string? PickupPointName { get; private set; }

    public string? PickupPointAddressLine1 { get; private set; }

    public string? PickupPointAddressLine2 { get; private set; }

    public string? PickupPointSuburb { get; private set; }

    public string? PickupPointCity { get; private set; }

    public string? PickupPointProvince { get; private set; }

    public string? PickupPointPostalCode { get; private set; }

    public string? PickupPointCountryCode { get; private set; }

    public decimal? PickupPointLatitude { get; private set; }

    public decimal? PickupPointLongitude { get; private set; }

    public string? PickupPointOpeningHours { get; private set; }

    public int? SellerPolicyReturnWindowDays { get; private set; }

    public string? SellerPolicyReturnPolicy { get; private set; }

    public string? SellerPolicyExchangePolicy { get; private set; }

    public string? SellerPolicyFulfilmentPolicy { get; private set; }

    public string? SellerPolicySupportPolicy { get; private set; }

    public string? SellerPolicyCareInstructions { get; private set; }

    public string? SellerPolicyProductDisclaimer { get; private set; }

    public DateTimeOffset? SellerPolicySnapshotAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public IReadOnlyCollection<Shipment> Shipments => _shipments.AsReadOnly();

    public decimal ItemsSubtotal => _items.Sum(item => item.LineTotal);

    public decimal TotalAmount => ItemsSubtotal + ShippingAmount + PlatformFeeAmount - DiscountAmount;

    public OrderDeliveryAddress? DeliveryAddress =>
        DeliveryRecipientName is null
            || DeliveryPhoneNumber is null
            || DeliveryAddressLine1 is null
            || DeliveryCity is null
            || DeliveryProvince is null
            || DeliveryPostalCode is null
            || DeliveryCountryCode is null
                ? null
                : new OrderDeliveryAddress(
                    DeliveryRecipientName,
                    DeliveryPhoneNumber,
                    DeliveryAddressLine1,
                    DeliveryAddressLine2,
                    DeliverySuburb,
                    DeliveryCity,
                    DeliveryProvince,
                    DeliveryPostalCode,
                    DeliveryCountryCode,
                    DeliveryInstructions,
                    DeliveryVerificationStatus,
                    DeliveryVerificationProvider,
                    DeliveryVerificationWarningsJson,
                    DeliveryVerifiedAtUtc);

    public PickupPointSnapshot? PickupPoint =>
        PickupPointId is null
            || PickupPointProviderName is null
            || PickupPointCode is null
            || PickupPointName is null
            || PickupPointAddressLine1 is null
            || PickupPointCity is null
            || PickupPointProvince is null
            || PickupPointPostalCode is null
            || PickupPointCountryCode is null
                ? null
                : new PickupPointSnapshot(
                    PickupPointId.Value,
                    PickupPointProviderName,
                    PickupPointCode,
                    PickupPointName,
                    PickupPointAddressLine1,
                    PickupPointAddressLine2,
                    PickupPointSuburb,
                    PickupPointCity,
                    PickupPointProvince,
                    PickupPointPostalCode,
                    PickupPointCountryCode,
                    PickupPointLatitude,
                    PickupPointLongitude,
                    PickupPointOpeningHours);

    public OrderSellerPolicySnapshot? SellerPolicySnapshot =>
        SellerPolicySnapshotAtUtc is null
            && SellerPolicyReturnWindowDays is null
            && SellerPolicyReturnPolicy is null
            && SellerPolicyExchangePolicy is null
            && SellerPolicyFulfilmentPolicy is null
            && SellerPolicySupportPolicy is null
            && SellerPolicyCareInstructions is null
            && SellerPolicyProductDisclaimer is null
                ? null
                : new OrderSellerPolicySnapshot(
                    SellerPolicyReturnWindowDays,
                    SellerPolicyReturnPolicy,
                    SellerPolicyExchangePolicy,
                    SellerPolicyFulfilmentPolicy,
                    SellerPolicySupportPolicy,
                    SellerPolicyCareInstructions,
                    SellerPolicyProductDisclaimer,
                    SellerPolicySnapshotAtUtc);

    public void AddItem(
        Guid productId,
        Guid productVariantId,
        string? productTitle,
        string sku,
        string size,
        string colour,
        decimal unitPrice,
        int quantity)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new InvalidOperationException("Items can only be added while an order is pending payment.");
        }

        _items.Add(new OrderItem(
            Id,
            productId,
            productVariantId,
            productTitle,
            sku,
            size,
            colour,
            unitPrice,
            quantity));
    }

    public void ChangeStatus(OrderStatus newStatus, DateTimeOffset changedAtUtc, string? reason = null)
    {
        if (newStatus == Status)
        {
            return;
        }

        var previousStatus = Status;
        Status = newStatus;
        UpdatedAtUtc = changedAtUtc;
        AddStatusHistory(previousStatus, newStatus, changedAtUtc, reason);
    }

    public void SetDeliveryAddress(OrderDeliveryAddress? deliveryAddress)
    {
        DeliveryRecipientName = deliveryAddress?.RecipientName;
        DeliveryPhoneNumber = deliveryAddress?.PhoneNumber;
        DeliveryAddressLine1 = deliveryAddress?.AddressLine1;
        DeliveryAddressLine2 = deliveryAddress?.AddressLine2;
        DeliverySuburb = deliveryAddress?.Suburb;
        DeliveryCity = deliveryAddress?.City;
        DeliveryProvince = deliveryAddress?.Province;
        DeliveryPostalCode = deliveryAddress?.PostalCode;
        DeliveryCountryCode = deliveryAddress?.CountryCode;
        DeliveryInstructions = deliveryAddress?.DeliveryInstructions;
        DeliveryVerificationStatus = deliveryAddress?.VerificationStatus ?? AddressVerificationStatus.Unverified;
        DeliveryVerificationProvider = deliveryAddress?.VerificationProvider;
        DeliveryVerificationWarningsJson = deliveryAddress?.VerificationWarningsJson;
        DeliveryVerifiedAtUtc = deliveryAddress?.VerifiedAtUtc;
    }

    public void SetDeliveryMethod(SellerDeliveryMethod deliveryMethod, decimal shippingAmount)
    {
        ArgumentNullException.ThrowIfNull(deliveryMethod);
        ValidateMoney(shippingAmount, nameof(shippingAmount), allowNegative: false);

        DeliveryMethodId = deliveryMethod.Id;
        DeliveryMethodName = deliveryMethod.Name;
        DeliveryMethodType = deliveryMethod.MethodType.ToString();
        DeliveryEstimatedMinDays = deliveryMethod.EstimatedMinDays;
        DeliveryEstimatedMaxDays = deliveryMethod.EstimatedMaxDays;
        ShippingAmount = shippingAmount;
    }

    public void SetPickupPoint(PickupPoint? pickupPoint)
    {
        PickupPointId = pickupPoint?.Id;
        PickupPointProviderName = pickupPoint?.ProviderName;
        PickupPointCode = pickupPoint?.Code;
        PickupPointName = pickupPoint?.Name;
        PickupPointAddressLine1 = pickupPoint?.AddressLine1;
        PickupPointAddressLine2 = pickupPoint?.AddressLine2;
        PickupPointSuburb = pickupPoint?.Suburb;
        PickupPointCity = pickupPoint?.City;
        PickupPointProvince = pickupPoint?.Province;
        PickupPointPostalCode = pickupPoint?.PostalCode;
        PickupPointCountryCode = pickupPoint?.CountryCode;
        PickupPointLatitude = pickupPoint?.Latitude;
        PickupPointLongitude = pickupPoint?.Longitude;
        PickupPointOpeningHours = pickupPoint?.OpeningHours;
    }

    public void SetSellerPolicySnapshot(SellerStorePolicy? policy, DateTimeOffset snapshotAtUtc)
    {
        SellerPolicyReturnWindowDays = policy?.ReturnWindowDays;
        SellerPolicyReturnPolicy = policy?.ReturnPolicy;
        SellerPolicyExchangePolicy = policy?.ExchangePolicy;
        SellerPolicyFulfilmentPolicy = policy?.FulfilmentPolicy;
        SellerPolicySupportPolicy = policy?.SupportPolicy;
        SellerPolicyCareInstructions = policy?.CareInstructions;
        SellerPolicyProductDisclaimer = policy?.ProductDisclaimer;
        SellerPolicySnapshotAtUtc = policy is null ? null : snapshotAtUtc;
    }

    private void AddStatusHistory(
        OrderStatus? previousStatus,
        OrderStatus newStatus,
        DateTimeOffset changedAtUtc,
        string? reason)
    {
        _statusHistory.Add(new OrderStatusHistory(Id, previousStatus, newStatus, changedAtUtc, reason));
    }

    private static void ValidateMoney(decimal amount, string parameterName, bool allowNegative)
    {
        if (!allowNegative && amount < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Amount cannot be negative.");
        }
    }
}
