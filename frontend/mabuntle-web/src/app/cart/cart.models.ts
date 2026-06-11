export interface CartResponse {
  cartId: string | null;
  buyerId: string | null;
  sellerId: string | null;
  sellerStoreName: string | null;
  items: CartItemResponse[];
  totalQuantity: number;
  subtotal: number;
}

export interface CartItemResponse {
  cartItemId: string;
  productId: string;
  productVariantId: string;
  productTitle: string | null;
  productSlug: string | null;
  primaryImageUrl: string | null;
  primaryImageAltText: string | null;
  sku: string;
  size: string;
  colour: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface AddCartItemRequest {
  productVariantId: string;
  quantity: number;
}

export interface UpdateCartItemRequest {
  quantity: number;
}

export interface CreateOrderFromCartRequest {
  cartId: string | null;
  reservationMinutes: number | null;
  deliveryAddressId: string | null;
  deliveryAddress: OrderDeliveryAddressRequest | null;
  deliveryMethodId: string | null;
  pickupPointId?: string | null;
}

export interface OrderDeliveryAddressRequest {
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
  verificationStatus?: string | null;
  verificationProvider?: string | null;
  verificationWarnings?: string[] | null;
  verifiedAtUtc?: string | null;
}

export interface OrderResult {
  orderId: string;
  buyerId: string;
  sellerId: string;
  cartId: string;
  status: string;
  items: OrderItemResult[];
  itemsSubtotal: number;
  shippingAmount: number;
  platformFeeAmount: number;
  discountAmount: number;
  totalAmount: number;
  deliveryAddress?: OrderDeliveryAddressResult | null;
  statusHistory: OrderStatusHistoryResult[];
  deliveryMethodId?: string | null;
  deliveryMethodName?: string | null;
  deliveryMethodType?: string | null;
  deliveryEstimatedMinDays?: number | null;
  deliveryEstimatedMaxDays?: number | null;
  pickupPoint?: OrderPickupPointResult | null;
}

export interface OrderDeliveryAddressResult {
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
  verificationStatus?: string | null;
  verificationProvider?: string | null;
  verificationWarnings?: string[] | null;
  verifiedAtUtc?: string | null;
}

export interface OrderPickupPointResult {
  pickupPointId: string;
  providerName: string;
  code: string;
  name: string;
  addressLine1: string;
  addressLine2: string | null;
  suburb: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
  latitude: number | null;
  longitude: number | null;
  openingHours: string | null;
}

export interface OrderItemResult {
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

export interface OrderStatusHistoryResult {
  statusHistoryId: string;
  previousStatus: string | null;
  newStatus: string;
  changedAtUtc: string;
  reason: string | null;
}

export interface CartShippingOptionsRequest {
  cartId: string | null;
  deliveryAddressId: string | null;
  deliveryAddress: OrderDeliveryAddressRequest | null;
}

export interface CartShippingOptionsResponse {
  cartId: string;
  sellerId: string;
  cartSubtotal: number;
  options: CartShippingOptionResponse[];
  addressVerification?: CartAddressVerificationResponse | null;
}

export interface CartShippingOptionResponse {
  deliveryMethodId: string;
  name: string;
  description: string | null;
  methodType: 'Standard' | 'Express' | 'LocalCourier' | 'PickupPoint';
  countryCode: string;
  province: string | null;
  basePrice: number;
  freeShippingThreshold: number | null;
  shippingAmount: number;
  freeShippingApplied: boolean;
  estimatedMinDays: number;
  estimatedMaxDays: number;
  displayOrder: number;
  requiresPickupPoint?: boolean;
  pickupPoints?: CartShippingPickupPointResponse[] | null;
}

export interface CartShippingPickupPointResponse {
  pickupPointId: string;
  providerName: string;
  code: string;
  name: string;
  addressLine1: string;
  addressLine2: string | null;
  suburb: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
  latitude: number | null;
  longitude: number | null;
  openingHours: string | null;
}

export interface CartAddressVerificationResponse {
  verificationStatus: string;
  verificationProvider: string;
  verificationWarnings: string[];
  verifiedAtUtc: string | null;
}
