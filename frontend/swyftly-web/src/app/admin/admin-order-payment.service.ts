import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminOrderDetailResponse,
  AdminOrderSummaryResponse,
  AdminPaymentDetailResponse,
  AdminPaymentReconciliationCandidateResponse,
  AdminPaymentReconciliationReviewResponse,
  AdminPaymentSummaryResponse,
  CreatePaymentReconciliationReviewRequest
} from './admin-order-payment.models';

@Injectable({ providedIn: 'root' })
export class AdminOrderPaymentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin`;

  getOrders(status?: string): Promise<AdminOrderSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminOrderSummaryResponse[]>(`${this.baseUrl}/orders`, {
      params: this.statusParams(status)
    }));
  }

  getOrder(orderId: string): Promise<AdminOrderDetailResponse> {
    return firstValueFrom(this.http.get<AdminOrderDetailResponse>(`${this.baseUrl}/orders/${orderId}`));
  }

  getPayments(status?: string, orderId?: string): Promise<AdminPaymentSummaryResponse[]> {
    let params = this.statusParams(status);
    if (orderId?.trim()) {
      params = params.set('orderId', orderId.trim());
    }

    return firstValueFrom(this.http.get<AdminPaymentSummaryResponse[]>(`${this.baseUrl}/payments`, { params }));
  }

  getPayment(paymentId: string): Promise<AdminPaymentDetailResponse> {
    return firstValueFrom(this.http.get<AdminPaymentDetailResponse>(`${this.baseUrl}/payments/${paymentId}`));
  }

  getPaymentReconciliationCandidates(olderThanMinutes = 30, includeSnoozed = false): Promise<AdminPaymentReconciliationCandidateResponse[]> {
    const params = new HttpParams()
      .set('olderThanMinutes', olderThanMinutes.toString())
      .set('includeSnoozed', includeSnoozed.toString());
    return firstValueFrom(
      this.http.get<AdminPaymentReconciliationCandidateResponse[]>(`${this.baseUrl}/payments/reconciliation-candidates`, { params })
    );
  }

  createPaymentReconciliationReview(
    paymentId: string,
    request: CreatePaymentReconciliationReviewRequest
  ): Promise<AdminPaymentReconciliationReviewResponse> {
    return firstValueFrom(
      this.http.post<AdminPaymentReconciliationReviewResponse>(
        `${this.baseUrl}/payments/${paymentId}/reconciliation-reviews`,
        request)
    );
  }

  private statusParams(status?: string): HttpParams {
    const trimmed = status?.trim();
    return trimmed ? new HttpParams().set('status', trimmed) : new HttpParams();
  }
}
