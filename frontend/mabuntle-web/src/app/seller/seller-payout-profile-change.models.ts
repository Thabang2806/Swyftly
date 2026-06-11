export interface SellerPayoutProfileChangeStateResponse {
  currentPayoutProfile: SellerPayoutProfileSummaryResponse | null;
  activeRequest: SellerPayoutProfileChangeRequestResponse | null;
  latestRequest: SellerPayoutProfileChangeRequestResponse | null;
}

export interface SellerPayoutProfileSummaryResponse {
  payoutProviderReference: string;
  isAdminApproved: boolean;
  approvedAtUtc: string | null;
  approvedByUserId: string | null;
}

export interface SellerPayoutProfileChangeRequestResponse {
  requestId: string;
  status: string;
  proposedPayoutProviderReference: string;
  reason: string;
  reviewReason: string | null;
  submittedAtUtc: string | null;
  cancelledAtUtc: string | null;
  reviewedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SellerPayoutProfileChangeRequestRequest {
  payoutProviderReference: string;
  reason: string;
}
