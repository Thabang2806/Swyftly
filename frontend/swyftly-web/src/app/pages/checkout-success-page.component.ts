import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-checkout-success-page',
  imports: [CurrencyPipe, MatButtonModule, RouterLink, StatusBadgeComponent, UiAlertComponent],
  template: `
    <section class="page checkout-result-page">
      <div class="checkout-result-card hf-checkout-result-card">
        <div class="checkout-result-heading">
          <app-status-badge [label]="order()?.status ?? 'Order created'" [tone]="statusTone(order()?.status ?? 'PendingPayment')" />
          <h1>Checkout started</h1>
          <p>Your order has been created. Payment is confirmed only after Swyftly receives a signed provider webhook.</p>
        </div>

        @if (isLoading()) {
          <div class="route-card">Loading order...</div>
        } @else {
          @if (errorMessage()) {
            <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
          }

          @if (successMessage()) {
            <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
          }

          @if (order()) {
            <div class="checkout-result-grid">
              <div class="checkout-reference">
                <span>Order reference</span>
                <strong>{{ order()!.orderId }}</strong>
              </div>
              <div class="checkout-reference">
                <span>Total</span>
                <strong>{{ order()!.totalAmount | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
            </div>

            <div class="checkout-result-steps">
              <div>
                <strong>1. Order reserved</strong>
                <span>{{ order()!.items.length }} item{{ order()!.items.length === 1 ? '' : 's' }} reserved for payment.</span>
              </div>
              <div>
                <strong>2. Payment {{ order()!.status === 'Paid' ? 'confirmed' : 'pending' }}</strong>
                <span>Total {{ order()!.totalAmount | currency:'ZAR':'symbol-narrow' }}.</span>
              </div>
              <div>
                <strong>3. Track from account</strong>
                <span>Order status updates will appear in your account.</span>
              </div>
            </div>

            @if (canRetryPayment()) {
              <app-ui-alert tone="info">Payment is still pending. You can retry the provider checkout for this order.</app-ui-alert>
              <div class="checkout-result-action-row">
                <button mat-flat-button type="button" [disabled]="isRetrying()" (click)="retryPayment()">
                  {{ isRetrying() ? 'Opening payment...' : 'Retry payment' }}
                </button>
              </div>
            }
          } @else if (orderId) {
            <div class="checkout-reference">
              <span>Order reference</span>
              <strong>{{ orderId }}</strong>
            </div>
          }
        }

        <div class="auth-actions checkout-result-actions">
          <a mat-flat-button [routerLink]="order() ? ['/account/orders', order()!.orderId] : ['/account/orders']">View order</a>
          <a mat-stroked-button routerLink="/shop">Continue shopping</a>
        </div>
      </div>
    </section>
  `
})
export class CheckoutSuccessPageComponent implements OnInit {
  private readonly orderService = inject(BuyerOrderService);
  private readonly paymentRedirectService = inject(BuyerPaymentRedirectService);
  private readonly paymentService = inject(BuyerPaymentService);
  private readonly route = inject(ActivatedRoute);

  protected readonly orderId = this.route.snapshot.queryParamMap.get('orderId');
  protected readonly order = signal<BuyerOrderResult | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isRetrying = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    if (!this.orderId) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    try {
      this.order.set(await this.orderService.getOrder(this.orderId));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  protected canRetryPayment(): boolean {
    return this.order()?.status === 'PendingPayment';
  }

  protected async retryPayment(): Promise<void> {
    const order = this.order();
    if (!order || !this.canRetryPayment() || this.isRetrying()) {
      return;
    }

    this.isRetrying.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      const payment = await this.paymentService.initiatePayment(order.orderId);
      if (payment.checkoutUrl) {
        this.paymentRedirectService.redirect(payment.checkoutUrl);
      } else {
        this.successMessage.set('Payment is pending. Refresh this page after completing provider checkout.');
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isRetrying.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Processing', 'Shipped', 'Delivered', 'Completed'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Disputed', 'Failed'].includes(status)) {
      return 'danger';
    }

    return 'accent';
  }
}
