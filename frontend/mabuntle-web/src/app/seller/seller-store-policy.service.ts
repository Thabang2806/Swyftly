import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { SellerPolicyResponse, SellerStorePolicyRequest } from '../shared/seller-policy.models';

@Injectable({ providedIn: 'root' })
export class SellerStorePolicyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getPolicy(): Promise<SellerPolicyResponse> {
    return firstValueFrom(this.http.get<SellerPolicyResponse>(`${this.baseUrl}/api/seller/store-policy`));
  }

  updatePolicy(request: SellerStorePolicyRequest): Promise<SellerPolicyResponse> {
    return firstValueFrom(this.http.put<SellerPolicyResponse>(`${this.baseUrl}/api/seller/store-policy`, request));
  }
}
