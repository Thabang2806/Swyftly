import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminPaymentDetailResponse } from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-payment-detail-page',
  imports: [
    AdminWorkspaceNavComponent,
    CurrencyPipe,
    DatePipe,
    PageHeaderComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-finance-page">
      <app-admin-workspace-nav />
      <a class="admin-back-link" routerLink="/payments">Back to payments</a>

      @if (isLoading()) {
        <div class="route-card">Loading payment...</div>
      } @else if (errorMessage()) {
        <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
      } @else if (payment()) {
        <app-page-header
          eyebrow="Admin payment"
          [heading]="payment()!.provider + ' payment'"
          [description]="payment()!.providerReference ?? payment()!.paymentId"
        >
          <div pageHeaderActions>
            <app-status-badge [label]="payment()!.status" [tone]="statusTone(payment()!.status)" />
            <a data-ui-button="secondary" [routerLink]="['/orders', payment()!.orderId]">Open order</a>
          </div>
        </app-page-header>

        <div class="admin-finance-policy">
          <app-status-badge label="Read only" tone="accent" />
          <span>Payment mutation remains with buyer payment initiation, provider webhooks, refund workflows, and finance dual-control actions.</span>
        </div>

        <div class="buyer-detail-grid">
          <section class="buyer-panel">
            <h2>Payment</h2>
            <dl class="seller-facts">
              <div><dt>Amount</dt><dd>{{ payment()!.amount | currency:payment()!.currency:'symbol-narrow' }}</dd></div>
              <div><dt>Status</dt><dd>{{ payment()!.status }}</dd></div>
              <div><dt>Provider</dt><dd>{{ payment()!.provider }}</dd></div>
              <div><dt>Reference</dt><dd>{{ payment()!.providerReference ?? 'No provider reference' }}</dd></div>
              <div><dt>Created</dt><dd>{{ payment()!.createdAtUtc | date:'medium' }}</dd></div>
            </dl>
          </section>

          <section class="buyer-panel">
            <h2>Related order</h2>
            @if (payment()!.order) {
              <dl class="seller-facts">
                <div><dt>Order</dt><dd>{{ payment()!.order!.orderId }}</dd></div>
                <div><dt>Buyer</dt><dd>{{ payment()!.order!.buyerId }}</dd></div>
                <div><dt>Seller</dt><dd>{{ payment()!.order!.sellerId }}</dd></div>
                <div><dt>Status</dt><dd>{{ payment()!.order!.status }}</dd></div>
                <div><dt>Total</dt><dd>{{ payment()!.order!.totalAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
              </dl>
            } @else {
              <p>Related order was not found.</p>
            }
          </section>
        </div>

        <section class="buyer-panel">
          <h2>Webhook events</h2>
          <div class="admin-table" role="table" aria-label="Payment events">
            @for (event of payment()!.events; track event.paymentEventId) {
              <div class="admin-table-row admin-finance-table-row" role="row">
                <span>
                  <strong>{{ event.eventType }}</strong>
                  <small>{{ event.providerEventId }}</small>
                </span>
                <span><app-status-badge [label]="event.processingStatus" [tone]="statusTone(event.processingStatus)" /></span>
                <span>{{ event.receivedAtUtc | date:'medium' }}</span>
                <span>{{ event.errorMessage ?? 'No error' }}</span>
              </div>
            } @empty {
              <p>No webhook events have been recorded for this payment.</p>
            }
          </div>
        </section>
      }
    </section>
  `
})
export class AdminPaymentDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly adminOrderPaymentService = inject(AdminOrderPaymentService);

  protected readonly payment = signal<AdminPaymentDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    try {
      this.payment.set(await this.adminOrderPaymentService.getPayment(this.route.snapshot.paramMap.get('paymentId') ?? ''));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Refunded', 'PartiallyRefunded', 'Processed'].includes(status)) {
      return 'success';
    }

    if (['Failed', 'Cancelled', 'Disputed'].includes(status)) {
      return 'danger';
    }

    if (['Pending', 'Authorized', 'Received'].includes(status)) {
      return 'warning';
    }

    return 'neutral';
  }
}
