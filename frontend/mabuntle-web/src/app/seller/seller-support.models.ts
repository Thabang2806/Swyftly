export type SellerSupportTicketCategory =
  'OrderIssue' |
  'PaymentIssue' |
  'ReturnIssue' |
  'SellerIssue' |
  'ProductIssue' |
  'TechnicalIssue' |
  'Other';

export interface CreateSellerSupportTicketRequest {
  category: SellerSupportTicketCategory;
  subject: string;
  description: string;
  linkedOrderId: string | null;
  linkedProductId: string | null;
  linkedSellerId: string | null;
  linkedPaymentId: string | null;
}

export interface SellerSupportMessageRequest {
  message: string;
}

export interface SellerSupportTicketResponse {
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
  messages: SellerSupportMessageResponse[];
}

export interface SellerSupportMessageResponse {
  supportMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  isInternal: boolean;
  createdAtUtc: string;
}
