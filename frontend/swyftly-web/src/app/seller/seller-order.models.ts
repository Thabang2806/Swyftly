export interface SellerOrderResult {
  orderId: string;
  buyerId: string;
  sellerId: string;
  cartId: string;
  status: string;
  items: SellerOrderItemResult[];
  itemsSubtotal: number;
  shippingAmount: number;
  platformFeeAmount: number;
  discountAmount: number;
  totalAmount: number;
  deliveryAddress?: SellerOrderDeliveryAddressResult | null;
  deliveryMethodId?: string | null;
  deliveryMethodName?: string | null;
  deliveryMethodType?: string | null;
  deliveryEstimatedMinDays?: number | null;
  deliveryEstimatedMaxDays?: number | null;
  statusHistory: SellerOrderStatusHistoryResult[];
  shipments: SellerShipmentResult[];
}

export interface SellerOrderDeliveryAddressResult {
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

export interface SellerOrderItemResult {
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

export interface SellerOrderStatusHistoryResult {
  statusHistoryId: string;
  previousStatus: string | null;
  newStatus: string;
  changedAtUtc: string;
  reason: string | null;
}

export interface SellerShipmentResult {
  shipmentId: string;
  status: string;
  carrierName: string | null;
  trackingNumber: string | null;
  trackingUrl: string | null;
  shippedAtUtc: string | null;
  deliveredAtUtc: string | null;
  events: SellerShipmentEventResult[];
}

export interface SellerShipmentEventResult {
  shipmentEventId: string;
  status: string;
  eventType: string;
  message: string | null;
  carrierName: string | null;
  trackingNumber: string | null;
  occurredAtUtc: string;
}

export interface AddSellerOrderTrackingRequest {
  carrierName: string;
  trackingNumber: string;
  trackingUrl: string | null;
  note: string | null;
}

export interface SellerFulfillmentExceptionRequest {
  reason: string;
}
