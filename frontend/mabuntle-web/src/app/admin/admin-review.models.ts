import { AdminAuditLogResponse } from './admin-seller.models';

export interface AdminProductReviewDetailResponse {
  reviewId: string;
  buyerId: string;
  sellerId: string;
  productId: string;
  orderId: string;
  orderItemId: string;
  rating: number;
  title: string | null;
  body: string | null;
  status: string;
  moderationReason: string | null;
  moderatedByUserId: string | null;
  moderatedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  product: AdminProductReviewProductResponse;
  seller: AdminProductReviewSellerResponse;
  buyer: AdminProductReviewBuyerResponse;
  order: AdminProductReviewOrderResponse;
  auditTrail: AdminAuditLogResponse[];
}

export interface AdminProductReviewProductResponse {
  title: string | null;
  slug: string | null;
  categoryId: string | null;
  primaryImageUrl: string | null;
  primaryImageAltText: string | null;
}

export interface AdminProductReviewSellerResponse {
  displayName: string | null;
  contactEmail: string | null;
  verificationStatus: string | null;
}

export interface AdminProductReviewBuyerResponse {
  userId: string | null;
}

export interface AdminProductReviewOrderResponse {
  status: string | null;
  totalAmount: number | null;
  productTitle: string | null;
  sku: string | null;
  size: string | null;
  colour: string | null;
  quantity: number | null;
}

export interface AdminProductReviewReasonRequest {
  reason: string;
}
