import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminOperationalListQuery, AdminPagedResponse } from './admin-operational-list.models';
import {
  AdminAdCampaignDetailResponse,
  AdminAdCampaignOperationalSummaryResponse,
  AdminAdCampaignReasonRequest,
  AdminAdCampaignSummaryResponse
} from './admin-ad-campaign.models';

@Injectable({ providedIn: 'root' })
export class AdminAdCampaignService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/ad-campaigns`;

  getCampaigns(query: AdminOperationalListQuery = {}): Promise<AdminPagedResponse<AdminAdCampaignOperationalSummaryResponse>> {
    return firstValueFrom(
      this.http.get<AdminPagedResponse<AdminAdCampaignOperationalSummaryResponse>>(this.baseUrl, {
        params: buildAdminOperationalParams(query)
      })
    );
  }

  getPendingCampaigns(): Promise<AdminAdCampaignSummaryResponse[]> {
    return firstValueFrom(this.http.get<AdminAdCampaignSummaryResponse[]>(`${this.baseUrl}/pending`));
  }

  getCampaign(campaignId: string): Promise<AdminAdCampaignDetailResponse> {
    return firstValueFrom(this.http.get<AdminAdCampaignDetailResponse>(`${this.baseUrl}/${campaignId}`));
  }

  approveCampaign(campaignId: string): Promise<AdminAdCampaignDetailResponse> {
    return firstValueFrom(this.http.post<AdminAdCampaignDetailResponse>(`${this.baseUrl}/${campaignId}/approve`, {}));
  }

  rejectCampaign(
    campaignId: string,
    request: AdminAdCampaignReasonRequest): Promise<AdminAdCampaignDetailResponse> {
    return firstValueFrom(this.http.post<AdminAdCampaignDetailResponse>(`${this.baseUrl}/${campaignId}/reject`, request));
  }
}

function buildAdminOperationalParams(query: AdminOperationalListQuery): HttpParams {
  let params = new HttpParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  });

  return params;
}
