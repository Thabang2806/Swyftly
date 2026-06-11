export interface AdminDisputeResponse {
  disputeId: string;
  orderId: string;
  returnRequestId: string | null;
  buyerId: string;
  sellerId: string;
  status: string;
  reason: string;
  openedAtUtc: string;
  resolvedAtUtc: string | null;
  resolutionReason: string | null;
  messages: AdminDisputeMessageResponse[];
  evidence: AdminDisputeEvidenceResponse[];
}

export interface AdminDisputeMessageResponse {
  disputeMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  createdAtUtc: string;
}

export interface AdminDisputeEvidenceResponse {
  disputeEvidenceId: string;
  submittedByUserId: string;
  submittedByRole: string;
  evidenceType: string;
  storageReference: string;
  description: string | null;
  createdAtUtc: string;
}

export interface ResolveAdminDisputeRequest {
  outcome: 'BuyerFavoured' | 'SellerFavoured';
  reason: string;
}
