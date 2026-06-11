import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerBalanceResponse, SellerPayoutResponse } from '../seller/seller-payout.models';
import { SellerPayoutService } from '../seller/seller-payout.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-payouts-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page">
      <app-seller-workspace-nav />

      <app-page-header
        eyebrow="Seller finance"
        heading="Payouts"
        description="Read-only seller balance and payout history. Admin finance actions are handled separately."
      />

      @if (isLoading()) {
        <div class="route-card">Loading payouts...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (balance()) {
          <section class="seller-balance-grid" aria-label="Seller balances">
            @for (currencyBalance of balance()!.balances; track currencyBalance.currency) {
              <article class="seller-balance-card">
                <app-status-badge [label]="currencyBalance.currency" tone="accent" />
                <dl>
                  <div>
                    <dt>Pending</dt>
                    <dd>{{ currencyBalance.pendingBalance | currency:currencyBalance.currency:'symbol-narrow' }}</dd>
                  </div>
                  <div>
                    <dt>Available</dt>
                    <dd>{{ currencyBalance.availableBalance | currency:currencyBalance.currency:'symbol-narrow' }}</dd>
                  </div>
                  <div>
                    <dt>Held</dt>
                    <dd>{{ currencyBalance.heldBalance | currency:currencyBalance.currency:'symbol-narrow' }}</dd>
                  </div>
                </dl>
              </article>
            }
          </section>
        }

        @if (payouts().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Payouts"
            heading="No payout records yet"
            message="Payout records are created from internal ledger activity after paid orders are processed."
          />
        } @else {
          <div class="admin-table seller-ops-table" role="table" aria-label="Seller payouts">
            <div class="admin-table-row heading seller-ops-table-row" role="row">
              <span role="columnheader">Payout</span>
              <span role="columnheader">Amount</span>
              <span role="columnheader">Items</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Hold</span>
            </div>

            @for (payout of payouts(); track payout.payoutId) {
              <div class="admin-table-row seller-ops-table-row" role="row">
                <span role="cell">
                  <strong>{{ payout.payoutId }}</strong>
                  <small>{{ payout.createdAtUtc | date:'medium' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ payout.amount | currency:payout.currency:'symbol-narrow' }}</strong>
                  <small>{{ payout.currency }}</small>
                </span>
                <span role="cell">
                  <strong>{{ payout.items.length }} item{{ payout.items.length === 1 ? '' : 's' }}</strong>
                  <small>{{ payoutItemSummary(payout) }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="payout.status" [tone]="statusTone(payout.status)" />
                </span>
                <span role="cell">
                  <strong>{{ payout.holdReason ?? 'None' }}</strong>
                  @if (payout.heldAtUtc) {
                    <small>{{ payout.heldAtUtc | date:'mediumDate' }}</small>
                  }
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class SellerPayoutsPageComponent implements OnInit {
  private readonly payoutService = inject(SellerPayoutService);

  protected readonly balance = signal<SellerBalanceResponse | null>(null);
  protected readonly payouts = signal<SellerPayoutResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadPayouts();
  }

  protected payoutItemSummary(payout: SellerPayoutResponse): string {
    const firstItem = payout.items[0];
    if (!firstItem) {
      return 'No ledger items attached';
    }

    return `${firstItem.sourceType} item ${firstItem.amount.toLocaleString(undefined, {
      style: 'currency',
      currency: firstItem.currency,
      currencyDisplay: 'narrowSymbol'
    })}`;
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Pending', 'Available', 'Processing'].includes(status)) {
      return 'accent';
    }

    if (['PaidOut'].includes(status)) {
      return 'success';
    }

    if (['OnHold', 'Failed', 'Reversed'].includes(status)) {
      return 'warning';
    }

    return 'neutral';
  }

  private async loadPayouts(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [balance, payouts] = await Promise.all([
        this.payoutService.getBalance(),
        this.payoutService.listPayouts()
      ]);
      this.balance.set(balance);
      this.payouts.set(payouts);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
