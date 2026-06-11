import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerSupportMessageRequest,
  BuyerSupportTicketResponse,
  CreateBuyerSupportTicketRequest
} from './buyer-support.models';

@Injectable({ providedIn: 'root' })
export class BuyerSupportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer/support-tickets`;

  listTickets(): Promise<BuyerSupportTicketResponse[]> {
    return firstValueFrom(this.http.get<BuyerSupportTicketResponse[]>(this.baseUrl));
  }

  getTicket(ticketId: string): Promise<BuyerSupportTicketResponse> {
    return firstValueFrom(this.http.get<BuyerSupportTicketResponse>(`${this.baseUrl}/${ticketId}`));
  }

  createTicket(request: CreateBuyerSupportTicketRequest): Promise<BuyerSupportTicketResponse> {
    return firstValueFrom(this.http.post<BuyerSupportTicketResponse>(this.baseUrl, request));
  }

  addMessage(ticketId: string, request: BuyerSupportMessageRequest): Promise<BuyerSupportTicketResponse> {
    return firstValueFrom(this.http.post<BuyerSupportTicketResponse>(`${this.baseUrl}/${ticketId}/messages`, request));
  }
}
