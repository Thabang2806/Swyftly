import { AdminAuditLogResponse } from './admin-seller.models';

export interface AdminProductSummaryResponse {
  productId: string;
  sellerId: string;
  sellerDisplayName: string | null;
  sellerVerificationStatus: string | null;
  title: string | null;
  categoryPath: string | null;
  status: string;
  highRiskFlagCount: number;
  updatedAtUtc: string;
}

export interface AdminProductRevisionSummaryResponse {
  revisionId: string;
  productId: string;
  sellerId: string;
  sellerDisplayName: string | null;
  sellerVerificationStatus: string | null;
  currentTitle: string | null;
  proposedTitle: string | null;
  status: string;
  submittedAtUtc: string | null;
  updatedAtUtc: string;
}

export interface AdminProductDetailResponse {
  productId: string;
  sellerId: string;
  seller: AdminProductSellerResponse;
  categoryId: string | null;
  categoryPath: string | null;
  brandId: string | null;
  title: string | null;
  slug: string | null;
  shortDescription: string | null;
  fullDescription: string | null;
  tags: string[];
  status: string;
  rejectionReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  publishedAtUtc: string | null;
  attributes: Record<string, string>;
  variants: AdminProductVariantResponse[];
  images: AdminProductImageResponse[];
  moderationResults: AdminProductModerationResultResponse[];
  auditTrail: AdminAuditLogResponse[];
}

export interface AdminProductRevisionDetailResponse {
  revisionId: string;
  productId: string;
  sellerId: string;
  seller: AdminProductSellerResponse;
  status: string;
  rejectionReason: string | null;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  current: AdminProductListingSnapshotResponse;
  proposed: AdminProductListingSnapshotResponse;
  auditTrail: AdminAuditLogResponse[];
}

export interface AdminProductListingSnapshotResponse {
  categoryId: string | null;
  categoryPath: string | null;
  brandId: string | null;
  title: string | null;
  slug: string | null;
  shortDescription: string | null;
  fullDescription: string | null;
  tags: string[];
  attributes: Record<string, string>;
  images: AdminProductRevisionImageResponse[];
}

export interface AdminProductSellerResponse {
  displayName: string | null;
  contactEmail: string | null;
  verificationStatus: string | null;
}

export interface AdminProductVariantResponse {
  variantId: string;
  sku: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  reservedQuantity: number;
  status: string;
  availableQuantity: number;
}

export interface AdminProductImageResponse {
  imageId: string;
  url: string;
  altText: string | null;
  sortOrder: number;
  isPrimary: boolean;
  createdAtUtc: string;
}

export interface AdminProductRevisionImageResponse {
  imageId: string;
  url: string;
  altText: string | null;
  sortOrder: number;
  isPrimary: boolean;
  createdAtUtc: string;
}

export interface AdminProductModerationResultResponse {
  moderationResultId: string;
  riskLevel: string;
  needsAdminReview: boolean;
  reason: string;
  detectedTerms: string[];
  missingFields: string[];
  flags: string[];
  provider: string;
  createdAtUtc: string;
}

export interface AdminProductApproveRequest {
  overrideReason?: string | null;
}

export interface AdminProductReasonRequest {
  reason: string;
}
