import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, NgZone, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-checkout-success-page',
  imports: [CurrencyPipe, DatePipe, LuxuryPublicStylesComponent, RouterLink, StatusBadgeComponent, UiAlertComponent],
  template: `
    <app-luxury-public-styles />
    <section class="page checkout-result-page">
      <div class="checkout-result-card hf-checkout-result-card">
        <div class="checkout-result-heading">
          <app-status-badge [label]="order()?.status ?? 'Order created'" [tone]="statusTone(order()?.status ?? 'PendingPayment')" />
          <h1>Checkout started</h1>
          <p>Your order has been created. Payment is confirmed only after Mabuntle receives a signed provider webhook.</p>
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

            @if (order()!.paymentSummary; as payment) {
              <section class="checkout-reference checkout-payment-summary">
                <span>Payment status</span>
                <strong>{{ payment.status }}</strong>
                <small>{{ payment.providerName }}{{ payment.providerReference ? ' - ' + payment.providerReference : '' }}</small>
                <small>{{ payment.amount | currency:payment.currency:'symbol-narrow' }} updated {{ payment.updatedAtUtc | date:'short' }}</small>
                @if (payment.paidAtUtc) {
                  <small>Paid {{ payment.paidAtUtc | date:'medium' }}</small>
                }
              </section>
            } @else {
              <app-ui-alert tone="info">Payment has not started yet for this order.</app-ui-alert>
            }

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
                <button data-ui-button="primary" type="button" [disabled]="isRetrying()" (click)="retryPayment()">
                  {{ isRetrying() ? 'Opening payment...' : 'Retry payment' }}
                </button>
              </div>
            }

            <div class="checkout-result-action-row">
              <button data-ui-button="secondary" type="button" [disabled]="isRefreshing()" (click)="refreshOrder()">
                {{ isRefreshing() ? 'Refreshing...' : 'Refresh payment status' }}
              </button>
              @if (lastCheckedAt()) {
                <span>Last checked {{ lastCheckedAt() | date:'shortTime' }}</span>
              }
            </div>
          } @else if (orderId) {
            <div class="checkout-reference">
              <span>Order reference</span>
              <strong>{{ orderId }}</strong>
            </div>
          }
        }

        <div class="auth-actions checkout-result-actions">
          <a data-ui-button="primary" [routerLink]="order() ? ['/account/orders', order()!.orderId] : ['/account/orders']">View order</a>
          <a data-ui-button="secondary" routerLink="/shop">Continue shopping</a>
        </div>
      </div>
    </section>
  `
})
export class CheckoutSuccessPageComponent implements OnInit, OnDestroy {
  private readonly orderService = inject(BuyerOrderService);
  private readonly paymentRedirectService = inject(BuyerPaymentRedirectService);
  private readonly paymentService = inject(BuyerPaymentService);
  private readonly route = inject(ActivatedRoute);
  private readonly ngZone = inject(NgZone);

  protected readonly orderId = this.route.snapshot.queryParamMap.get('orderId');
  protected readonly order = signal<BuyerOrderResult | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isRefreshing = signal(false);
  protected readonly isRetrying = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly lastCheckedAt = signal<Date | null>(null);
  private pollAttemptCount = 0;
  private pollTimer: ReturnType<typeof setTimeout> | null = null;

  async ngOnInit(): Promise<void> {
    if (!this.orderId) {
      return;
    }

    await this.loadOrder(true);
  }

  ngOnDestroy(): void {
    this.clearPollTimer();
  }

  protected async refreshOrder(): Promise<void> {
    await this.loadOrder(false);
  }

  private async loadOrder(showInitialLoading: boolean): Promise<void> {
    if (!this.orderId) {
      return;
    }

    if (showInitialLoading) {
      this.isLoading.set(true);
    } else {
      this.isRefreshing.set(true);
    }

    this.errorMessage.set(null);
    try {
      this.order.set(await this.orderService.getOrder(this.orderId));
      this.lastCheckedAt.set(new Date());
      this.schedulePendingPaymentPoll();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
      this.isRefreshing.set(false);
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

  private schedulePendingPaymentPoll(): void {
    this.clearPollTimer();
    if (this.order()?.status !== 'PendingPayment' || this.pollAttemptCount >= 5) {
      return;
    }

    this.pollAttemptCount += 1;
    this.ngZone.runOutsideAngular(() => {
      this.pollTimer = setTimeout(() => {
        this.ngZone.run(() => {
          void this.loadOrder(false);
        });
      }, 3000);
    });
  }

  private clearPollTimer(): void {
    if (this.pollTimer) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }
}
