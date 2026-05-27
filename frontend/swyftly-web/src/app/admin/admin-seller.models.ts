import { SellerPolicyResponse } from '../shared/seller-policy.models';
import { SellerVerificationEvidenceResponse } from '../seller/seller-verification-evidence.models';
import { AdminQueueTriageFields } from './admin-queue-triage.models';

export interface AdminSellerSummaryResponse {
  sellerId: string;
  displayName: string | null;
  contactEmail: string | null;
  storeName: string | null;
  storeSlug: string | null;
  verificationStatus: string;
  submittedAtUtc: string | null;
}

export interface AdminSellerOperationalSummaryResponse extends AdminSellerSummaryResponse, AdminQueueTriageFields {
  updatedAtUtc: string;
  detailRoute: string;
}

export interface AdminSellerDetailResponse {
  sellerId: string;
  userId: string;
  verificationStatus: string;
  displayName: string | null;
  contactEmail: string | null;
  phoneNumber: string | null;
  businessType: string | null;
  businessName: string | null;
  storefront: AdminSellerStorefrontResponse | null;
  address: AdminSellerAddressResponse | null;
  payout: AdminSellerPayoutResponse | null;
  storePolicy: SellerPolicyResponse;
  verificationEvidence: SellerVerificationEvidenceResponse[];
  auditTrail: AdminAuditLogResponse[];
}

export interface AdminSellerStorefrontResponse {
  storeName: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  isPublished: boolean;
}

export interface AdminSellerAddressResponse {
  addressLine1: string;
  addressLine2: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
}

export interface AdminSellerPayoutResponse {
  payoutProviderReference: string;
  hasSubmittedPlaceholder: boolean;
  isAdminApproved: boolean;
}

export interface AdminAuditLogResponse {
  id: string;
  actionType: string;
  actorUserId: string | null;
  actorRole: string | null;
  reason: string | null;
  createdAtUtc: string;
}

export interface AdminSellerReasonRequest {
  reason: string;
}
