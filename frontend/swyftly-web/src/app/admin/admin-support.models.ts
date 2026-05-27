export type AdminSupportTicketStatus =
  'Open' |
  'WaitingForCustomer' |
  'WaitingForSeller' |
  'Escalated' |
  'Resolved' |
  'Closed';

export type AdminSupportTicketPriority = 'Normal' | 'High' | 'Urgent';

export type AdminSupportSlaStatus = 'OnTrack' | 'DueSoon' | 'Overdue';

export type AdminSupportTicketCategory =
  'OrderIssue' |
  'PaymentIssue' |
  'ReturnIssue' |
  'SellerIssue' |
  'ProductIssue' |
  'TechnicalIssue' |
  'Other';

export interface AdminSupportTicketResponse {
  supportTicketId: string;
  createdByUserId: string;
  createdByRole: string;
  buyerId: string | null;
  sellerId: string | null;
  category: AdminSupportTicketCategory | string;
  status: AdminSupportTicketStatus | string;
  priority: AdminSupportTicketPriority | string;
  subject: string;
  description: string;
  linkedOrderId: string | null;
  linkedProductId: string | null;
  linkedSellerId: string | null;
  linkedPaymentId: string | null;
  assignedSupportUserId: string | null;
  escalationReason: string | null;
  escalatedAtUtc: string | null;
  escalatedByUserId: string | null;
  openedAtUtc: string;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  messages: AdminSupportMessageResponse[];
  customerContext: AdminSupportCustomerContextResponse | null;
}

export interface AdminSupportCustomerContextResponse {
  buyer: AdminSupportBuyerContextResponse | null;
  seller: AdminSupportSellerContextResponse | null;
  order: AdminSupportOrderContextResponse | null;
  payment: AdminSupportPaymentContextResponse | null;
  product: AdminSupportProductContextResponse | null;
}

export interface AdminSupportBuyerContextResponse {
  buyerId: string;
  userId: string;
  displayName: string | null;
  email: string | null;
  phoneNumber: string | null;
}

export interface AdminSupportSellerContextResponse {
  sellerId: string;
  userId: string;
  displayName: string | null;
  contactEmail: string | null;
  phoneNumber: string | null;
  verificationStatus: string;
  adminRoute: string;
}

export interface AdminSupportOrderContextResponse {
  orderId: string;
  status: string;
  totalAmount: number;
  createdAtUtc: string;
  buyerId: string;
  sellerId: string;
  adminRoute: string;
}

export interface AdminSupportPaymentContextResponse {
  paymentId: string;
  orderId: string;
  provider: string;
  status: string;
  amount: number;
  currency: string;
  paidAtUtc: string | null;
  failedAtUtc: string | null;
  adminRoute: string;
}

export interface AdminSupportProductContextResponse {
  productId: string;
  sellerId: string;
  title: string | null;
  slug: string | null;
  status: string;
  adminRoute: string;
}

export interface AdminSupportMessageResponse {
  supportMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  isInternal: boolean;
  createdAtUtc: string;
}

export interface AdminSupportMessageRequest {
  message: string;
}

export interface AdminSupportQueueResponse {
  items: AdminSupportQueueItemResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  statusCounts: AdminSupportCountResponse[];
  priorityCounts: AdminSupportCountResponse[];
  slaCounts: AdminSupportCountResponse[];
}

export interface AdminSupportQueueItemResponse {
  supportTicketId: string;
  createdByUserId: string;
  createdByRole: string;
  buyerId: string | null;
  sellerId: string | null;
  category: AdminSupportTicketCategory | string;
  status: AdminSupportTicketStatus | string;
  priority: AdminSupportTicketPriority | string;
  subject: string;
  description: string;
  linkedOrderId: string | null;
  linkedProductId: string | null;
  linkedSellerId: string | null;
  linkedPaymentId: string | null;
  assignedSupportUserId: string | null;
  assignedSupportDisplayName: string | null;
  openedAtUtc: string;
  updatedAtUtc: string;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  escalationReason: string | null;
  escalatedAtUtc: string | null;
  escalatedByUserId: string | null;
  latestInternalNote: string | null;
  messageCount: number;
  ageHours: number;
  slaStatus: AdminSupportSlaStatus | string;
  slaDueAtUtc: string;
}

