import {
  BuyerOrderDeliveryAddressResult,
  BuyerOrderItemResult,
  BuyerOrderPickupPointResult,
  BuyerOrderStatusHistoryResult,
  BuyerShipmentResult
} from '../buyer/buyer-order.models';

export interface AdminOrderSummaryResponse {
  orderId: string;
  buyerId: string;
  sellerId: string;
  sellerDisplayName: string | null;
  status: string;
  itemCount: number;
  itemsSubtotal: number;
  shippingAmount: number;
  platformFeeAmount: number;
  discountAmount: number;
  totalAmount: number;
  deliveryMethodId?: string | null;
  deliveryMethodName?: string | null;
  deliveryMethodType?: string | null;
  deliveryEstimatedMinDays?: number | null;
  deliveryEstimatedMaxDays?: number | null;
  pickupPoint?: BuyerOrderPickupPointResult | null;
  paymentStatus: string | null;
  shipmentStatus: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminOrderDetailResponse extends AdminOrderSummaryResponse {
  cartId: string;
  deliveryAddress: BuyerOrderDeliveryAddressResult | null;
  items: BuyerOrderItemResult[];
  statusHistory: BuyerOrderStatusHistoryResult[];
  shipments: BuyerShipmentResult[];
  payments: AdminPaymentSummaryResponse[];
}

export interface AdminPaymentSummaryResponse {
  paymentId: string;
  orderId: string;
  buyerId: string;
  provider: string;
  providerReference: string | null;
  amount: number;
  currency: string;
  status: string;
  paidAtUtc: string | null;
  failedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminPaymentDetailResponse extends AdminPaymentSummaryResponse {
  order: AdminPaymentOrderResponse | null;
  events: AdminPaymentEventResponse[];
}

export interface AdminPaymentReconciliationCandidateResponse extends AdminPaymentSummaryResponse {
  reasonCode: string;
  recommendedAction: string;
  latestEvent: AdminPaymentEventResponse | null;
  latestReview: AdminPaymentReconciliationReviewResponse | null;
}

export type PaymentReconciliationOutcome =
  | 'ProviderPending'
  | 'MatchedNoAction'
  | 'ProviderPaidMissingWebhook'
  | 'ProviderFailedMissingWebhook'
  | 'ManualRecoveryRequired';

export interface CreatePaymentReconciliationReviewRequest {
  observedProviderStatus: string;
  observedAmount: number | null;
  observedCurrency: string | null;
  outcome: PaymentReconciliationOutcome;
  reason: string;
  nextReviewAfterUtc: string | null;
}

export interface AdminPaymentReconciliationReviewResponse {
  reviewId: string;
  paymentId: string;
  provider: string;
  providerReference: string | null;
  observedProviderStatus: string;
  observedAmount: number | null;
  observedCurrency: string | null;
  outcome: PaymentReconciliationOutcome;
  reason: string;
  reviewedByUserId: string;
  reviewedAtUtc: string;
  nextReviewAfterUtc: string | null;
}

export interface AdminPaymentOrderResponse {
  orderId: string;
  buyerId: string;
  sellerId: string;
  status: string;
  itemCount: number;
  totalAmount: number;
  createdAtUtc: string;
}

export interface AdminPaymentEventResponse {
  paymentEventId: string;
  provider: string;
  providerEventId: string;
  eventType: string;
  processingStatus: string;
  receivedAtUtc: string;
  processedAtUtc: string | null;
  errorMessage: string | null;
}
