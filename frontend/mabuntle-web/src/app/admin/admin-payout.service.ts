import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminPayoutReasonRequest, AdminPayoutResponse } from './admin-payout.models';

@Injectable({ providedIn: 'root' })
export class AdminPayoutService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/payouts`;

  getPendingPayouts(): Promise<AdminPayoutResponse[]> {
    return firstValueFrom(this.http.get<AdminPayoutResponse[]>(`${this.baseUrl}/pending`));
  }

  holdPayout(payoutId: string, request: AdminPayoutReasonRequest): Promise<AdminPayoutResponse> {
    return this.postPayoutAction(payoutId, 'hold', request);
  }

  releasePayout(payoutId: string, request: AdminPayoutReasonRequest): Promise<AdminPayoutResponse> {
    return this.postPayoutAction(payoutId, 'release', request);
  }

  makePayoutAvailable(payoutId: string, request: AdminPayoutReasonRequest): Promise<AdminPayoutResponse> {
    return this.postPayoutAction(payoutId, 'make-available', request);
  }

  processPayout(payoutId: string, request: AdminPayoutReasonRequest): Promise<AdminPayoutResponse> {
    return this.postPayoutAction(payoutId, 'process', request);
  }

  reconcilePayout(payoutId: string, request: AdminPayoutReasonRequest): Promise<AdminPayoutResponse> {
    return this.postPayoutAction(payoutId, 'reconcile', request);
  }

  private postPayoutAction(
    payoutId: string,
    action: string,
    request: AdminPayoutReasonRequest
  ): Promise<AdminPayoutResponse> {
    return firstValueFrom(this.http.post<AdminPayoutResponse>(`${this.baseUrl}/${payoutId}/${action}`, request));
  }
}
