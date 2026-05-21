import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminRefundResponse,
  ApproveAdminRefundRequest,
  ConfirmManualProviderRefundRequest,
  CreateAdminRefundRequest
} from './admin-refund.models';

@Injectable({ providedIn: 'root' })
export class AdminRefundService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin`;

  getRefunds(): Promise<AdminRefundResponse[]> {
    return firstValueFrom(this.http.get<AdminRefundResponse[]>(`${this.baseUrl}/refunds`));
  }

  createOrderRefund(orderId: string, request: CreateAdminRefundRequest): Promise<AdminRefundResponse> {
    return firstValueFrom(this.http.post<AdminRefundResponse>(`${this.baseUrl}/orders/${orderId}/refunds`, request));
  }

  createReturnRefund(returnRequestId: string, request: CreateAdminRefundRequest): Promise<AdminRefundResponse> {
    return firstValueFrom(this.http.post<AdminRefundResponse>(`${this.baseUrl}/returns/${returnRequestId}/refunds`, request));
  }

  approveRefund(refundId: string, request: ApproveAdminRefundRequest): Promise<AdminRefundResponse> {
    return firstValueFrom(this.http.post<AdminRefundResponse>(`${this.baseUrl}/refunds/${refundId}/approve`, request));
  }

  confirmManualProviderRefund(refundId: string, request: ConfirmManualProviderRefundRequest): Promise<AdminRefundResponse> {
    return firstValueFrom(
      this.http.post<AdminRefundResponse>(
        `${this.baseUrl}/refunds/${refundId}/confirm-manual-provider-refund`,
        request)
    );
  }
}
