import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerDisputeEvidenceRequest,
  BuyerDisputeMessageRequest,
  BuyerDisputeResponse
} from './buyer-dispute.models';

@Injectable({ providedIn: 'root' })
export class BuyerDisputeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer/disputes`;

  listDisputes(): Promise<BuyerDisputeResponse[]> {
    return firstValueFrom(this.http.get<BuyerDisputeResponse[]>(this.baseUrl));
  }

  addMessage(disputeId: string, request: BuyerDisputeMessageRequest): Promise<BuyerDisputeResponse> {
    return firstValueFrom(this.http.post<BuyerDisputeResponse>(`${this.baseUrl}/${disputeId}/messages`, request));
  }

  addEvidence(disputeId: string, request: BuyerDisputeEvidenceRequest): Promise<BuyerDisputeResponse> {
    return firstValueFrom(this.http.post<BuyerDisputeResponse>(`${this.baseUrl}/${disputeId}/evidence`, request));
  }
}
