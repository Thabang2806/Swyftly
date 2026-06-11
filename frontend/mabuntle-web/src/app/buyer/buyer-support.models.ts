export type BuyerSupportTicketCategory =
  'OrderIssue' |
  'PaymentIssue' |
  'ReturnIssue' |
  'SellerIssue' |
  'ProductIssue' |
  'TechnicalIssue' |
  'Other';

export interface CreateBuyerSupportTicketRequest {
  category: BuyerSupportTicketCategory;
  subject: string;
  description: string;
  linkedOrderId: string | null;
  linkedProductId: string | null;
  linkedSellerId: string | null;
  linkedPaymentId: string | null;
}

export interface BuyerSupportMessageRequest {
  message: string;
}

export interface BuyerSupportTicketResponse {
  supportTicketId: string;
  createdByUserId: string;
  createdByRole: string;
  buyerId: string | null;
  sellerId: string | null;
  category: string;
  status: string;
  subject: string;
  description: string;
  linkedOrderId: string | null;
  linkedProductId: string | null;
  linkedSellerId: string | null;
  linkedPaymentId: string | null;
  assignedSupportUserId: string | null;
  openedAtUtc: string;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  messages: BuyerSupportMessageResponse[];
}

export interface BuyerSupportMessageResponse {
  supportMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  isInternal: boolean;
  createdAtUtc: string;
}