export interface AdminSupportSummaryResponse {
  generatedAtUtc: string;
  openTicketCount: number;
  escalatedTicketCount: number;
  overdueTicketCount: number;
  myOpenTicketCount: number;
  unassignedOpenTicketCount: number;
  resolvedTodayCount: number;
  resolvedLast7DaysCount: number;
  averageFirstResponseHours: number | null;
  averageResolutionHours: number | null;
  statusCounts: AdminSupportCountResponse[];
  priorityCounts: AdminSupportCountResponse[];
  slaCounts: AdminSupportCountResponse[];
  assigneeCounts: AdminSupportAssigneeCountResponse[];
}

export interface AdminSupportCountResponse {
  key: string;
  count: number;
}

export interface AdminSupportAssigneeCountResponse {
  assignedSupportUserId: string;
  assignedSupportDisplayName: string | null;
  count: number;
}

export interface AdminSupportQueueFilters {
  view?: 'NeedsAttention' | 'All';
  status?: string;
  category?: string;
  search?: string;
  assigned?: 'Any' | 'Mine' | 'Unassigned';
  priority?: string;
  sla?: string;
  savedViewId?: string;
  page?: number;
  pageSize?: number;
  sort?: string;
}

export interface AdminSupportQualityReportFilters {
  fromUtc?: string;
  toUtc?: string;
  bucket?: 'Day' | 'Week';
  category?: string;
  priority?: string;
  assignedSupportUserId?: string;
  createdByRole?: string;
}

export interface AdminSupportQualityReportResponse {
  generatedAtUtc: string;
  fromUtc: string;
  toUtc: string;
  bucket: 'Day' | 'Week' | string;
  summary: AdminSupportQualitySummaryResponse;
  trend: AdminSupportQualityTrendBucketResponse[];
  categoryBreakdown: AdminSupportQualityBreakdownResponse[];
  priorityBreakdown: AdminSupportQualityBreakdownResponse[];
  requesterRoleBreakdown: AdminSupportQualityBreakdownResponse[];
  slaBreakdown: AdminSupportQualityBreakdownResponse[];
  assigneeBreakdown: AdminSupportQualityAssigneeBreakdownResponse[];
}

export interface AdminSupportQualitySummaryResponse {
  createdCount: number;
  resolvedCount: number;
  closedCount: number;
  escalatedCount: number;
  currentlyOpenCount: number;
  currentlyOverdueCount: number;
  averageFirstResponseHours: number | null;
  averageResolutionHours: number | null;
  firstResponseTargetMetCount: number;
  firstResponseTargetMissedCount: number;
  resolutionTargetMetCount: number;
  resolutionTargetMissedCount: number;
}

export interface AdminSupportQualityTrendBucketResponse {
  bucketStartUtc: string;
  bucketEndUtc: string;
  createdCount: number;
  resolvedCount: number;
  escalatedCount: number;
  averageFirstResponseHours: number | null;
  averageResolutionHours: number | null;
}

export interface AdminSupportQualityBreakdownResponse {
  key: string;
  createdCount: number;
  resolvedCount: number;
  escalatedCount: number;
  firstResponseTargetMissedCount: number;
  resolutionTargetMissedCount: number;
  averageFirstResponseHours: number | null;
  averageResolutionHours: number | null;
}

export interface AdminSupportQualityAssigneeBreakdownResponse {
  assignedSupportUserId: string;
  assignedSupportDisplayName: string | null;
  createdCount: number;
  resolvedCount: number;
  escalatedCount: number;
  firstResponseTargetMissedCount: number;
  resolutionTargetMissedCount: number;
  averageFirstResponseHours: number | null;
  averageResolutionHours: number | null;
}

export interface AdminSupportTriageRequest {
  priority: string;
  internalNote?: string | null;
}

export interface AdminSupportEscalationRequest {
  reason: string;
}
