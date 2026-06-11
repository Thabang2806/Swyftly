import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SellerNotificationPreferencesRequest,
  SellerNotificationPreferencesResponse,
  SellerNotificationResponse,
  SellerNotificationsReadAllResponse,
  SellerNotificationUnreadCountResponse
} from './seller-notification.models';

@Injectable({ providedIn: 'root' })
export class SellerNotificationService {
  private readonly http = inject(HttpClient, { optional: true });
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/notifications`;
  readonly unreadCount = signal(0);

  listNotifications(): Promise<SellerNotificationResponse[]> {
    return firstValueFrom(this.httpClient.get<SellerNotificationResponse[]>(this.baseUrl));
  }

  getUnreadCount(): Promise<SellerNotificationUnreadCountResponse> {
    if (!this.http) {
      return Promise.resolve({ unreadCount: 0 });
    }

    return firstValueFrom(this.http.get<SellerNotificationUnreadCountResponse>(`${this.baseUrl}/unread-count`));
  }

  async refreshUnreadCount(): Promise<void> {
    const result = await this.getUnreadCount();
    this.unreadCount.set(result.unreadCount);
  }

  markRead(notificationId: string): Promise<SellerNotificationResponse> {
    return firstValueFrom(this.httpClient.post<SellerNotificationResponse>(`${this.baseUrl}/${notificationId}/read`, null));
  }

  markAllRead(): Promise<SellerNotificationsReadAllResponse> {
    return firstValueFrom(this.httpClient.post<SellerNotificationsReadAllResponse>(`${this.baseUrl}/read-all`, null));
  }

  getPreferences(): Promise<SellerNotificationPreferencesResponse> {
    return firstValueFrom(this.httpClient.get<SellerNotificationPreferencesResponse>(
      `${environment.apiBaseUrl}/api/seller/notification-preferences`));
  }

  updatePreferences(request: SellerNotificationPreferencesRequest): Promise<SellerNotificationPreferencesResponse> {
    return firstValueFrom(this.httpClient.put<SellerNotificationPreferencesResponse>(
      `${environment.apiBaseUrl}/api/seller/notification-preferences`,
      request));
  }

  private get httpClient(): HttpClient {
    if (!this.http) {
      throw new Error('SellerNotificationService requires HttpClient for notification mutations.');
    }

    return this.http;
  }
}
