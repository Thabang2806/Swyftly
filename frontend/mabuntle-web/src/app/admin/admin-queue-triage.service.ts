import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminQueueBulkTriageRequest,
  AdminQueueBulkTriageResponse,
  AdminQueueItemType,
  AdminQueueTriageResponse,
  AdminQueueTriageUpdateRequest
} from './admin-queue-triage.models';

@Injectable({ providedIn: 'root' })
export class AdminQueueTriageService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/moderation-queue`;

  getTriage(itemType: AdminQueueItemType | string, itemId: string): Promise<AdminQueueTriageResponse> {
    return firstValueFrom(this.http.get<AdminQueueTriageResponse>(`${this.itemUrl(itemType, itemId)}/triage`));
  }

  updateTriage(itemType: AdminQueueItemType | string, itemId: string, request: AdminQueueTriageUpdateRequest): Promise<AdminQueueTriageResponse> {
    return firstValueFrom(this.http.put<AdminQueueTriageResponse>(`${this.itemUrl(itemType, itemId)}/triage`, request));
  }

  claim(itemType: AdminQueueItemType | string, itemId: string): Promise<AdminQueueTriageResponse> {
    return firstValueFrom(this.http.post<AdminQueueTriageResponse>(`${this.itemUrl(itemType, itemId)}/claim`, {}));
  }

  unclaim(itemType: AdminQueueItemType | string, itemId: string): Promise<AdminQueueTriageResponse> {
    return firstValueFrom(this.http.post<AdminQueueTriageResponse>(`${this.itemUrl(itemType, itemId)}/unclaim`, {}));
  }

  bulkTriage(request: AdminQueueBulkTriageRequest): Promise<AdminQueueBulkTriageResponse> {
    return firstValueFrom(this.http.post<AdminQueueBulkTriageResponse>(`${this.baseUrl}/bulk-triage`, request));
  }

  private itemUrl(itemType: AdminQueueItemType | string, itemId: string): string {
    return `${this.baseUrl}/items/${encodeURIComponent(itemType)}/${encodeURIComponent(itemId)}`;
  }
}
