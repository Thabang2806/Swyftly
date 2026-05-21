import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminProductApproveRequest,
  AdminProductDetailResponse,
  AdminProductReasonRequest,
  AdminProductRevisionDetailResponse,
  AdminProductRevisionSummaryResponse,
  AdminProductSummaryResponse
} from './admin-product.models';

@Injectable({ providedIn: 'root' })
export class AdminProductService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/products`;

  getPendingReviewProducts(): Promise<AdminProductSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminProductSummaryResponse[]>(`${this.baseUrl}/pending-review`));
  }

  getPendingRevisions(): Promise<AdminProductRevisionSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminProductRevisionSummaryResponse[]>(`${this.baseUrl}/pending-revisions`));
  }

  getProduct(productId: string): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.get<AdminProductDetailResponse>(`${this.baseUrl}/${productId}`));
  }

  getRevision(revisionId: string): Promise<AdminProductRevisionDetailResponse> {
    return firstValueFrom(this.http.get<AdminProductRevisionDetailResponse>(`${this.baseUrl}/revisions/${revisionId}`));
  }

  approveProduct(productId: string, request: AdminProductApproveRequest = {}): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductDetailResponse>(`${this.baseUrl}/${productId}/approve`, request));
  }

  rejectProduct(productId: string, request: AdminProductReasonRequest): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductDetailResponse>(`${this.baseUrl}/${productId}/reject`, request));
  }

  requestChanges(productId: string, request: AdminProductReasonRequest): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductDetailResponse>(`${this.baseUrl}/${productId}/request-changes`, request));
  }

  approveRevision(revisionId: string): Promise<AdminProductRevisionDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductRevisionDetailResponse>(`${this.baseUrl}/revisions/${revisionId}/approve`, {}));
  }

  rejectRevision(revisionId: string, request: AdminProductReasonRequest): Promise<AdminProductRevisionDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductRevisionDetailResponse>(`${this.baseUrl}/revisions/${revisionId}/reject`, request));
  }
}
