import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SellerDeliveryMethodRequest,
  SellerDeliveryMethodResponse
} from './seller-delivery-method.models';

@Injectable({ providedIn: 'root' })
export class SellerDeliveryMethodService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/delivery-methods`;

  list(): Promise<SellerDeliveryMethodResponse[]> {
    return firstValueFrom(this.http.get<SellerDeliveryMethodResponse[]>(this.baseUrl));
  }

  create(request: SellerDeliveryMethodRequest): Promise<SellerDeliveryMethodResponse> {
    return firstValueFrom(this.http.post<SellerDeliveryMethodResponse>(this.baseUrl, request));
  }

  update(deliveryMethodId: string, request: SellerDeliveryMethodRequest): Promise<SellerDeliveryMethodResponse> {
    return firstValueFrom(this.http.put<SellerDeliveryMethodResponse>(`${this.baseUrl}/${deliveryMethodId}`, request));
  }

  activate(deliveryMethodId: string): Promise<SellerDeliveryMethodResponse> {
    return firstValueFrom(this.http.post<SellerDeliveryMethodResponse>(`${this.baseUrl}/${deliveryMethodId}/activate`, null));
  }

  deactivate(deliveryMethodId: string): Promise<SellerDeliveryMethodResponse> {
    return firstValueFrom(this.http.post<SellerDeliveryMethodResponse>(`${this.baseUrl}/${deliveryMethodId}/deactivate`, null));
  }
}
