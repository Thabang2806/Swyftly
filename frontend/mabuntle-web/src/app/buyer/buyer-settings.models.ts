export interface BuyerProfileSettingsRequest {
  displayName: string | null;
  phoneNumber: string | null;
}

export interface BuyerProfileSettingsResponse {
  buyerId: string;
  userId: string;
  email: string;
  displayName: string | null;
  phoneNumber: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export type BuyerNotificationPreferenceCategory = 'Orders' | 'Returns' | 'Reviews' | 'Support';

export interface BuyerNotificationPreferenceRequest {
  category: BuyerNotificationPreferenceCategory;
  isEnabled: boolean;
  emailEnabled: boolean;
}

export interface BuyerNotificationPreferencesRequest {
  preferences: BuyerNotificationPreferenceRequest[];
}

export interface BuyerNotificationPreferenceResponse {
  category: BuyerNotificationPreferenceCategory;
  isEnabled: boolean;
  emailEnabled: boolean;
}

export interface BuyerNotificationPreferencesResponse {
  preferences: BuyerNotificationPreferenceResponse[];
}

export interface BuyerDeliveryAddressRequest {
  label: string;
  recipientName: string;
  phoneNumber: string;
  addressLine1: string;
  addressLine2: string | null;
  suburb: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
  deliveryInstructions: string | null;
  isDefault: boolean;
}

export interface BuyerDeliveryAddressResponse extends BuyerDeliveryAddressRequest {
  deliveryAddressId: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  verificationStatus?: string | null;
  verificationProvider?: string | null;
  verificationWarnings?: string[] | null;
  verifiedAtUtc?: string | null;
}

export interface BuyerDeliveryAddressVerificationRequest {
  recipientName: string;
  phoneNumber: string;
  addressLine1: string;
  addressLine2: string | null;
  suburb: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
  deliveryInstructions: string | null;
}

export interface BuyerDeliveryAddressVerificationResponse extends BuyerDeliveryAddressVerificationRequest {
  verificationStatus: string;
  verificationProvider: string;
  verificationWarnings: string[];
  verifiedAtUtc: string;
}
