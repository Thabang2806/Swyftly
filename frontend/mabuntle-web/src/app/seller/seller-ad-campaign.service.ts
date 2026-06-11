import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SellerAdCampaignMetricsResponse,
  SellerAdCampaignResponse,
  UpsertSellerAdCampaignRequest
} from './seller-ad-campaign.models';

@Injectable({ providedIn: 'root' })
export class SellerAdCampaignService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/ad-campaigns`;

  listCampaigns(): Promise<SellerAdCampaignResponse[]> {
    return firstValueFrom(this.http.get<SellerAdCampaignResponse[]>(this.baseUrl));
  }

  getCampaign(campaignId: string): Promise<SellerAdCampaignResponse> {
    return firstValueFrom(this.http.get<SellerAdCampaignResponse>(`${this.baseUrl}/${campaignId}`));
  }

  createCampaign(request: UpsertSellerAdCampaignRequest): Promise<SellerAdCampaignResponse> {
    return firstValueFrom(this.http.post<SellerAdCampaignResponse>(this.baseUrl, request));
  }

  updateCampaign(campaignId: string, request: UpsertSellerAdCampaignRequest): Promise<SellerAdCampaignResponse> {
    return firstValueFrom(this.http.put<SellerAdCampaignResponse>(`${this.baseUrl}/${campaignId}`, request));
  }

  submitForReview(campaignId: string): Promise<SellerAdCampaignResponse> {
    return firstValueFrom(this.http.post<SellerAdCampaignResponse>(`${this.baseUrl}/${campaignId}/submit-review`, {}));
  }

  pauseCampaign(campaignId: string): Promise<SellerAdCampaignResponse> {
    return firstValueFrom(this.http.post<SellerAdCampaignResponse>(`${this.baseUrl}/${campaignId}/pause`, {}));
  }

  resumeCampaign(campaignId: string): Promise<SellerAdCampaignResponse> {
    return firstValueFrom(this.http.post<SellerAdCampaignResponse>(`${this.baseUrl}/${campaignId}/resume`, {}));
  }

  cancelCampaign(campaignId: string): Promise<SellerAdCampaignResponse> {
    return firstValueFrom(this.http.post<SellerAdCampaignResponse>(`${this.baseUrl}/${campaignId}/cancel`, {}));
  }

  getMetrics(campaignId: string): Promise<SellerAdCampaignMetricsResponse> {
    return firstValueFrom(this.http.get<SellerAdCampaignMetricsResponse>(`${this.baseUrl}/${campaignId}/metrics`));
  }
}
