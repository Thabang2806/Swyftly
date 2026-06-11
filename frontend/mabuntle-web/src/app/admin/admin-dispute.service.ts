import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminDisputeResponse, ResolveAdminDisputeRequest } from './admin-dispute.models';

@Injectable({ providedIn: 'root' })
export class AdminDisputeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/disputes`;

  getDisputes(): Promise<AdminDisputeResponse[]> {
    return firstValueFrom(this.http.get<AdminDisputeResponse[]>(this.baseUrl));
  }

  resolveDispute(disputeId: string, request: ResolveAdminDisputeRequest): Promise<AdminDisputeResponse> {
    return firstValueFrom(this.http.post<AdminDisputeResponse>(`${this.baseUrl}/${disputeId}/resolve`, request));
  }
}
