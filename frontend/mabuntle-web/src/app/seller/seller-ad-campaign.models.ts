export interface UpsertSellerAdCampaignRequest {
  name: string;
  campaignType: string;
  startsAtUtc: string;
  endsAtUtc: string;
  productIds: string[];
  budget: UpsertAdBudgetRequest;
}

export interface UpsertAdBudgetRequest {
  currency: string;
  dailyBudget: number;
  totalBudget: number;
  maxCostPerClick: number;
}

export interface SellerAdCampaignResponse {
  adCampaignId: string;
  sellerId: string;
  name: string;
  campaignType: string;
  status: string;
  startsAtUtc: string;
  endsAtUtc: string;
  submittedAtUtc: string | null;
  approvedAtUtc: string | null;
  pausedAtUtc: string | null;
  completedAtUtc: string | null;
  cancelledAtUtc: string | null;
  rejectionReason: string | null;
  productIds: string[];
  budget: AdBudgetResponse | null;
  eligibility: AdCampaignEligibilityResponse;
  moderationEvents: SellerAdCampaignModerationEventResponse[];
}

export interface SellerAdCampaignModerationEventResponse {
  auditLogId: string;
  actionType: string;
  actorRole: string | null;
  reason: string | null;
  createdAtUtc: string;
}

export interface AdBudgetResponse {
  currency: string;
  dailyBudget: number;
  totalBudget: number;
  maxCostPerClick: number;
  spentAmount: number;
}

export interface AdCampaignEligibilityResponse {
  isEligible: boolean;
  sellerReasons: string[];
  products: AdProductEligibilityResponse[];
}

export interface AdProductEligibilityResponse {
  productId: string;
  isEligible: boolean;
  qualityScore: number;
  reasons: string[];
}

export interface SellerAdCampaignMetricsResponse {
  adCampaignId: string;
  sellerId: string;
  status: string;
  impressions: number;
  clicks: number;
  clickThroughRate: number;
  spend: number;
  ordersGenerated: number;
  revenueGenerated: number;
  returnOnAdSpend: number;
  currency: string;
}
