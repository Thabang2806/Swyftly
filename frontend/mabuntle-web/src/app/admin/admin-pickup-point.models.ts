export interface AdminPickupPointRequest {
  providerName: string;
  code: string;
  name: string;
  addressLine1: string;
  addressLine2: string | null;
  suburb: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
  latitude: number | null;
  longitude: number | null;
  openingHours: string | null;
  isActive: boolean;
}

export interface AdminPickupPointResponse extends AdminPickupPointRequest {
  pickupPointId: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}
