export interface AdminDashboardSummaryResponse {
  pendingSellerApprovals: number;
  pendingProductReviews: number;
  newOrdersToday: number;
  openDisputes: number;
  pendingRefunds: number;
  pendingPayouts: number;
  totalGrossSalesPlaceholder: number;
  platformCommissionPlaceholder: number;
}
