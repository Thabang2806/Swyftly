import { AdminAuditLogResponse } from './admin-seller.models';
import { AdminQueueTriageFields } from './admin-queue-triage.models';

export interface AdminAdCampaignSummaryResponse {
  adCampaignId: string;
  sellerId: string;
  sellerDisplayName: string | null;
  name: string;
  campaignType: string;
  status: string;
  startsAtUtc: string;
  endsAtUtc: string;
  submittedAtUtc: string | null;
  productCount: number;
  totalBudget: number | null;
  currency: string | null;
}

export interface AdminAdCampaignOperationalSummaryResponse extends AdminAdCampaignSummaryResponse, AdminQueueTriageFields {
  sellerVerificationStatus: string | null;
  updatedAtUtc: string;
  detailRoute: string;
}

export interface AdminAdCampaignDetailResponse {
  adCampaignId: string;
  sellerId: string;
  seller: AdminAdCampaignSellerResponse;
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
  products: AdminAdCampaignProductResponse[];
  budget: AdminAdCampaignBudgetResponse | null;
  eligibility: AdminAdCampaignEligibilityResponse;
  auditTrail: AdminAuditLogResponse[];
}

export interface AdminAdCampaignSellerResponse {
  displayName: string | null;
  contactEmail: string | null;
  verificationStatus: string | null;
}

export interface AdminAdCampaignProductResponse {
  productId: string;
  title: string | null;
  status: string;
  publishedAtUtc: string | null;
}

export interface AdminAdCampaignBudgetResponse {
  currency: string;
  dailyBudget: number;
  totalBudget: number;
  maxCostPerClick: number;
  spentAmount: number;
}

export interface AdminAdCampaignEligibilityResponse {
  isEligible: boolean;
  sellerReasons: string[];
  products: AdminAdProductEligibilityResponse[];
}

export interface AdminAdProductEligibilityResponse {
  productId: string;
  isEligible: boolean;
  qualityScore: number;
  reasons: string[];
}

export interface AdminAdCampaignReasonRequest {
  reason: string;
}
