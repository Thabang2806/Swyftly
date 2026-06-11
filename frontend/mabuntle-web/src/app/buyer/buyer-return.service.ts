import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerReturnRequestResult,
  CreateBuyerReturnRequest,
  DisputeBuyerReturnRequest
} from './buyer-return.models';

@Injectable({ providedIn: 'root' })
export class BuyerReturnService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer`;

  listReturns(): Promise<BuyerReturnRequestResult[]> {
    return firstValueFrom(this.http.get<BuyerReturnRequestResult[]>(`${this.baseUrl}/returns`));
  }

  getReturn(returnRequestId: string): Promise<BuyerReturnRequestResult> {
    return firstValueFrom(this.http.get<BuyerReturnRequestResult>(`${this.baseUrl}/returns/${returnRequestId}`));
  }

  createReturn(orderId: string, request: CreateBuyerReturnRequest): Promise<BuyerReturnRequestResult> {
    return firstValueFrom(this.http.post<BuyerReturnRequestResult>(`${this.baseUrl}/orders/${orderId}/returns`, request));
  }

  disputeReturn(returnRequestId: string, request: DisputeBuyerReturnRequest): Promise<BuyerReturnRequestResult> {
    return firstValueFrom(this.http.post<BuyerReturnRequestResult>(`${this.baseUrl}/returns/${returnRequestId}/dispute`, request));
  }
}
