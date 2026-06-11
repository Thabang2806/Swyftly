import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminAuditLogSearchRequest,
  AdminAuditLogSearchResponse
} from './admin-audit-log.models';

@Injectable({ providedIn: 'root' })
export class AdminAuditLogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/audit-logs`;

  search(request: AdminAuditLogSearchRequest = {}): Promise<AdminAuditLogSearchResponse> {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(request)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return firstValueFrom(this.http.get<AdminAuditLogSearchResponse>(this.baseUrl, { params }));
  }
}
