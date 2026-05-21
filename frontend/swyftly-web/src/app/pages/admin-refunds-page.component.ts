import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminRefundResponse } from '../admin/admin-refund.models';
import { AdminRefundService } from '../admin/admin-refund.service';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-refunds-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-finance-page">
      <a class="admin-back-link" routerLink="/admin">Back to dashboard</a>

      <app-page-header
        eyebrow="Admin finance"
        heading="Refunds"
        description="Create refund requests and approve provider refunds with finance role checks visible."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/admin/payouts">Payouts</a>
          <a mat-stroked-button routerLink="/admin/orders">Orders</a>
          <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
        </div>
      </app-page-header>

      <div class="admin-finance-policy">
        <app-status-badge [label]="roleSummary()" tone="accent" />
        <span>Creating a refund requires finance operate. Approving a provider refund requires finance approve and dual-control validation.</span>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading refunds...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <div class="admin-finance-layout">
          <section class="admin-finance-action-panel">
            <h2>Create refund</h2>
            <p>Use an order id or a return request id. The API validates ownership, payment state, and refund eligibility.</p>
            <form [formGroup]="createForm" (ngSubmit)="createRefund()" class="admin-finance-form" novalidate>
              <mat-form-field appearance="outline">
                <mat-label>Order ID</mat-label>
                <input matInput formControlName="orderId" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Return request ID</mat-label>
                <input matInput formControlName="returnRequestId" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Amount</mat-label>
                <input matInput type="number" min="0.01" step="0.01" formControlName="amount" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Reason</mat-label>
                <textarea matInput rows="4" formControlName="reason"></textarea>
              </mat-form-field>

              <button mat-flat-button type="submit" [disabled]="!canOperate() || isActing()">Create refund</button>
            </form>
          </section>

          <aside class="admin-finance-action-panel">
            <h2>Approve refund</h2>
            @if (!selectedRefund()) {
              <p>Select a refund record before approving.</p>
            } @else {
              <app-status-badge [label]="selectedRefund()!.status" [tone]="statusTone(selectedRefund()!.status)" />
              <strong>{{ selectedRefund()!.amount | currency:selectedRefund()!.currency:'symbol-narrow' }}</strong>
              <small>{{ selectedRefund()!.refundId }}</small>

              <form [formGroup]="approveForm" (ngSubmit)="approveRefund()" class="admin-finance-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Approval reason</mat-label>
                  <textarea matInput rows="4" formControlName="reason"></textarea>
                </mat-form-field>
                <button mat-flat-button type="submit" [disabled]="!canApprove() || isActing()">Approve refund</button>
              </form>

              @if (selectedRefund()!.status === 'Processing') {
                <app-ui-alert tone="warning">
                  PayFast refunds are completed in the provider dashboard for now. Confirm only after the dashboard refund is complete and a provider reference is available.
                </app-ui-alert>
                <form [formGroup]="manualConfirmForm" (ngSubmit)="confirmManualProviderRefund()" class="admin-finance-form" novalidate>
                  <mat-form-field appearance="outline">
                    <mat-label>Provider refund reference</mat-label>
                    <input matInput formControlName="providerRefundReference" />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Confirmation reason</mat-label>
                    <textarea matInput rows="3" formControlName="reason"></textarea>
                  </mat-form-field>
                  <button mat-flat-button type="submit" [disabled]="!canApprove() || isActing()">Confirm manual provider refund</button>
                </form>
              }
            }
            @if (!canOperate() || !canApprove()) {
              <p class="admin-finance-note">Read-only users can review refund records. The backend remains authoritative for finance policy and dual-control decisions.</p>
            }
          </aside>
        </div>

        @if (refunds().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Refunds"
            heading="No refund records"
            message="Refund requests created by finance operators will appear here for review and approval."
          />
        } @else {
          <div class="admin-table admin-finance-table" role="table" aria-label="Admin refunds">
            <div class="admin-table-row heading admin-finance-table-row" role="row">
              <span role="columnheader">Refund</span>
              <span role="columnheader">Order</span>
              <span role="columnheader">Amount</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (refund of refunds(); track refund.refundId) {
              <div class="admin-table-row admin-finance-table-row" role="row">
                <span role="cell">
                  <strong>{{ refund.refundId }}</strong>
                  <small>{{ refund.requestedAtUtc | date:'medium' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ refund.orderId }}</strong>
                  <small>{{ refund.returnRequestId ? 'Return ' + refund.returnRequestId : 'Order refund' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ refund.amount | currency:refund.currency:'symbol-narrow' }}</strong>
                  <small>{{ refund.reason }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="refund.status" [tone]="statusTone(refund.status)" />
                  @if (refund.failureReason) {
                    <small>{{ refund.failureReason }}</small>
                  }
                </span>
                <span role="cell">
                  <button mat-stroked-button type="button" (click)="selectRefund(refund)">Select</button>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class AdminRefundsPageComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly refundService = inject(AdminRefundService);

  protected readonly refunds = signal<AdminRefundResponse[]>([]);
  protected readonly selectedRefund = signal<AdminRefundResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly canOperate = computed(() => this.authService.hasAnyRole(['FinanceOperator', 'SuperAdmin']));
  protected readonly canApprove = computed(() => this.authService.hasAnyRole(['FinanceApprover', 'SuperAdmin']));

  protected readonly createForm = this.formBuilder.group({
    orderId: [''],
    returnRequestId: [''],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    reason: ['', [Validators.required]]
  });

  protected readonly manualConfirmForm = this.formBuilder.group({
    providerRefundReference: ['', [Validators.required]],
    reason: ['', [Validators.required]]
  });

  protected readonly approveForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadRefunds();
  }

  protected selectRefund(refund: AdminRefundResponse): void {
    this.selectedRefund.set(refund);
    this.approveForm.reset({ reason: '' });
    this.manualConfirmForm.reset({ providerRefundReference: '', reason: '' });
  }

  protected async createRefund(): Promise<void> {
    if (!this.canOperate()) {
      this.errorMessage.set('You can review refunds, but you do not have finance operate permission.');
      return;
    }

    if (this.createForm.invalid || this.isActing()) {
      this.createForm.markAllAsTouched();
      return;
    }

    const value = this.createForm.getRawValue();
    const orderId = value.orderId.trim();
    const returnRequestId = value.returnRequestId.trim();

    if (orderId.length === 0 && returnRequestId.length === 0) {
      this.errorMessage.set('Provide either an order id or a return request id.');
      return;
    }

    if (orderId.length > 0 && returnRequestId.length > 0) {
      this.errorMessage.set('Provide either an order id or a return request id, not both.');
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const request = { amount: value.amount, reason: value.reason };
      const refund = returnRequestId.length > 0
        ? await this.refundService.createReturnRefund(returnRequestId, request)
        : await this.refundService.createOrderRefund(orderId, request);
      this.refunds.set([refund, ...this.refunds()]);
      this.selectedRefund.set(refund);
      this.createForm.reset({ orderId: '', returnRequestId: '', amount: 0, reason: '' });
      this.successMessage.set('Refund request created.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  protected async approveRefund(): Promise<void> {
    if (!this.canApprove()) {
      this.errorMessage.set('You can review refunds, but you do not have finance approve permission.');
      return;
    }

    const refund = this.selectedRefund();
    if (!refund || this.approveForm.invalid || this.isActing()) {
      this.approveForm.markAllAsTouched();
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.refundService.approveRefund(refund.refundId, this.approveForm.getRawValue());
      this.refunds.set(this.refunds().map(item => item.refundId === updated.refundId ? updated : item));
      this.selectedRefund.set(updated);
      this.approveForm.reset({ reason: '' });
      this.successMessage.set('Refund approved.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  protected async confirmManualProviderRefund(): Promise<void> {
    if (!this.canApprove()) {
      this.errorMessage.set('You can review refunds, but you do not have finance approve permission.');
      return;
    }

    const refund = this.selectedRefund();
    if (!refund || this.manualConfirmForm.invalid || this.isActing()) {
      this.manualConfirmForm.markAllAsTouched();
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.refundService.confirmManualProviderRefund(refund.refundId, this.manualConfirmForm.getRawValue());
      this.refunds.set(this.refunds().map(item => item.refundId === updated.refundId ? updated : item));
      this.selectedRefund.set(updated);
      this.manualConfirmForm.reset({ providerRefundReference: '', reason: '' });
      this.successMessage.set('Manual provider refund confirmed.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
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
    if (['Requested', 'Approved', 'Processing'].includes(status)) {
      return 'warning';
    }

    if (['Refunded'].includes(status)) {
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
      this.refunds.set(await this.refundService.getRefunds());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
