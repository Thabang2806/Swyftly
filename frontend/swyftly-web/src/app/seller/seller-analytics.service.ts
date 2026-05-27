import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SellerAnalyticsCsvReport,
  SellerAnalyticsPerformanceRequest,
  SellerAnalyticsPerformanceResponse,
  SellerAnalyticsSummaryResponse,
  SellerReportDigestSendResult,
  SellerReportScheduleRequest,
  SellerReportScheduleResponse
} from './seller-analytics.models';

@Injectable({ providedIn: 'root' })
export class SellerAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/analytics`;

  getSummary(): Promise<SellerAnalyticsSummaryResponse> {
    return firstValueFrom(this.http.get<SellerAnalyticsSummaryResponse>(`${this.baseUrl}/summary`));
  }

  getPerformance(request: SellerAnalyticsPerformanceRequest = {}): Promise<SellerAnalyticsPerformanceResponse> {
    return firstValueFrom(this.http.get<SellerAnalyticsPerformanceResponse>(`${this.baseUrl}/performance`, {
      params: this.createParams(request)
    }));
  }

  getCsvExportUrl(report: SellerAnalyticsCsvReport, request: SellerAnalyticsPerformanceRequest = {}): string {
    const params = this.createParams({ ...request, report }).toString();
    return params ? `${this.baseUrl}/export.csv?${params}` : `${this.baseUrl}/export.csv`;
  }

  getReportSchedule(): Promise<SellerReportScheduleResponse> {
    return firstValueFrom(this.http.get<SellerReportScheduleResponse>(`${this.baseUrl}/report-schedule`));
  }

  updateReportSchedule(request: SellerReportScheduleRequest): Promise<SellerReportScheduleResponse> {
    return firstValueFrom(this.http.put<SellerReportScheduleResponse>(`${this.baseUrl}/report-schedule`, request));
  }

  sendTestReportDigest(): Promise<SellerReportDigestSendResult> {
    return firstValueFrom(this.http.post<SellerReportDigestSendResult>(
      `${this.baseUrl}/report-schedule/send-test`,
      {}));
  }

  private createParams(request: SellerAnalyticsPerformanceRequest & { report?: SellerAnalyticsCsvReport }): HttpParams {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(request)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    }

    return params;
  }
}
