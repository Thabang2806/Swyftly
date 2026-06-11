import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { BuyerOrderResult } from './buyer-order.models';

@Injectable({ providedIn: 'root' })
export class BuyerOrderService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer/orders`;

  listOrders(): Promise<BuyerOrderResult[]> {
    return firstValueFrom(this.http.get<BuyerOrderResult[]>(this.baseUrl));
  }

  getOrder(orderId: string): Promise<BuyerOrderResult> {
    return firstValueFrom(this.http.get<BuyerOrderResult>(`${this.baseUrl}/${orderId}`));
  }
}
