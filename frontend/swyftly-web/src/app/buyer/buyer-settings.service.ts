import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerDeliveryAddressRequest,
  BuyerDeliveryAddressResponse,
  BuyerNotificationPreferencesRequest,
  BuyerNotificationPreferencesResponse,
  BuyerProfileSettingsRequest,
  BuyerProfileSettingsResponse
} from './buyer-settings.models';

@Injectable({ providedIn: 'root' })
export class BuyerSettingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer`;

  getProfile(): Promise<BuyerProfileSettingsResponse> {
    return firstValueFrom(this.http.get<BuyerProfileSettingsResponse>(`${this.baseUrl}/profile`));
  }

  updateProfile(request: BuyerProfileSettingsRequest): Promise<BuyerProfileSettingsResponse> {
    return firstValueFrom(this.http.put<BuyerProfileSettingsResponse>(`${this.baseUrl}/profile`, request));
  }

  getNotificationPreferences(): Promise<BuyerNotificationPreferencesResponse> {
    return firstValueFrom(this.http.get<BuyerNotificationPreferencesResponse>(`${this.baseUrl}/notification-preferences`));
  }

  updateNotificationPreferences(
    request: BuyerNotificationPreferencesRequest
  ): Promise<BuyerNotificationPreferencesResponse> {
    return firstValueFrom(this.http.put<BuyerNotificationPreferencesResponse>(`${this.baseUrl}/notification-preferences`, request));
  }

  listDeliveryAddresses(): Promise<BuyerDeliveryAddressResponse[]> {
    return firstValueFrom(this.http.get<BuyerDeliveryAddressResponse[]>(`${this.baseUrl}/delivery-addresses`));
  }

  createDeliveryAddress(request: BuyerDeliveryAddressRequest): Promise<BuyerDeliveryAddressResponse> {
    return firstValueFrom(this.http.post<BuyerDeliveryAddressResponse>(`${this.baseUrl}/delivery-addresses`, request));
  }

  updateDeliveryAddress(
    deliveryAddressId: string,
    request: BuyerDeliveryAddressRequest
  ): Promise<BuyerDeliveryAddressResponse> {
    return firstValueFrom(this.http.put<BuyerDeliveryAddressResponse>(
      `${this.baseUrl}/delivery-addresses/${deliveryAddressId}`,
      request));
  }

  deleteDeliveryAddress(deliveryAddressId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/delivery-addresses/${deliveryAddressId}`));
  }

  makeDefaultDeliveryAddress(deliveryAddressId: string): Promise<BuyerDeliveryAddressResponse[]> {
    return firstValueFrom(this.http.post<BuyerDeliveryAddressResponse[]>(
      `${this.baseUrl}/delivery-addresses/${deliveryAddressId}/make-default`,
      null));
  }
}
