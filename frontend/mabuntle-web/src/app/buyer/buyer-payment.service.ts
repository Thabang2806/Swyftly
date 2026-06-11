import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { PaymentInitiationResponse } from './buyer-payment.models';

@Injectable({ providedIn: 'root' })
export class BuyerPaymentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/payments`;

  initiatePayment(orderId: string): Promise<PaymentInitiationResponse> {
    return firstValueFrom(this.http.post<PaymentInitiationResponse>(`${this.baseUrl}/initiate`, { orderId }));
  }
}

@Injectable({ providedIn: 'root' })
export class BuyerPaymentRedirectService {
  private readonly document = inject(DOCUMENT);

  redirect(checkoutUrl: string): void {
    this.document.defaultView?.location.assign(checkoutUrl);
  }
}

