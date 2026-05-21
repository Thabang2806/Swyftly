export type SellerDeliveryMethodType = 'Standard' | 'Express' | 'LocalCourier';

export interface SellerDeliveryMethodRequest {
  name: string;
  description: string | null;
  methodType: SellerDeliveryMethodType;
  countryCode: string;
  province: string | null;
  basePrice: number;
  freeShippingThreshold: number | null;
  estimatedMinDays: number;
  estimatedMaxDays: number;
  displayOrder: number;
  isActive: boolean;
}

export interface SellerDeliveryMethodResponse extends SellerDeliveryMethodRequest {
  deliveryMethodId: string;
  sellerId: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}
