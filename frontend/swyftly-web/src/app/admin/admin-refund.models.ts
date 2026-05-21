export interface AdminRefundResponse {
  refundId: string;
  orderId: string;
  paymentId: string;
  buyerId: string;
  sellerId: string;
  returnRequestId: string | null;
  amount: number;
  currency: string;
  status: string;
  reason: string;
  providerRefundReference: string | null;
  failureReason: string | null;
  requestedAtUtc: string;
  approvedAtUtc: string | null;
  refundedAtUtc: string | null;
  events: AdminRefundEventResponse[];
}

export interface AdminRefundEventResponse {
  refundEventId: string;
  status: string;
  eventType: string;
  message: string;
  createdAtUtc: string;
}

export interface CreateAdminRefundRequest {
  amount: number;
  reason: string;
}

export interface ApproveAdminRefundRequest {
  reason: string;
}

export interface ConfirmManualProviderRefundRequest {
  providerRefundReference: string;
  reason: string;
}
