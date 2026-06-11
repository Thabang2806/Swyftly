export interface BuyerRefundResult {
  refundId: string;
  orderId: string;
  returnRequestId: string | null;
  amount: number;
  currency: string;
  status: string;
  statusMessage: string;
  requestedAtUtc: string;
  approvedAtUtc: string | null;
  refundedAtUtc: string | null;
  timeline: BuyerRefundTimelineEventResult[];
}

export interface BuyerRefundTimelineEventResult {
  status: string;
  eventType: string;
  message: string;
  createdAtUtc: string;
}
