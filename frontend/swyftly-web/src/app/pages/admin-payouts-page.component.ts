import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminPayoutResponse } from '../admin/admin-payout.models';
import { AdminPayoutService } from '../admin/admin-payout.service';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { MetricTileComponent } from '../shared/ui/metric-tile.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';
import { WorkspaceShellComponent } from '../shared/ui/workspace-shell.component';

type PayoutAction = 'hold' | 'release' | 'make-available' | 'process' | 'reconcile';

@Component({
  selector: 'app-admin-payouts-page',
  imports: [
    AdminWorkspaceNavComponent,
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MetricTileComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent,
    WorkspaceShellComponent
  ],
  template: `
    <section class="page admin-finance-page hf-admin-console">
      <app-workspace-shell>
        <app-admin-workspace-nav workspaceNav />

        <app-page-header
          eyebrow="Finance operations"
          heading="Seller balances and payouts"
          description="Review pending and held seller payouts, with finance role checks visible before each action."
        >
          <div pageHeaderActions>
            <a mat-stroked-button routerLink="/admin/refunds">Refunds</a>
            <a mat-stroked-button routerLink="/admin/reports">Reports</a>
            <a mat-flat-button routerLink="/admin/audit-logs">Audit logs</a>
          </div>
        </app-page-header>

        <div class="hf-metric-grid">
          @for (metric of payoutMetrics(); track metric.label) {
            <app-metric-tile
              [label]="metric.label"
              [value]="metric.value"
              [badge]="metric.badge"
              [badgeTone]="metric.tone"
            />
          }
        </div>

      <div class="admin-finance-policy">
        <app-status-badge [label]="roleSummary()" tone="accent" />
        <span>Hold and make available require finance operate. Release, process, and reconcile require finance approve.</span>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading payouts...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (payouts().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Payouts"
            heading="No pending payouts"
            message="Pending or held payout records will appear here when seller ledger activity is ready for finance review."
          />
        } @else {
          <div class="admin-finance-layout hf-admin-finance-layout">
            <div class="hf-admin-queue-card">
              <div class="hf-admin-card-heading">
                <div>
                  <span>Payout queue</span>
                  <h2>Pending finance review</h2>
                </div>
                <app-status-badge [label]="manualReviewCount() + ' need review'" [tone]="manualReviewCount() > 0 ? 'warning' : 'success'" />
              </div>

            <div class="admin-table admin-finance-table" role="table" aria-label="Pending payouts">
              <div class="admin-table-row heading admin-finance-table-row" role="row">
                <span role="columnheader">Payout</span>
                <span role="columnheader">Seller</span>
                <span role="columnheader">Amount</span>
                <span role="columnheader">Status</span>
                <span role="columnheader">Action</span>
              </div>

              @for (payout of payouts(); track payout.payoutId) {
                <div
                  class="admin-table-row admin-finance-table-row hf-admin-select-row"
                  role="row"
                  [class.active]="selectedPayout()?.payoutId === payout.payoutId"
                  (click)="selectPayout(payout)"
                >
                  <span role="cell">
                    <strong>{{ payout.payoutId }}</strong>
                    <small>{{ payout.createdAtUtc | date:'medium' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ payout.sellerId }}</strong>
                    <small>{{ payout.items.length }} payout item{{ payout.items.length === 1 ? '' : 's' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ payout.amount | currency:payout.currency:'symbol-narrow' }}</strong>
                    <small>{{ payoutItemSummary(payout) }}</small>
                  </span>
                  <span role="cell">
                    <app-status-badge [label]="payout.status" [tone]="statusTone(payout.status)" />
                    @if (payout.holdReason) {
                      <small>Hold: {{ payout.holdReason }}</small>
                    }
                  </span>
                  <span role="cell">
                    <button mat-stroked-button type="button" (click)="selectPayout(payout); $event.stopPropagation()">Select</button>
                  </span>
                </div>
              }
            </div>
            </div>

            <aside class="admin-finance-action-panel hf-admin-finance-panel">
              <div class="hf-admin-card-heading">
                <div>
                  <span>Ledger snapshot</span>
                  <h2>Payout action</h2>
                </div>
                <app-status-badge [label]="roleSummary()" tone="accent" />
              </div>

              <div class="hf-ledger-snapshot">
                <div>
                  <span>Queue total</span>
                  <strong>{{ totalPayoutAmount() | currency:'ZAR':'symbol-narrow' }}</strong>
                </div>
                <div>
                  <span>Held total</span>
                  <strong>{{ heldPayoutAmount() | currency:'ZAR':'symbol-narrow' }}</strong>
                </div>
                <div>
                  <span>Payout items</span>
                  <strong>{{ payoutItemCount() }}</strong>
                </div>
              </div>

              @if (!selectedPayout()) {
                <p>Select a payout to review available finance actions.</p>
              } @else {
                <app-status-badge [label]="selectedPayout()!.status" [tone]="statusTone(selectedPayout()!.status)" />
                <strong>{{ selectedPayout()!.amount | currency:selectedPayout()!.currency:'symbol-narrow' }}</strong>
                <small>{{ selectedPayout()!.payoutId }}</small>

                <form [formGroup]="reasonForm" class="admin-finance-form" novalidate>
                  <mat-form-field appearance="outline">
                    <mat-label>Reason</mat-label>
                    <textarea matInput rows="4" formControlName="reason"></textarea>
                  </mat-form-field>

                  <div class="admin-finance-actions">
                    <button mat-stroked-button type="button" [disabled]="!canOperate() || isActing()" (click)="runAction('hold')">Hold</button>
                    <button mat-stroked-button type="button" [disabled]="!canApprove() || isActing()" (click)="runAction('release')">Release</button>
                    <button mat-stroked-button type="button" [disabled]="!canOperate() || isActing()" (click)="runAction('make-available')">Make available</button>
                    <button mat-flat-button type="button" [disabled]="!canApprove() || isActing()" (click)="runAction('process')">Process</button>
                    <button mat-stroked-button type="button" [disabled]="!canApprove() || isActing()" (click)="runAction('reconcile')">Reconcile</button>
                  </div>
                </form>

                @if (!canOperate() || !canApprove()) {
                  <p class="admin-finance-note">Unavailable actions are still enforced by the API. Dual-control conflicts are shown as finance errors when returned by the server.</p>
                }
              }
            </aside>
          </div>
        }
      }
      </app-workspace-shell>
    </section>
  `
})
export class AdminPayoutsPageComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly payoutService = inject(AdminPayoutService);

  protected readonly payouts = signal<AdminPayoutResponse[]>([]);
  protected readonly selectedPayout = signal<AdminPayoutResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly canOperate = computed(() => this.authService.hasAnyRole(['FinanceOperator', 'SuperAdmin']));
  protected readonly canApprove = computed(() => this.authService.hasAnyRole(['FinanceApprover', 'SuperAdmin']));

  protected readonly payoutMetrics = computed(() => [
    {
      label: 'Pending payouts',
      value: this.payouts().length.toString(),
      badge: 'Queue',
      tone: 'warning' as StatusBadgeTone
    },
    {
      label: 'Queue total',
      value: this.formatZar(this.totalPayoutAmount()),
      badge: 'Internal',
      tone: 'accent' as StatusBadgeTone
    },
    {
      label: 'Held amount',
      value: this.formatZar(this.heldPayoutAmount()),
      badge: this.heldPayoutAmount() > 0 ? 'Held' : 'Clear',
      tone: this.heldPayoutAmount() > 0 ? 'warning' as StatusBadgeTone : 'success' as StatusBadgeTone
    },
    {
      label: 'Payout items',
      value: this.payoutItemCount().toString(),
      badge: 'Ledger',
      tone: 'neutral' as StatusBadgeTone
    }
  ]);

  protected readonly reasonForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadPayouts();
  }

  protected selectPayout(payout: AdminPayoutResponse): void {
    this.selectedPayout.set(payout);
    this.reasonForm.reset({ reason: '' });
  }

  protected async runAction(action: PayoutAction): Promise<void> {
    if (!this.canRunAction(action)) {
      this.errorMessage.set('You can review payouts, but you do not have permission for this finance action.');
      return;
    }

    const payout = this.selectedPayout();
    if (!payout || this.reasonForm.invalid || this.isActing()) {
      this.reasonForm.markAllAsTouched();
      return;
    }

    const request = this.reasonForm.getRawValue();
    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.executeAction(action, payout.payoutId, request);
      this.replacePayout(updated);
      this.selectedPayout.set(updated);
      this.successMessage.set('Payout action completed.');
      this.reasonForm.reset({ reason: '' });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  protected payoutItemSummary(payout: AdminPayoutResponse): string {
    const firstItem = payout.items[0];
    if (!firstItem) {
      return 'No payout items';
    }

    return firstItem.orderId ? `Order ${firstItem.orderId}` : `Ledger ${firstItem.ledgerEntryId}`;
  }

  protected totalPayoutAmount(): number {
    return this.payouts().reduce((total, payout) => total + payout.amount, 0);
  }

  protected heldPayoutAmount(): number {
    return this.payouts()
      .filter(payout => payout.status === 'OnHold')
      .reduce((total, payout) => total + payout.amount, 0);
  }

  protected payoutItemCount(): number {
    return this.payouts().reduce((total, payout) => total + payout.items.length, 0);
  }

  protected manualReviewCount(): number {
    return this.payouts().filter(payout => payout.status === 'Pending' || payout.status === 'OnHold').length;
  }

  protected roleSummary(): string {
    if (this.canOperate() && this.canApprove()) {
      return 'Operate and approve';
    }

    if (this.canOperate()) {
      return 'Operate only';
    }

    if (this.canApprove()) {
      return 'Approve only';
    }

    return 'Read only';
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Pending', 'Available', 'Processing'].includes(status)) {
      return 'accent';
    }

    if (status === 'PaidOut') {
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
      const payouts = await this.payoutService.getPendingPayouts();
      this.payouts.set(payouts);
      this.selectedPayout.set(payouts[0] ?? null);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private formatZar(value: number): string {
    return new Intl.NumberFormat('en-ZA', {
      style: 'currency',
      currency: 'ZAR',
      maximumFractionDigits: 0
    }).format(value);
  }

  private executeAction(
    action: PayoutAction,
    payoutId: string,
    request: { reason: string }
  ): Promise<AdminPayoutResponse> {
    switch (action) {
      case 'hold':
        return this.payoutService.holdPayout(payoutId, request);
      case 'release':
        return this.payoutService.releasePayout(payoutId, request);
      case 'make-available':
        return this.payoutService.makePayoutAvailable(payoutId, request);
      case 'process':
        return this.payoutService.processPayout(payoutId, request);
      case 'reconcile':
        return this.payoutService.reconcilePayout(payoutId, request);
    }
  }

  private canRunAction(action: PayoutAction): boolean {
    if (action === 'hold' || action === 'make-available') {
      return this.canOperate();
    }

    return this.canApprove();
  }

  private replacePayout(updated: AdminPayoutResponse): void {
    this.payouts.set(this.payouts().map(payout => payout.payoutId === updated.payoutId ? updated : payout));
  }
}
