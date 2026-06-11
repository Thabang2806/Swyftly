import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerRefundResult } from '../buyer/buyer-refund.models';
import { BuyerRefundService } from '../buyer/buyer-refund.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-refunds-page',
  imports: [
    BuyerWorkspaceNavComponent,
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Refunds"
        description="Track refund requests and finance processing outcomes without changing refund decisions."
      />

      @if (isLoading()) {
        <div class="route-card">Loading refunds...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (refunds().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Refunds"
            heading="No refund activity"
            message="Refund requests will appear here after a return, dispute, or finance review creates one."
          >
            <a data-ui-button="primary" routerLink="/account/orders">View orders</a>
          </app-empty-state>
        } @else {
          <div class="admin-table buyer-ops-table" role="table" aria-label="Buyer refunds">
            <div class="admin-table-row heading buyer-ops-table-row" role="row">
              <span role="columnheader">Refund</span>
              <span role="columnheader">Order</span>
              <span role="columnheader">Amount</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (refund of refunds(); track refund.refundId) {
              <div class="admin-table-row buyer-ops-table-row" role="row">
                <span role="cell">
                  <strong>{{ refund.refundId }}</strong>
                  <small>Requested {{ refund.requestedAtUtc | date:'medium' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ refund.orderId }}</strong>
                  @if (refund.returnRequestId) {
                    <small>Return {{ refund.returnRequestId }}</small>
                  } @else {
                    <small>Order-level refund</small>
                  }
                </span>
                <span role="cell">{{ refund.amount | currency:refund.currency:'symbol-narrow' }}</span>
                <span role="cell">
                  <app-status-badge [label]="refund.status" [tone]="refundStatusTone(refund.status)" />
                  <small>{{ refund.statusMessage }}</small>
                </span>
                <span role="cell">
                  <div class="buyer-action-row">
                    <a data-ui-button="secondary" [routerLink]="['/account/orders', refund.orderId]">Order</a>
                    @if (refund.returnRequestId) {
                      <a data-ui-button="secondary" [routerLink]="['/account/returns', refund.returnRequestId]">Return</a>
                    }
                  </div>
                </span>
              </div>
            }
          </div>

          <section class="buyer-panel">
            <h2>Refund timeline</h2>
            <p>Refund status is updated by finance and provider confirmation. Provider action in progress means Mabuntle is still waiting on finance or provider completion.</p>
            <div class="seller-timeline">
              @for (refund of refunds(); track refund.refundId) {
                @for (event of refund.timeline; track event.createdAtUtc + event.eventType) {
                  <div>
                    <app-status-badge [label]="refund.status" [tone]="refundStatusTone(refund.status)" />
                    <span>{{ event.message }}</span>
                    <small>{{ event.createdAtUtc | date:'medium' }}</small>
                  </div>
                }
              }
            </div>
          </section>
        }
      }
    </section>
  `
})
export class BuyerRefundsPageComponent implements OnInit {
  private readonly refundService = inject(BuyerRefundService);

  protected readonly refunds = signal<BuyerRefundResult[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadRefunds();
  }

  protected refundStatusTone(status: string): StatusBadgeTone {
    if (['Requested', 'Approved', 'Processing'].includes(status)) {
      return 'warning';
    }

    if (status === 'Refunded') {
      return 'success';
    }

    if (['Failed', 'Rejected'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  private async loadRefunds(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.refunds.set(await this.refundService.listRefunds());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
