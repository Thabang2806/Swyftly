import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminPickupPointRequest, AdminPickupPointResponse } from './admin-pickup-point.models';

@Injectable({ providedIn: 'root' })
export class AdminPickupPointService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/pickup-points`;

  list(): Promise<AdminPickupPointResponse[]> {
    return firstValueFrom(this.http.get<AdminPickupPointResponse[]>(this.baseUrl));
  }

  create(request: AdminPickupPointRequest): Promise<AdminPickupPointResponse> {
    return firstValueFrom(this.http.post<AdminPickupPointResponse>(this.baseUrl, request));
  }

  update(pickupPointId: string, request: AdminPickupPointRequest): Promise<AdminPickupPointResponse> {
    return firstValueFrom(this.http.put<AdminPickupPointResponse>(`${this.baseUrl}/${pickupPointId}`, request));
  }

  activate(pickupPointId: string): Promise<AdminPickupPointResponse> {
    return firstValueFrom(this.http.post<AdminPickupPointResponse>(`${this.baseUrl}/${pickupPointId}/activate`, null));
  }

  deactivate(pickupPointId: string): Promise<AdminPickupPointResponse> {
    return firstValueFrom(this.http.post<AdminPickupPointResponse>(`${this.baseUrl}/${pickupPointId}/deactivate`, null));
  }
}
