import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { SellerDashboardSummaryResponse } from './seller-dashboard.models';

@Injectable({ providedIn: 'root' })
export class SellerDashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/dashboard`;

  getSummary(): Promise<SellerDashboardSummaryResponse> {
    return firstValueFrom(this.http.get<SellerDashboardSummaryResponse>(`${this.baseUrl}/summary`));
  }
}
