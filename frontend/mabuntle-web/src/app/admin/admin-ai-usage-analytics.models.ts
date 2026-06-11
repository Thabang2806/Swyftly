export interface AdminAiUsageAnalyticsResponse {
  fromUtc: string;
  toUtc: string;
  generatedAtUtc: string;
  featureName: string | null;
  sellerId: string | null;
  totals: AdminAiUsageTotalsResponse;
  suggestions: AdminAiSuggestionAnalyticsResponse;
  moderation: AdminAiModerationAnalyticsResponse;
  featureUsage: AdminAiFeatureUsageResponse[];
  modelUsage: AdminAiModelUsageResponse[];
  topSellers: AdminAiTopSellerUsageResponse[];
}

export interface AdminAiUsageTotalsResponse {
  requests: number;
  successfulRequests: number;
  failedRequests: number;
  failureRate: number;
  inputTokens: number;
  outputTokens: number;
  estimatedCost: number;
  averageLatencyMs: number;
}

export interface AdminAiSuggestionAnalyticsResponse {
  productSuggestionsGenerated: number;
  productSuggestionsAccepted: number;
  suggestionAcceptanceRate: number;
  productSuggestionsApplied: number;
  productsTouchedByAi: number;
  productsImprovedWithAi: number;
  averageListingQualityScore: number;
  averageQualityScoreImprovement: number | null;
  qualityScoreImprovementNote: string;
  fieldAuditCount: number;
  fieldValuesAccepted: number;
  fieldValuesEdited: number;
}

export interface AdminAiModerationAnalyticsResponse {
  moderationChecks: number;
  adminReviewFlags: number;
  lowRiskFlags: number;
  mediumRiskFlags: number;
  highRiskFlags: number;
}

export interface AdminAiFeatureUsageResponse {
  featureName: string;
  requests: number;
  successfulRequests: number;
  failedRequests: number;
  estimatedCost: number;
  averageLatencyMs: number;
}

export interface AdminAiModelUsageResponse {
  modelUsed: string;
  requests: number;
  inputTokens: number;
  outputTokens: number;
  estimatedCost: number;
  averageLatencyMs: number;
}

export interface AdminAiTopSellerUsageResponse {
  sellerId: string;
  sellerDisplayName: string | null;
  requests: number;
  failedRequests: number;
  estimatedCost: number;
  averageLatencyMs: number;
}

export interface AdminAiUsageAnalyticsRequest {
  fromUtc?: string;
  toUtc?: string;
  featureName?: string;
  sellerId?: string;
}
