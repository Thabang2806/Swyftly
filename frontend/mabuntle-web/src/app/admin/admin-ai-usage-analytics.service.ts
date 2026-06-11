import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminAiUsageAnalyticsRequest,
  AdminAiUsageAnalyticsResponse
} from './admin-ai-usage-analytics.models';

@Injectable({ providedIn: 'root' })
export class AdminAiUsageAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/analytics/ai-usage`;

  getAnalytics(request: AdminAiUsageAnalyticsRequest = {}): Promise<AdminAiUsageAnalyticsResponse> {
    return firstValueFrom(this.http.get<AdminAiUsageAnalyticsResponse>(this.baseUrl, {
      params: this.createParams(request)
    }));
  }

  private createParams(request: AdminAiUsageAnalyticsRequest): HttpParams {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(request)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    }

    return params;
  }
}
