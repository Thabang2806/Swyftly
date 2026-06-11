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
  selector: 'app-checkout-failed-page',
  imports: [CurrencyPipe, DatePipe, LuxuryPublicStylesComponent, RouterLink, StatusBadgeComponent, UiAlertComponent],
  template: `
    <app-luxury-public-styles />
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

          @if (paymentError()) {
            <app-ui-alert tone="warning">{{ paymentError() }}</app-ui-alert>
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
                @if (payment.failedAtUtc) {
                  <small>Failed {{ payment.failedAtUtc | date:'medium' }}</small>
                }
                @if (payment.cancelledAtUtc) {
                  <small>Cancelled {{ payment.cancelledAtUtc | date:'medium' }}</small>
                }
              </section>
            } @else {
              <app-ui-alert tone="info">Payment did not start for this order. Retry when you are ready.</app-ui-alert>
            }

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
                <span>{{ order()!.status === 'Cancelled' ? 'Start checkout again from your cart, or add the items again if the cart is empty.' : 'Retry payment or review your cart.' }}</span>
              </div>
            </div>

            @if (canRetryPayment()) {
              <div class="checkout-result-action-row">
                <button data-ui-button="primary" type="button" [disabled]="isRetrying()" (click)="retryPayment()">
                  {{ isRetrying() ? 'Opening payment...' : 'Retry payment' }}
                </button>
              </div>
            } @else if (order()!.status === 'Cancelled') {
              <app-ui-alert tone="warning">This order was cancelled after payment failure. Start checkout again from your cart, or add the items again if the cart is empty.</app-ui-alert>
            }

            <div class="checkout-result-action-row">
              <button data-ui-button="secondary" type="button" [disabled]="isRefreshing()" (click)="refreshOrder()">
                {{ isRefreshing() ? 'Refreshing...' : 'Refresh payment status' }}
              </button>
              @if (lastCheckedAt()) {
                <span>Last checked {{ lastCheckedAt() | date:'shortTime' }}</span>
              }
            </div>
          }
        }

        <div class="auth-actions checkout-result-actions">
          <a data-ui-button="primary" routerLink="/cart">Review cart</a>
          <a data-ui-button="secondary" routerLink="/shop">Continue shopping</a>
        </div>
      </div>
    </section>
  `
})
export class CheckoutFailedPageComponent implements OnInit, OnDestroy {
  private readonly orderService = inject(BuyerOrderService);
  private readonly paymentRedirectService = inject(BuyerPaymentRedirectService);
  private readonly paymentService = inject(BuyerPaymentService);
  private readonly route = inject(ActivatedRoute);
  private readonly ngZone = inject(NgZone);

  protected readonly orderId = this.route.snapshot.queryParamMap.get('orderId');
  protected readonly paymentError = signal<string | null>(sanitizePaymentError(this.route.snapshot.queryParamMap.get('paymentError')));
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

function sanitizePaymentError(message: string | null): string | null {
  const normalized = message?.replace(/\s+/g, ' ').trim() ?? '';
  if (!normalized) {
    return null;
  }

  return normalized.length > 180 ? `${normalized.slice(0, 177)}...` : normalized;
}
