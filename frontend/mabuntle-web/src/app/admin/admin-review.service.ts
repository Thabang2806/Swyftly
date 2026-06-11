import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminProductReviewDetailResponse, AdminProductReviewReasonRequest } from './admin-review.models';

@Injectable({ providedIn: 'root' })
export class AdminReviewService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/reviews`;

  getPendingReviews(): Promise<AdminProductReviewDetailResponse[]> {
    return firstValueFrom(this.http.get<AdminProductReviewDetailResponse[]>(`${this.baseUrl}/pending`));
  }

  getReview(reviewId: string): Promise<AdminProductReviewDetailResponse> {
    return firstValueFrom(this.http.get<AdminProductReviewDetailResponse>(`${this.baseUrl}/${reviewId}`));
  }

  approveReview(reviewId: string): Promise<AdminProductReviewDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductReviewDetailResponse>(`${this.baseUrl}/${reviewId}/approve`, null));
  }

  rejectReview(reviewId: string, request: AdminProductReviewReasonRequest): Promise<AdminProductReviewDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductReviewDetailResponse>(`${this.baseUrl}/${reviewId}/reject`, request));
  }

  removeReview(reviewId: string, request: AdminProductReviewReasonRequest): Promise<AdminProductReviewDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductReviewDetailResponse>(`${this.baseUrl}/${reviewId}/remove`, request));
  }
}
