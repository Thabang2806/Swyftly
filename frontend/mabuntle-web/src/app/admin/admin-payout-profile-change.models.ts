export interface AdminPayoutProfileChangeRequestResponse {
  requestId: string;
  sellerId: string;
  sellerDisplayName: string | null;
  sellerContactEmail: string | null;
  sellerVerificationStatus: string;
  currentPayoutProviderReference: string | null;
  currentPayoutIsAdminApproved: boolean;
  proposedPayoutProviderReference: string;
  reason: string;
  status: string;
  requestedByUserId: string;
  submittedAtUtc: string | null;
  cancelledAtUtc: string | null;
  reviewedByUserId: string | null;
  reviewedAtUtc: string | null;
  reviewReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminPayoutProfileChangeReviewRequest {
  reason: string;
}
