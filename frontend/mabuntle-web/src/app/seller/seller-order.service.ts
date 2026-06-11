import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AddSellerOrderTrackingRequest,
  BookSellerCarrierRequest,
  SellerFulfillmentExceptionRequest,
  SellerOrderResult
} from './seller-order.models';

@Injectable({ providedIn: 'root' })
export class SellerOrderService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/orders`;

  listOrders(): Promise<SellerOrderResult[]> {
    return firstValueFrom(this.http.get<SellerOrderResult[]>(this.baseUrl));
  }

  getOrder(orderId: string): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.get<SellerOrderResult>(`${this.baseUrl}/${orderId}`));
  }

  markProcessing(orderId: string): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/mark-processing`, {}));
  }

  addTracking(orderId: string, request: AddSellerOrderTrackingRequest): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/tracking`, request));
  }

  markReadyToShip(orderId: string): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/mark-ready-to-ship`, {}));
  }

  markShipped(orderId: string): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/mark-shipped`, {}));
  }

  markDelivered(orderId: string): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/mark-delivered`, {}));
  }

  markDeliveryFailed(orderId: string, request: SellerFulfillmentExceptionRequest): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/mark-delivery-failed`, request));
  }

  markReturnedToSender(orderId: string, request: SellerFulfillmentExceptionRequest): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/mark-returned-to-sender`, request));
  }

  bookCarrier(orderId: string, request: BookSellerCarrierRequest): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/book-carrier`, request));
  }

  syncCarrierTracking(orderId: string): Promise<SellerOrderResult> {
    return firstValueFrom(this.http.post<SellerOrderResult>(`${this.baseUrl}/${orderId}/sync-carrier-tracking`, {}));
  }
}
