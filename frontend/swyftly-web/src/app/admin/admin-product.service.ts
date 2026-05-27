import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminOperationalListQuery, AdminPagedResponse } from './admin-operational-list.models';
import {
  AdminProductApproveRequest,
  AdminProductDetailResponse,
  AdminProductModerationItemResponse,
  AdminProductReasonRequest,
  AdminProductRevisionDetailResponse,
  AdminProductRevisionSummaryResponse,
  AdminProductSummaryResponse,
  AdminProductVariantRevisionDetailResponse,
  AdminProductVariantRevisionSummaryResponse
} from './admin-product.models';

@Injectable({ providedIn: 'root' })
export class AdminProductService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/products`;

  getModerationItems(query: AdminOperationalListQuery = {}): Promise<AdminPagedResponse<AdminProductModerationItemResponse>> {
    return firstValueFrom(
      this.http.get<AdminPagedResponse<AdminProductModerationItemResponse>>(`${this.baseUrl}/moderation-items`, {
        params: buildAdminOperationalParams(query)
      })
    );
  }

  getPendingReviewProducts(): Promise<AdminProductSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminProductSummaryResponse[]>(`${this.baseUrl}/pending-review`));
  }

  getPendingRevisions(): Promise<AdminProductRevisionSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminProductRevisionSummaryResponse[]>(`${this.baseUrl}/pending-revisions`));
  }

  getPendingVariantRevisions(): Promise<AdminProductVariantRevisionSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminProductVariantRevisionSummaryResponse[]>(`${this.baseUrl}/pending-variant-revisions`));
  }

  getProduct(productId: string): Promise<AdminProductDetailResponse> {
    return firstValueFrom(this.http.get<AdminProductDetailResponse>(`${this.baseUrl}/${productId}`));
  }

  getRevision(revisionId: string): Promise<AdminProductRevisionDetailResponse> {
    return firstValueFrom(this.http.get<AdminProductRevisionDetailResponse>(`${this.baseUrl}/revisions/${revisionId}`));
  }

  getVariantRevision(revisionId: string): Promise<AdminProductVariantRevisionDetailResponse> {
    return firstValueFrom(this.http.get<AdminProductVariantRevisionDetailResponse>(`${this.baseUrl}/variant-revisions/${revisionId}`));
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

  approveVariantRevision(revisionId: string): Promise<AdminProductVariantRevisionDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductVariantRevisionDetailResponse>(`${this.baseUrl}/variant-revisions/${revisionId}/approve`, {}));
  }

  rejectVariantRevision(revisionId: string, request: AdminProductReasonRequest): Promise<AdminProductVariantRevisionDetailResponse> {
    return firstValueFrom(this.http.post<AdminProductVariantRevisionDetailResponse>(`${this.baseUrl}/variant-revisions/${revisionId}/reject`, request));
  }
}

function buildAdminOperationalParams(query: AdminOperationalListQuery): HttpParams {
  let params = new HttpParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  });

  return params;
}
