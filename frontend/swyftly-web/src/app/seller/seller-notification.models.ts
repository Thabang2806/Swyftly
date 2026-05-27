export interface SellerNotificationResponse {
  notificationId: string;
  recipientUserId: string;
  type: string;
  title: string;
  message: string;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
  readAtUtc: string | null;
  createdAtUtc: string;
}

export interface SellerNotificationUnreadCountResponse {
  unreadCount: number;
}

export interface SellerNotificationsReadAllResponse {
  updatedCount: number;
}

export type SellerNotificationPreferenceCategory = 'Verification' | 'Products' | 'Revisions' | 'Ads' | 'Reports';

export interface SellerNotificationPreferenceRequest {
  category: SellerNotificationPreferenceCategory;
  isEnabled: boolean;
  emailEnabled: boolean;
}

export interface SellerNotificationPreferencesRequest {
  preferences: SellerNotificationPreferenceRequest[];
}

export interface SellerNotificationPreferenceResponse {
  category: SellerNotificationPreferenceCategory;
  isEnabled: boolean;
  emailEnabled: boolean;
}

export interface SellerNotificationPreferencesResponse {
  preferences: SellerNotificationPreferenceResponse[];
}

export interface SellerNotificationReadRealtimeEvent {
  notificationId: string;
  readAtUtc: string;
}

export interface SellerNotificationsReadAllRealtimeEvent {
  readAtUtc: string;
  updatedCount: number;
}
