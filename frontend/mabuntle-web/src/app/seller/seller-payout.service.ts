import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { SellerBalanceResponse, SellerPayoutResponse } from './seller-payout.models';

@Injectable({ providedIn: 'root' })
export class SellerPayoutService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller`;

  getBalance(): Promise<SellerBalanceResponse> {
    return firstValueFrom(this.http.get<SellerBalanceResponse>(`${this.baseUrl}/balance`));
  }

  listPayouts(): Promise<SellerPayoutResponse[]> {
    return firstValueFrom(this.http.get<SellerPayoutResponse[]>(`${this.baseUrl}/payouts`));
  }
}
