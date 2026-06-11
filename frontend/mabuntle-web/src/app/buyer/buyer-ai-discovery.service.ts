import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerAiDiscoveryHistoryListResponse,
  BuyerAiDiscoveryHistoryQuery,
  BuyerAiDiscoveryPreferenceRequest,
  BuyerAiDiscoveryPreferenceResponse
} from './buyer-ai-discovery.models';

@Injectable({ providedIn: 'root' })
export class BuyerAiDiscoveryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer/ai-discovery`;

  getPreferences(): Promise<BuyerAiDiscoveryPreferenceResponse> {
    return firstValueFrom(this.http.get<BuyerAiDiscoveryPreferenceResponse>(`${this.baseUrl}/preferences`));
  }

  updatePreferences(request: BuyerAiDiscoveryPreferenceRequest): Promise<BuyerAiDiscoveryPreferenceResponse> {
    return firstValueFrom(this.http.put<BuyerAiDiscoveryPreferenceResponse>(`${this.baseUrl}/preferences`, request));
  }

  getHistory(query: BuyerAiDiscoveryHistoryQuery = {}): Promise<BuyerAiDiscoveryHistoryListResponse> {
    let params = new HttpParams();
    if (query.page) {
      params = params.set('page', query.page);
    }

    if (query.pageSize) {
      params = params.set('pageSize', query.pageSize);
    }

    if (query.tool) {
      params = params.set('tool', query.tool);
    }

    return firstValueFrom(this.http.get<BuyerAiDiscoveryHistoryListResponse>(`${this.baseUrl}/history`, { params }));
  }

  deleteHistoryItem(historyId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/history/${historyId}`));
  }

  clearHistory(): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/history`));
  }
}
