export interface BuyerOrderResult {
  orderId: string;
  buyerId: string;
  sellerId: string;
  cartId: string;
  status: string;
  items: BuyerOrderItemResult[];
  itemsSubtotal: number;
  shippingAmount: number;
  platformFeeAmount: number;
  discountAmount: number;
  totalAmount: number;
  deliveryAddress?: BuyerOrderDeliveryAddressResult | null;
  deliveryMethodId?: string | null;
  deliveryMethodName?: string | null;
  deliveryMethodType?: string | null;
  deliveryEstimatedMinDays?: number | null;
  deliveryEstimatedMaxDays?: number | null;
  statusHistory: BuyerOrderStatusHistoryResult[];
  shipments: BuyerShipmentResult[];
}

export interface BuyerOrderDeliveryAddressResult {
  recipientName: string;
  phoneNumber: string;
  addressLine1: string;
  addressLine2: string | null;
  suburb: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
  deliveryInstructions: string | null;
}

export interface BuyerOrderItemResult {
  orderItemId: string;
  productId: string;
  productVariantId: string;
  productTitle: string | null;
  sku: string;
  size: string;
  colour: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface BuyerOrderStatusHistoryResult {
  statusHistoryId: string;
  previousStatus: string | null;
  newStatus: string;
  changedAtUtc: string;
  reason: string | null;
}

export interface BuyerShipmentResult {
  shipmentId: string;
  status: string;
  carrierName: string | null;
  trackingNumber: string | null;
  trackingUrl: string | null;
  shippedAtUtc: string | null;
  deliveredAtUtc: string | null;
  events: BuyerShipmentEventResult[];
}

export interface BuyerShipmentEventResult {
  shipmentEventId: string;
  status: string;
  eventType: string;
  message: string | null;
  carrierName: string | null;
  trackingNumber: string | null;
  occurredAtUtc: string;
}
