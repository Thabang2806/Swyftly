import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminPayoutProfileChangeRequestResponse,
  AdminPayoutProfileChangeReviewRequest
} from './admin-payout-profile-change.models';

@Injectable({ providedIn: 'root' })
export class AdminPayoutProfileChangeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/sellers/payout-profile-change-requests`;

  list(): Promise<AdminPayoutProfileChangeRequestResponse[]> {
    return firstValueFrom(this.http.get<AdminPayoutProfileChangeRequestResponse[]>(this.baseUrl));
  }

  get(requestId: string): Promise<AdminPayoutProfileChangeRequestResponse> {
    return firstValueFrom(this.http.get<AdminPayoutProfileChangeRequestResponse>(`${this.baseUrl}/${requestId}`));
  }

  approve(
    requestId: string,
    request: AdminPayoutProfileChangeReviewRequest
  ): Promise<AdminPayoutProfileChangeRequestResponse> {
    return this.review(requestId, 'approve', request);
  }

  reject(
    requestId: string,
    request: AdminPayoutProfileChangeReviewRequest
  ): Promise<AdminPayoutProfileChangeRequestResponse> {
    return this.review(requestId, 'reject', request);
  }

  private review(
    requestId: string,
    action: 'approve' | 'reject',
    request: AdminPayoutProfileChangeReviewRequest
  ): Promise<AdminPayoutProfileChangeRequestResponse> {
    return firstValueFrom(this.http.post<AdminPayoutProfileChangeRequestResponse>(
      `${this.baseUrl}/${requestId}/${action}`,
      request));
  }
}
