import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { BuyerRefundResult } from './buyer-refund.models';

@Injectable({ providedIn: 'root' })
export class BuyerRefundService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer`;

  listRefunds(): Promise<BuyerRefundResult[]> {
    return firstValueFrom(this.http.get<BuyerRefundResult[]>(`${this.baseUrl}/refunds`));
  }

  getRefund(refundId: string): Promise<BuyerRefundResult> {
    return firstValueFrom(this.http.get<BuyerRefundResult>(`${this.baseUrl}/refunds/${refundId}`));
  }

  listOrderRefunds(orderId: string): Promise<BuyerRefundResult[]> {
    return firstValueFrom(this.http.get<BuyerRefundResult[]>(`${this.baseUrl}/orders/${orderId}/refunds`));
  }

  listReturnRefunds(returnRequestId: string): Promise<BuyerRefundResult[]> {
    return firstValueFrom(this.http.get<BuyerRefundResult[]>(`${this.baseUrl}/returns/${returnRequestId}/refunds`));
  }
}
