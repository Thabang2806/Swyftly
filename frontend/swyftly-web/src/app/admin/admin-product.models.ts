import { AdminAuditLogResponse } from './admin-seller.models';
import { AdminQueueTriageFields } from './admin-queue-triage.models';

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

export interface AdminProductVariantRevisionSummaryResponse {
  revisionId: string;
  productId: string;
  sellerId: string;
  sellerDisplayName: string | null;
  sellerVerificationStatus: string | null;
  productTitle: string | null;
  status: string;
  itemCount: number;
  submittedAtUtc: string | null;
  updatedAtUtc: string;
}

export type AdminProductModerationItemType = 'Product' | 'ListingRevision' | 'VariantRevision';

export interface AdminProductModerationItemResponse extends AdminQueueTriageFields {
  id: string;
  itemType: AdminProductModerationItemType;
  productId: string;
  revisionId: string | null;
  sellerId: string;
  sellerDisplayName: string | null;
  sellerVerificationStatus: string | null;
  title: string | null;
  categoryPath: string | null;
  status: string;
  submittedAtUtc: string | null;
  updatedAtUtc: string;
  riskFlagCount: number;
  itemCount: number;
  detailRoute: string;
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
  seoTitle?: string | null;
  seoDescription?: string | null;
  merchandisingLabel?: string | null;
  careInstructions?: string | null;
  productDisclaimer?: string | null;
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

export interface AdminProductVariantRevisionDetailResponse {
  revisionId: string;
  productId: string;
  sellerId: string;
  seller: AdminProductSellerResponse;
  productTitle: string | null;
  productSlug: string | null;
  status: string;
  sellerReason: string | null;
  rejectionReason: string | null;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  currentVariants: AdminProductVariantRevisionFinalVariantResponse[];
  items: AdminProductVariantRevisionItemResponse[];
  proposedFinalVariants: AdminProductVariantRevisionFinalVariantResponse[];
  validationErrors: Record<string, string[]>;
  auditTrail: AdminAuditLogResponse[];
}

export interface AdminProductVariantRevisionItemResponse {
  revisionItemId: string;
  operation: string;
  sourceVariantId: string | null;
  sku: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  initialStockQuantity: number | null;
  proposedStatus: string;
  barcode: string | null;
}

export interface AdminProductVariantRevisionFinalVariantResponse {
  sourceVariantId: string | null;
  changeType: string;
  sku: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  reservedQuantity: number;
  status: string;
  barcode: string | null;
  availableQuantity: number;
}

export interface AdminProductListingSnapshotResponse {
  categoryId: string | null;
  categoryPath: string | null;
  brandId: string | null;
  title: string | null;
  slug: string | null;
  shortDescription: string | null;
  fullDescription: string | null;
  seoTitle?: string | null;
  seoDescription?: string | null;
  merchandisingLabel?: string | null;
  careInstructions?: string | null;
  productDisclaimer?: string | null;
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
