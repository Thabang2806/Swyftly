export interface AdminPayoutResponse {
  payoutId: string;
  sellerId: string;
  amount: number;
  currency: string;
  status: string;
  createdAtUtc: string;
  heldAtUtc: string | null;
  holdReason: string | null;
  releasedAtUtc: string | null;
  releaseReason: string | null;
  hasPendingPayoutProfileChange: boolean;
  pendingPayoutProfileChangeRequestId: string | null;
  items: AdminPayoutItemResponse[];
}

export interface AdminPayoutItemResponse {
  payoutItemId: string;
  ledgerEntryId: string;
  orderId: string | null;
  paymentId: string | null;
  amount: number;
  currency: string;
  createdAtUtc: string;
}

export interface AdminPayoutReasonRequest {
  reason: string;
}
