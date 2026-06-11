export interface AdminMarketplaceReportResponse {
  fromUtc: string;
  toUtc: string;
  generatedAtUtc: string;
  currency: string;
  finance: AdminMarketplaceFinanceSummaryResponse;
  operations: AdminMarketplaceOperationsSummaryResponse;
  topSellers: AdminTopSellerReportRowResponse[];
  topCategories: AdminTopCategoryReportRowResponse[];
  csvExportUrl: string;
}

export interface AdminMarketplaceFinanceSummaryResponse {
  grossMerchandiseValue: number;
  platformCommissionEarned: number;
  paymentProcessingFees: number;
  refunds: number;
  sellerPendingBalances: number;
  sellerAvailableBalances: number;
  sellerHeldBalances: number;
  payoutsProcessed: number;
  failedPayouts: number;
}

export interface AdminMarketplaceOperationsSummaryResponse {
  orderCount: number;
  refundCount: number;
  payoutsProcessedCount: number;
  failedPayoutCount: number;
  disputeCount: number;
  activeDisputeCount: number;
}

export interface AdminTopSellerReportRowResponse {
  sellerId: string;
  sellerDisplayName: string | null;
  orderCount: number;
  grossMerchandiseValue: number;
  itemsSold: number;
}

export interface AdminTopCategoryReportRowResponse {
  categoryId: string | null;
  categoryName: string | null;
  quantitySold: number;
  revenue: number;
}

export interface AdminMarketplaceReportRequest {
  fromUtc?: string;
  toUtc?: string;
}

export interface AdminBuyerGrowthReportRequest extends AdminMarketplaceReportRequest {
  bucket?: 'Day' | 'Week';
}

export interface AdminBuyerGrowthReportResponse {
  fromUtc: string;
  toUtc: string;
  generatedAtUtc: string;
  bucket: 'Day' | 'Week';
  summary: AdminBuyerGrowthSummaryResponse;
  outcomeSummary: AdminBuyerGrowthOutcomeSummaryResponse;
  confidenceBreakdown: AdminBuyerGrowthBreakdownResponse[];
  sourceToolBreakdown: AdminBuyerGrowthBreakdownResponse[];
  outcomeSourceToolBreakdown: AdminBuyerGrowthOutcomeBreakdownResponse[];
  outcomeConfidenceBreakdown: AdminBuyerGrowthOutcomeBreakdownResponse[];
  topCategories: AdminBuyerGrowthContextResponse[];
  topColours: AdminBuyerGrowthContextResponse[];
  topMaterials: AdminBuyerGrowthContextResponse[];
  trend: AdminBuyerGrowthTrendBucketResponse[];
}

export interface AdminBuyerGrowthSummaryResponse {
  searchSubmittedCount: number;
  shopHandoffCount: number;
  productOpenedCount: number;
  feedbackSubmittedCount: number;
  assistantSearchCount: number;
  visualSearchCount: number;
}

export interface AdminBuyerGrowthOutcomeSummaryResponse {
  productOpenedCount: number;
  addToCartCount: number;
  checkoutStartedCount: number;
  orderCreatedCount: number;
  paidOrderCount: number;
  productOpenToCartRate: number;
  cartToCheckoutRate: number;
  checkoutToOrderRate: number;
  orderToPaidRate: number;
}

export interface AdminBuyerGrowthBreakdownResponse {
  name: string;
  count: number;
}

export interface AdminBuyerGrowthOutcomeBreakdownResponse {
  name: string;
  productOpenedCount: number;
  addToCartCount: number;
  checkoutStartedCount: number;
  orderCreatedCount: number;
  paidOrderCount: number;
}

export interface AdminBuyerGrowthContextResponse {
  value: string;
  count: number;
}

export interface AdminBuyerGrowthTrendBucketResponse {
  periodStartUtc: string;
  periodEndUtc: string;
  searchSubmittedCount: number;
  shopHandoffCount: number;
  productOpenedCount: number;
  feedbackSubmittedCount: number;
  attributedProductOpenCount: number;
  addToCartCount: number;
  checkoutStartedCount: number;
  orderCreatedCount: number;
  paidOrderCount: number;
}
