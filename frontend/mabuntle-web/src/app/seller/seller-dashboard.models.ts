export interface SellerDashboardAlertResponse {
  severity: 'danger' | 'warning' | 'accent' | 'neutral' | string;
  title: string;
  message: string;
  route: string;
  count: number;
}

export interface SellerDashboardActivityResponse {
  type: string;
  title: string;
  status: string;
  occurredAtUtc: string;
  route: string;
}

export interface SellerDashboardSummaryResponse {
  sellerId: string;
  generatedAtUtc: string;
  fromUtc: string;
  salesLast30Days: number;
  ordersLast30Days: number;
  paidOrderCount: number;
  processingOrderCount: number;
  readyToShipOrderCount: number;
  shippedOrderCount: number;
  pendingFulfilmentOrders: number;
  deliveryExceptionOrderCount: number;
  draftProductCount: number;
  pendingReviewProductCount: number;
  publishedProductCount: number;
  changesRequestedProductCount: number;
  pendingListingRevisionCount: number;
  pendingVariantRevisionCount: number;
  lowStockProductCount: number;
  outOfStockVariantCount: number;
  reservedStockCount: number;
  openReturnCount: number;
  returnsAwaitingSellerResponseCount: number;
  openSupportTicketCount: number;
  activeDisputeCount: number;
  pendingPayoutAmount: number;
  availablePayoutAmount: number;
  heldPayoutAmount: number;
  pendingPayoutCount: number;
  processingPayoutCount: number;
  hasPendingPayoutProfileChange: boolean;
  activeAdCampaignCount: number;
  pendingAdReviewCount: number;
  adSpendLast30Days: number;
  adRevenueLast30Days: number;
  unreadNotificationCount: number;
  alerts: SellerDashboardAlertResponse[];
  recentActivity: SellerDashboardActivityResponse[];
}
