import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminQueueSavedViewRequest, AdminQueueSavedViewResponse } from './admin-moderation-queue.models';
import {
  AdminSupportEscalationRequest,
  AdminSupportMessageRequest,
  AdminSupportQualityReportFilters,
  AdminSupportQualityReportResponse,
  AdminSupportQueueFilters,
  AdminSupportQueueResponse,
  AdminSupportSummaryResponse,
  AdminSupportTriageRequest,
  AdminSupportTicketResponse
} from './admin-support.models';

@Injectable({ providedIn: 'root' })
export class AdminSupportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/support/tickets`;

  listTickets(): Promise<AdminSupportTicketResponse[]> {
    return firstValueFrom(this.http.get<AdminSupportTicketResponse[]>(this.baseUrl));
  }

  listQueue(filters: AdminSupportQueueFilters = {}): Promise<AdminSupportQueueResponse> {
    return firstValueFrom(this.http.get<AdminSupportQueueResponse>(`${this.baseUrl}/queue`, { params: this.toParams(filters) }));
  }

  getSummary(): Promise<AdminSupportSummaryResponse> {
    return firstValueFrom(this.http.get<AdminSupportSummaryResponse>(`${this.baseUrl}/summary`));
  }

  exportQueue(filters: AdminSupportQueueFilters = {}): Promise<Blob> {
    return firstValueFrom(this.http.get(`${this.baseUrl}/queue/export.csv`, {
      params: this.toParams(filters),
      responseType: 'blob'
    }));
  }

  getQualityReport(filters: AdminSupportQualityReportFilters = {}): Promise<AdminSupportQualityReportResponse> {
    return firstValueFrom(this.http.get<AdminSupportQualityReportResponse>(`${this.baseUrl}/quality-report`, { params: this.toParams(filters) }));
  }

  exportQualityReport(filters: AdminSupportQualityReportFilters = {}): Promise<Blob> {
    return firstValueFrom(this.http.get(`${this.baseUrl}/quality-report/export.csv`, {
      params: this.toParams(filters),
      responseType: 'blob'
    }));
  }

  getSavedViews(): Promise<AdminQueueSavedViewResponse[]> {
    return firstValueFrom(this.http.get<AdminQueueSavedViewResponse[]>(`${this.baseUrl}/views`));
  }

  createSavedView(request: AdminQueueSavedViewRequest): Promise<AdminQueueSavedViewResponse> {
    return firstValueFrom(this.http.post<AdminQueueSavedViewResponse>(`${this.baseUrl}/views`, request));
  }

  updateSavedView(viewId: string, request: AdminQueueSavedViewRequest): Promise<AdminQueueSavedViewResponse> {
    return firstValueFrom(this.http.put<AdminQueueSavedViewResponse>(`${this.baseUrl}/views/${viewId}`, request));
  }

  deleteSavedView(viewId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/views/${viewId}`));
  }

  makeDefault(viewId: string): Promise<AdminQueueSavedViewResponse> {
    return firstValueFrom(this.http.post<AdminQueueSavedViewResponse>(`${this.baseUrl}/views/${viewId}/make-default`, {}));
  }

  getTicket(ticketId: string): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.get<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}`));
  }

  addPublicMessage(ticketId: string, request: AdminSupportMessageRequest): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/messages`, request));
  }

  addInternalNote(ticketId: string, request: AdminSupportMessageRequest): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/internal-notes`, request));
  }

  claimTicket(ticketId: string, force = false): Promise<AdminSupportTicketResponse> {
    const url = force ? `${this.baseUrl}/${ticketId}/claim?force=true` : `${this.baseUrl}/${ticketId}/claim`;
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(url, {}));
  }

  unclaimTicket(ticketId: string): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/unclaim`, {}));
  }

  triageTicket(ticketId: string, request: AdminSupportTriageRequest): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.put<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/triage`, request));
  }

  escalateTicket(ticketId: string, request: AdminSupportEscalationRequest): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/escalate`, request));
  }

  resolveTicket(ticketId: string): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/resolve`, {}));
  }

  closeTicket(ticketId: string): Promise<AdminSupportTicketResponse> {
    return firstValueFrom(this.http.post<AdminSupportTicketResponse>(`${this.baseUrl}/${ticketId}/close`, {}));
  }

  private toParams(filters: AdminSupportQueueFilters): Record<string, string> {
    const params: Record<string, string> = {};
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== null && `${value}`.trim().length > 0) {
        params[key] = `${value}`;
      }
    }

    return params;
  }
}
