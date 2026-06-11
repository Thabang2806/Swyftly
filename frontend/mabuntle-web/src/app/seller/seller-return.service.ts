import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SellerReturnRequestResult,
  SellerReturnResponseRequest,
  SellerReturnRestockDecisionRequest,
  SellerReturnRestockDecisionResponse
} from './seller-return.models';

@Injectable({ providedIn: 'root' })
export class SellerReturnService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/returns`;

  listReturns(): Promise<SellerReturnRequestResult[]> {
    return firstValueFrom(this.http.get<SellerReturnRequestResult[]>(this.baseUrl));
  }

  getReturn(returnRequestId: string): Promise<SellerReturnRequestResult> {
    return firstValueFrom(this.http.get<SellerReturnRequestResult>(`${this.baseUrl}/${returnRequestId}`));
  }

  approveReturn(returnRequestId: string, request: SellerReturnResponseRequest): Promise<SellerReturnRequestResult> {
    return firstValueFrom(this.http.post<SellerReturnRequestResult>(`${this.baseUrl}/${returnRequestId}/approve`, request));
  }

  rejectReturn(returnRequestId: string, request: SellerReturnResponseRequest): Promise<SellerReturnRequestResult> {
    return firstValueFrom(this.http.post<SellerReturnRequestResult>(`${this.baseUrl}/${returnRequestId}/reject`, request));
  }

  listRestockDecisions(returnRequestId: string): Promise<SellerReturnRestockDecisionResponse[]> {
    return firstValueFrom(this.http.get<SellerReturnRestockDecisionResponse[]>(`${this.baseUrl}/${returnRequestId}/restock-decisions`));
  }

  createRestockDecisions(
    returnRequestId: string,
    request: SellerReturnRestockDecisionRequest
  ): Promise<SellerReturnRestockDecisionResponse[]> {
    return firstValueFrom(
      this.http.post<SellerReturnRestockDecisionResponse[]>(`${this.baseUrl}/${returnRequestId}/restock-decisions`, request)
    );
  }
}
