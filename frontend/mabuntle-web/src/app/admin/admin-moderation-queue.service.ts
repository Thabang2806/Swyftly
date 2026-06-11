import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminQueueKey,
  AdminQueueSavedViewRequest,
  AdminQueueSavedViewResponse,
  AdminQueueSummaryResponse
} from './admin-moderation-queue.models';

@Injectable({ providedIn: 'root' })
export class AdminModerationQueueService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/moderation-queue`;

  getSavedViews(queue?: AdminQueueKey | string): Promise<AdminQueueSavedViewResponse[]> {
    const params = queue ? new HttpParams().set('queue', queue) : undefined;
    return firstValueFrom(this.http.get<AdminQueueSavedViewResponse[]>(`${this.baseUrl}/views`, { params }));
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

  getSummary(): Promise<AdminQueueSummaryResponse> {
    return firstValueFrom(this.http.get<AdminQueueSummaryResponse>(`${this.baseUrl}/summary`));
  }
}
