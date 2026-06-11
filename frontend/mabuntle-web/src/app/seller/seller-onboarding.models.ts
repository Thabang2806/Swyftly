export interface SellerOnboardingResponse {
  sellerId: string;
  verificationStatus: string;
  canSubmitForVerification: boolean;
  isProfileComplete: boolean;
  isStorefrontComplete: boolean;
  isAddressComplete: boolean;
  isPayoutPlaceholderComplete: boolean;
  profile: SellerProfileResponse;
  storefront: SellerStorefrontResponse | null;
  address: SellerAddressResponse | null;
  payout: SellerPayoutResponse | null;
  latestVerificationReview: SellerVerificationReviewResponse | null;
}

export interface SellerProfileResponse {
  displayName: string | null;
  contactEmail: string | null;
  phoneNumber: string | null;
  businessType: string | null;
  businessName: string | null;
}

export interface SellerStorefrontResponse {
  storeName: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  isPublished: boolean;
}

export interface SellerAddressResponse {
  addressLine1: string;
  addressLine2: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
}

export interface SellerPayoutResponse {
  payoutProviderReference: string;
  hasSubmittedPlaceholder: boolean;
  isAdminApproved: boolean;
}

export interface SellerVerificationReviewResponse {
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  rejectionReason: string | null;
  suspensionReason: string | null;
}

export interface UpdateSellerProfileRequest {
  displayName: string;
  contactEmail: string;
  phoneNumber: string;
  businessType: 'Individual' | 'RegisteredBusiness';
  businessName: string | null;
}

export interface UpdateSellerStorefrontRequest {
  storeName: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
}

export interface UpdateSellerAddressRequest {
  addressLine1: string;
  addressLine2: string | null;
  city: string;
  province: string;
  postalCode: string;
  countryCode: string;
}

export interface UpdateSellerPayoutRequest {
  payoutProviderReference: string;
}
