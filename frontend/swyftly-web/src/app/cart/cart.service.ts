import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AddCartItemRequest,
  CartResponse,
  CartShippingOptionsRequest,
  CartShippingOptionsResponse,
  CreateOrderFromCartRequest,
  OrderResult,
  UpdateCartItemRequest
} from './cart.models';

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getCart(): Promise<CartResponse> {
    return firstValueFrom(this.http.get<CartResponse>(`${this.baseUrl}/api/cart`));
  }

  addItem(request: AddCartItemRequest): Promise<CartResponse> {
    return firstValueFrom(this.http.post<CartResponse>(`${this.baseUrl}/api/cart/items`, request));
  }

  updateItem(cartItemId: string, request: UpdateCartItemRequest): Promise<CartResponse> {
    return firstValueFrom(this.http.put<CartResponse>(`${this.baseUrl}/api/cart/items/${cartItemId}`, request));
  }

  removeItem(cartItemId: string): Promise<CartResponse> {
    return firstValueFrom(this.http.delete<CartResponse>(`${this.baseUrl}/api/cart/items/${cartItemId}`));
  }

  moveItemToWishlist(cartItemId: string): Promise<CartResponse> {
    return firstValueFrom(this.http.post<CartResponse>(`${this.baseUrl}/api/cart/items/${cartItemId}/move-to-wishlist`, null));
  }

  getShippingOptions(request: CartShippingOptionsRequest): Promise<CartShippingOptionsResponse> {
    return firstValueFrom(this.http.post<CartShippingOptionsResponse>(`${this.baseUrl}/api/cart/shipping-options`, request));
  }

  clearCart(): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/api/cart`));
  }

  createOrderFromCart(request: CreateOrderFromCartRequest): Promise<OrderResult> {
    return firstValueFrom(this.http.post<OrderResult>(`${this.baseUrl}/api/orders/from-cart`, request));
  }
}
