import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  CreateSellerSupportTicketRequest,
  SellerSupportMessageRequest,
  SellerSupportTicketResponse
} from './seller-support.models';

@Injectable({ providedIn: 'root' })
export class SellerSupportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/support-tickets`;

  listTickets(): Promise<SellerSupportTicketResponse[]> {
    return firstValueFrom(this.http.get<SellerSupportTicketResponse[]>(this.baseUrl));
  }

  getTicket(ticketId: string): Promise<SellerSupportTicketResponse> {
    return firstValueFrom(this.http.get<SellerSupportTicketResponse>(`${this.baseUrl}/${ticketId}`));
  }

  createTicket(request: CreateSellerSupportTicketRequest): Promise<SellerSupportTicketResponse> {
    return firstValueFrom(this.http.post<SellerSupportTicketResponse>(this.baseUrl, request));
  }

  addMessage(ticketId: string, request: SellerSupportMessageRequest): Promise<SellerSupportTicketResponse> {
    return firstValueFrom(this.http.post<SellerSupportTicketResponse>(`${this.baseUrl}/${ticketId}/messages`, request));
  }
}
