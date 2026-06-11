import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SellerOnboardingResponse,
  UpdateSellerAddressRequest,
  UpdateSellerPayoutRequest,
  UpdateSellerProfileRequest,
  UpdateSellerStorefrontRequest
} from './seller-onboarding.models';

@Injectable({ providedIn: 'root' })
export class SellerOnboardingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/onboarding`;

  getOnboarding(): Promise<SellerOnboardingResponse> {
    return firstValueFrom(this.http.get<SellerOnboardingResponse>(this.baseUrl));
  }

  updateProfile(request: UpdateSellerProfileRequest): Promise<SellerOnboardingResponse> {
    return firstValueFrom(this.http.put<SellerOnboardingResponse>(`${this.baseUrl}/profile`, request));
  }

  updateStorefront(request: UpdateSellerStorefrontRequest): Promise<SellerOnboardingResponse> {
    return firstValueFrom(this.http.put<SellerOnboardingResponse>(`${this.baseUrl}/storefront`, request));
  }

  updateAddress(request: UpdateSellerAddressRequest): Promise<SellerOnboardingResponse> {
    return firstValueFrom(this.http.put<SellerOnboardingResponse>(`${this.baseUrl}/address`, request));
  }

  updatePayout(request: UpdateSellerPayoutRequest): Promise<SellerOnboardingResponse> {
    return firstValueFrom(this.http.put<SellerOnboardingResponse>(`${this.baseUrl}/payout`, request));
  }

  submitVerification(): Promise<SellerOnboardingResponse> {
    return firstValueFrom(this.http.post<SellerOnboardingResponse>(`${this.baseUrl}/submit-verification`, {}));
  }
}
