import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SellerPayoutProfileChangeRequestRequest,
  SellerPayoutProfileChangeStateResponse
} from './seller-payout-profile-change.models';

@Injectable({ providedIn: 'root' })
export class SellerPayoutProfileChangeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/payout-profile/change-request`;

  getState(): Promise<SellerPayoutProfileChangeStateResponse> {
    return firstValueFrom(this.http.get<SellerPayoutProfileChangeStateResponse>(this.baseUrl));
  }

  upsertDraft(request: SellerPayoutProfileChangeRequestRequest): Promise<SellerPayoutProfileChangeStateResponse> {
    return firstValueFrom(this.http.put<SellerPayoutProfileChangeStateResponse>(this.baseUrl, request));
  }

  submitForReview(): Promise<SellerPayoutProfileChangeStateResponse> {
    return firstValueFrom(this.http.post<SellerPayoutProfileChangeStateResponse>(`${this.baseUrl}/submit-review`, {}));
  }

  cancel(): Promise<SellerPayoutProfileChangeStateResponse> {
    return firstValueFrom(this.http.post<SellerPayoutProfileChangeStateResponse>(`${this.baseUrl}/cancel`, {}));
  }
}
