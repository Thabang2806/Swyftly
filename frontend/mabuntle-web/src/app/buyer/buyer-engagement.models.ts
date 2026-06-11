import { ProductSearchItemResponse } from '../shop/public-catalog.models';

export interface BuyerWishlistItemResponse {
  wishlistItemId: string;
  createdAtUtc: string;
  product: ProductSearchItemResponse;
  availableVariants: BuyerWishlistVariantOptionResponse[];
}

export interface BuyerWishlistProductIdsResponse {
  productIds: string[];
}

export interface BuyerWishlistVariantOptionResponse {
  productVariantId: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  inStock: boolean;
  availableQuantity: number;
}

export interface MoveWishlistItemToCartRequest {
  productVariantId: string;
  quantity: number;
}

export interface ProductReviewRequest {
  rating: number;
  title: string | null;
  body: string | null;
}

export interface BuyerProductReviewResponse {
  reviewId: string;
  productId: string;
  orderId: string;
  orderItemId: string;
  rating: number;
  title: string | null;
  body: string | null;
  status: string;
  moderationReason: string | null;
  moderatedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  product: BuyerReviewProductSummaryResponse | null;
}

export interface BuyerReviewProductSummaryResponse {
  productId: string;
  sellerId: string;
  title: string | null;
  slug: string | null;
  primaryImageUrl: string | null;
  primaryImageAltText: string | null;
}

export interface PublicProductReviewResponse {
  reviewId: string;
  productId: string;
  rating: number;
  title: string | null;
  body: string | null;
  createdAtUtc: string;
}

export interface PublicProductReviewSummaryResponse {
  productId: string;
  reviewCount: number;
  averageRating: number;
  ratingCounts: ProductReviewRatingCountResponse[];
}

export interface ProductReviewRatingCountResponse {
  rating: number;
  count: number;
}

export interface BuyerNotificationResponse {
  notificationId: string;
  recipientUserId: string;
  type: string;
  title: string;
  message: string;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
  readAtUtc: string | null;
  createdAtUtc: string;
}

export interface BuyerNotificationUnreadCountResponse {
  unreadCount: number;
}

export interface NotificationReadRealtimeEvent {
  notificationId: string;
  readAtUtc: string;
}

export interface NotificationsReadAllRealtimeEvent {
  readAtUtc: string;
  updatedCount: number;
}

export interface NotificationsReadAllResponse {
  updatedCount: number;
}
