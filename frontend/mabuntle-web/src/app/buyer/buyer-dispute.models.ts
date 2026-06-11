export interface BuyerDisputeResponse {
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
  messages: BuyerDisputeMessageResponse[];
  evidence: BuyerDisputeEvidenceResponse[];
}

export interface BuyerDisputeMessageResponse {
  disputeMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  createdAtUtc: string;
}

export interface BuyerDisputeEvidenceResponse {
  disputeEvidenceId: string;
  submittedByUserId: string;
  submittedByRole: string;
  evidenceType: string;
  storageReference: string;
  description: string | null;
  createdAtUtc: string;
}

export interface BuyerDisputeMessageRequest {
  message: string;
}

export interface BuyerDisputeEvidenceRequest {
  evidenceType: string;
  storageReference: string;
  description: string | null;
}
