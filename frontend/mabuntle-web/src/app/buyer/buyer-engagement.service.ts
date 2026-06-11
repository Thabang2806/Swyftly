import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerNotificationResponse,
  BuyerNotificationUnreadCountResponse,
  BuyerProductReviewResponse,
  BuyerWishlistProductIdsResponse,
  BuyerWishlistItemResponse,
  MoveWishlistItemToCartRequest,
  NotificationsReadAllResponse,
  ProductReviewRequest,
  PublicProductReviewResponse,
  PublicProductReviewSummaryResponse
} from './buyer-engagement.models';
import { CartResponse } from '../cart/cart.models';

@Injectable({ providedIn: 'root' })
export class BuyerEngagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  listWishlist(): Promise<BuyerWishlistItemResponse[]> {
    return firstValueFrom(this.http.get<BuyerWishlistItemResponse[]>(`${this.baseUrl}/api/buyer/wishlist`));
  }

  listWishlistProductIds(): Promise<BuyerWishlistProductIdsResponse> {
    return firstValueFrom(this.http.get<BuyerWishlistProductIdsResponse>(`${this.baseUrl}/api/buyer/wishlist/product-ids`));
  }

  addWishlistItem(productId: string): Promise<BuyerWishlistItemResponse> {
    return firstValueFrom(this.http.post<BuyerWishlistItemResponse>(`${this.baseUrl}/api/buyer/wishlist/${productId}`, null));
  }

  moveWishlistItemToCart(productId: string, request: MoveWishlistItemToCartRequest): Promise<CartResponse> {
    return firstValueFrom(this.http.post<CartResponse>(`${this.baseUrl}/api/buyer/wishlist/${productId}/move-to-cart`, request));
  }

  removeWishlistItem(productId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/api/buyer/wishlist/${productId}`));
  }

  listBuyerReviews(): Promise<BuyerProductReviewResponse[]> {
    return firstValueFrom(this.http.get<BuyerProductReviewResponse[]>(`${this.baseUrl}/api/buyer/reviews`));
  }

  createReview(orderId: string, orderItemId: string, request: ProductReviewRequest): Promise<BuyerProductReviewResponse> {
    return firstValueFrom(
      this.http.post<BuyerProductReviewResponse>(
        `${this.baseUrl}/api/buyer/orders/${orderId}/items/${orderItemId}/review`,
        request));
  }

  updateReview(reviewId: string, request: ProductReviewRequest): Promise<BuyerProductReviewResponse> {
    return firstValueFrom(this.http.put<BuyerProductReviewResponse>(`${this.baseUrl}/api/buyer/reviews/${reviewId}`, request));
  }

  deleteReview(reviewId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/api/buyer/reviews/${reviewId}`));
  }

  listProductReviews(slug: string): Promise<PublicProductReviewResponse[]> {
    return firstValueFrom(this.http.get<PublicProductReviewResponse[]>(`${this.baseUrl}/api/products/${slug}/reviews`));
  }

  getProductReviewSummary(slug: string): Promise<PublicProductReviewSummaryResponse> {
    return firstValueFrom(this.http.get<PublicProductReviewSummaryResponse>(`${this.baseUrl}/api/products/${slug}/review-summary`));
  }

  listNotifications(): Promise<BuyerNotificationResponse[]> {
    return firstValueFrom(this.http.get<BuyerNotificationResponse[]>(`${this.baseUrl}/api/buyer/notifications`));
  }

  getUnreadNotificationCount(): Promise<BuyerNotificationUnreadCountResponse> {
    return firstValueFrom(this.http.get<BuyerNotificationUnreadCountResponse>(`${this.baseUrl}/api/buyer/notifications/unread-count`));
  }

  markNotificationRead(notificationId: string): Promise<BuyerNotificationResponse> {
    return firstValueFrom(this.http.post<BuyerNotificationResponse>(`${this.baseUrl}/api/buyer/notifications/${notificationId}/read`, null));
  }

  markAllNotificationsRead(): Promise<NotificationsReadAllResponse> {
    return firstValueFrom(this.http.post<NotificationsReadAllResponse>(`${this.baseUrl}/api/buyer/notifications/read-all`, null));
  }
}
