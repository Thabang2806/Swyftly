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
  selector: 'app-checkout-failed-page',
  imports: [CurrencyPipe, MatButtonModule, RouterLink, StatusBadgeComponent, UiAlertComponent],
  template: `
    <section class="page checkout-result-page">
      <div class="checkout-result-card hf-checkout-result-card hf-checkout-result-card--warning">
        <div class="checkout-result-heading">
          <app-status-badge [label]="order()?.status ?? 'Checkout issue'" [tone]="statusTone(order()?.status ?? 'Failed')" />
          <h1>Checkout needs attention</h1>
          <p>Payment may not have started or the provider may still be waiting for completion.</p>
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
                <strong>Order status</strong>
                <span>{{ order()!.status }}</span>
              </div>
              <div>
                <strong>Total</strong>
                <span>{{ order()!.totalAmount | currency:'ZAR':'symbol-narrow' }}</span>
              </div>
              <div>
                <strong>Next action</strong>
                <span>{{ order()!.status === 'Cancelled' ? 'Start checkout again from your cart.' : 'Retry payment or review your cart.' }}</span>
              </div>
            </div>

            @if (canRetryPayment()) {
              <div class="checkout-result-action-row">
                <button mat-flat-button type="button" [disabled]="isRetrying()" (click)="retryPayment()">
                  {{ isRetrying() ? 'Opening payment...' : 'Retry payment' }}
                </button>
              </div>
            } @else if (order()!.status === 'Cancelled') {
              <app-ui-alert tone="warning">This order was cancelled after payment failure. Start checkout again from your cart instead of reopening this order.</app-ui-alert>
            }
          }
        }

        <div class="auth-actions checkout-result-actions">
          <a mat-flat-button routerLink="/cart">Review cart</a>
          <a mat-stroked-button routerLink="/shop">Continue shopping</a>
        </div>
      </div>
    </section>
  `
})
export class CheckoutFailedPageComponent implements OnInit {
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
