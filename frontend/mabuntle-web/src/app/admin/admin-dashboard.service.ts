import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminDashboardSummaryResponse } from './admin-dashboard.models';

@Injectable({ providedIn: 'root' })
export class AdminDashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/dashboard`;

  getSummary(): Promise<AdminDashboardSummaryResponse> {
    return firstValueFrom(this.http.get<AdminDashboardSummaryResponse>(`${this.baseUrl}/summary`));
  }
}
