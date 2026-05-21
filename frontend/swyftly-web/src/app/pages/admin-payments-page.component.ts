import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  AdminPaymentReconciliationCandidateResponse,
  AdminPaymentSummaryResponse,
  PaymentReconciliationOutcome
} from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-payments-page',
  imports: [
    AdminWorkspaceNavComponent,
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-finance-page">
      <app-admin-workspace-nav />

      <app-page-header
        eyebrow="Admin finance"
        heading="Payments"
        description="Read-only payment records for provider status, webhook, refund, and ledger investigation."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/admin/orders">Orders</a>
          <a mat-stroked-button routerLink="/admin/refunds">Refunds</a>
        </div>
      </app-page-header>

      @if (reconciliationCandidates().length > 0) {
        <section class="route-card">
          <div class="admin-section-heading">
            <div>
              <p class="eyebrow">Manual reconciliation</p>
              <h2>Payments needing provider review</h2>
            </div>
            <div class="buyer-action-row">
              <app-status-badge [label]="reconciliationCandidates().length + ' open'" tone="warning" />
              <button mat-stroked-button type="button" (click)="toggleSnoozed()">
                {{ includeSnoozed() ? 'Hide snoozed' : 'Include snoozed' }}
              </button>
            </div>
          </div>
          <app-ui-alert tone="info">
            Reconciliation reviews record provider-dashboard evidence only. Valid signed PayFast ITNs remain the only automated settlement path.
          </app-ui-alert>
          <div class="admin-table admin-finance-table" role="table" aria-label="Payment reconciliation candidates">
            @for (candidate of reconciliationCandidates(); track candidate.paymentId) {
              <div class="admin-table-row admin-finance-table-row" role="row">
                <span role="cell">
                  <strong>{{ candidate.provider }}</strong>
                  <small>{{ candidate.providerReference ?? candidate.paymentId }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="candidate.reasonCode" tone="warning" />
                  <small>{{ candidate.status }} since {{ candidate.createdAtUtc | date:'medium' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ candidate.amount | currency:candidate.currency:'symbol-narrow' }}</strong>
                  <small>{{ candidate.recommendedAction }}</small>
                  @if (candidate.latestReview) {
                    <small>
                      Latest review: {{ candidate.latestReview.outcome }}
                      @if (candidate.latestReview.nextReviewAfterUtc) {
                        - next {{ candidate.latestReview.nextReviewAfterUtc | date:'medium' }}
                      }
                    </small>
                  } @else {
                    <small>Unreviewed</small>
                  }
                </span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/admin/payments', candidate.paymentId]">Review</a>
                  <button mat-stroked-button type="button" (click)="selectCandidate(candidate)">Record evidence</button>
                </span>
              </div>
            }
          </div>

          @if (selectedCandidate()) {
            <form [formGroup]="reviewForm" (ngSubmit)="recordReconciliationReview()" class="admin-finance-form reconciliation-review-form" novalidate>
              <div>
                <p class="eyebrow">Review evidence</p>
                <h3>{{ selectedCandidate()!.provider }} {{ selectedCandidate()!.providerReference ?? selectedCandidate()!.paymentId }}</h3>
                @if (isCandidateSnoozed(selectedCandidate()!)) {
                  <app-status-badge label="Snoozed" tone="neutral" />
                }
              </div>

              <mat-form-field appearance="outline">
                <mat-label>Observed provider status</mat-label>
                <input matInput formControlName="observedProviderStatus" placeholder="COMPLETE, PENDING, FAILED" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Observed amount</mat-label>
                <input matInput type="number" min="0" step="0.01" formControlName="observedAmount" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Observed currency</mat-label>
                <input matInput formControlName="observedCurrency" maxlength="3" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Outcome</mat-label>
                <mat-select formControlName="outcome">
                  @for (outcome of reconciliationOutcomes; track outcome) {
                    <mat-option [value]="outcome">{{ outcome }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>

              @if (reviewForm.controls.outcome.value === 'ProviderPaidMissingWebhook') {
                <app-ui-alert tone="warning">
                  Do not mark this order paid from the admin screen. Investigate the provider dashboard and replay or recover the signed PayFast ITN.
                </app-ui-alert>
              }

              <mat-form-field appearance="outline">
                <mat-label>Reason</mat-label>
                <textarea matInput rows="3" formControlName="reason"></textarea>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Next review after</mat-label>
                <input matInput type="datetime-local" formControlName="nextReviewAfterUtc" />
              </mat-form-field>

              <div class="buyer-action-row">
                <button mat-flat-button type="submit" [disabled]="!canApprove() || isActing()">Save review</button>
                <button mat-stroked-button type="button" (click)="clearSelectedCandidate()">Cancel</button>
              </div>
              @if (!canApprove()) {
                <p class="admin-finance-note">You can view reconciliation evidence, but FinanceApprove is required to record a review.</p>
              }
            </form>
          }
        </section>
      }

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-support-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Search</mat-label>
          <input matInput formControlName="search" placeholder="Payment, order, reference" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <input matInput formControlName="status" placeholder="Paid, Pending, Failed" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Order ID</mat-label>
          <input matInput formControlName="orderId" />
        </mat-form-field>

        <div class="buyer-action-row">
          <button mat-flat-button type="submit">Apply filters</button>
          <button mat-stroked-button type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading admin payments...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (filteredPayments().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Payments"
            heading="No payments found"
            message="Payment records matching the current filters will appear here."
          />
        } @else {
          <div class="admin-table admin-finance-table" role="table" aria-label="Admin payments">
            <div class="admin-table-row heading admin-finance-table-row" role="row">
              <span role="columnheader">Payment</span>
              <span role="columnheader">Order</span>
              <span role="columnheader">Amount</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (payment of filteredPayments(); track payment.paymentId) {
              <div class="admin-table-row admin-finance-table-row" role="row">
                <span role="cell">
                  <strong>{{ payment.provider }}</strong>
                  <small>{{ payment.providerReference ?? payment.paymentId }}</small>
                </span>
                <span role="cell">
                  <strong>{{ payment.orderId }}</strong>
                  <small>Buyer {{ payment.buyerId }}</small>
                </span>
                <span role="cell">
                  <strong>{{ payment.amount | currency:payment.currency:'symbol-narrow' }}</strong>
                  <small>{{ payment.createdAtUtc | date:'medium' }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="payment.status" [tone]="statusTone(payment.status)" />
                  @if (payment.paidAtUtc) {
                    <small>Paid {{ payment.paidAtUtc | date:'medium' }}</small>
                  } @else if (payment.failedAtUtc) {
                    <small>Failed {{ payment.failedAtUtc | date:'medium' }}</small>
                  } @else {
                    <small>Updated {{ payment.updatedAtUtc | date:'medium' }}</small>
                  }
                </span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/admin/payments', payment.paymentId]">Open</a>
                </span>
              </div>
            }
          </div>

          <p class="audit-count">{{ filteredPayments().length }} of {{ payments().length }} payment{{ payments().length === 1 ? '' : 's' }}</p>
        }
      }
    </section>
  `
})
export class AdminPaymentsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly adminOrderPaymentService = inject(AdminOrderPaymentService);
  private readonly authService = inject(AuthService);

  protected readonly payments = signal<AdminPaymentSummaryResponse[]>([]);
  protected readonly reconciliationCandidates = signal<AdminPaymentReconciliationCandidateResponse[]>([]);
  protected readonly selectedCandidate = signal<AdminPaymentReconciliationCandidateResponse | null>(null);
  protected readonly includeSnoozed = signal(false);
  protected readonly filters = signal({ search: '', status: '', orderId: '' });
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly reconciliationOutcomes: PaymentReconciliationOutcome[] = [
    'ProviderPending',
    'MatchedNoAction',
    'ProviderPaidMissingWebhook',
    'ProviderFailedMissingWebhook',
    'ManualRecoveryRequired'
  ];

  protected readonly canApprove = computed(() => this.authService.hasAnyRole(['FinanceApprover', 'SuperAdmin']));

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: [''],
    orderId: ['']
  });

  protected readonly reviewForm = this.formBuilder.group({
    observedProviderStatus: ['', [Validators.required]],
    observedAmount: [0],
    observedCurrency: ['ZAR'],
    outcome: ['ProviderPending' as PaymentReconciliationOutcome, [Validators.required]],
    reason: ['', [Validators.required]],
    nextReviewAfterUtc: ['']
  });

  protected readonly filteredPayments = computed(() => {
    const search = this.filters().search.trim().toLowerCase();
    const status = this.filters().status.trim().toLowerCase();

    return this.payments().filter(payment => {
      const matchesStatus = !status || payment.status.toLowerCase().includes(status);
      const searchable = [
        payment.paymentId,
        payment.orderId,
        payment.buyerId,
        payment.provider,
        payment.providerReference ?? ''
      ].join(' ').toLowerCase();
      return matchesStatus && (!search || searchable.includes(search));
    });
  });

  async ngOnInit(): Promise<void> {
    const orderId = this.route.snapshot.queryParamMap.get('orderId') ?? '';
    this.filtersForm.patchValue({ orderId });
    this.filters.set({ search: '', status: '', orderId });
    await this.loadPayments('', orderId);
  }

  protected async applyFilters(): Promise<void> {
    this.filters.set(this.filtersForm.getRawValue());
    await this.loadPayments(this.filters().status, this.filters().orderId);
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset({ search: '', status: '', orderId: '' });
    this.filters.set({ search: '', status: '', orderId: '' });
    await this.loadPayments();
  }

  protected selectCandidate(candidate: AdminPaymentReconciliationCandidateResponse): void {
    this.selectedCandidate.set(candidate);
    this.reviewForm.reset({
      observedProviderStatus: candidate.latestReview?.observedProviderStatus ?? '',
      observedAmount: candidate.latestReview?.observedAmount ?? candidate.amount,
      observedCurrency: candidate.latestReview?.observedCurrency ?? candidate.currency,
      outcome: candidate.latestReview?.outcome ?? 'ProviderPending',
      reason: '',
      nextReviewAfterUtc: ''
    });
  }

  protected clearSelectedCandidate(): void {
    this.selectedCandidate.set(null);
    this.reviewForm.reset({
      observedProviderStatus: '',
      observedAmount: 0,
      observedCurrency: 'ZAR',
      outcome: 'ProviderPending',
      reason: '',
      nextReviewAfterUtc: ''
    });
  }

  protected async toggleSnoozed(): Promise<void> {
    this.includeSnoozed.update(value => !value);
    await this.loadPayments(this.filters().status, this.filters().orderId);
  }

  protected async recordReconciliationReview(): Promise<void> {
    const candidate = this.selectedCandidate();
    if (!this.canApprove()) {
      this.errorMessage.set('FinanceApprove is required to record reconciliation evidence.');
      return;
    }

    if (!candidate || this.reviewForm.invalid || this.isActing()) {
      this.reviewForm.markAllAsTouched();
      return;
    }

    const value = this.reviewForm.getRawValue();
    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const review = await this.adminOrderPaymentService.createPaymentReconciliationReview(candidate.paymentId, {
        observedProviderStatus: value.observedProviderStatus,
        observedAmount: value.observedAmount > 0 ? value.observedAmount : null,
        observedCurrency: value.observedCurrency.trim() || null,
        outcome: value.outcome,
        reason: value.reason,
        nextReviewAfterUtc: this.toUtcIso(value.nextReviewAfterUtc)
      });
      this.reconciliationCandidates.set(this.reconciliationCandidates().map(item =>
        item.paymentId === candidate.paymentId ? { ...item, latestReview: review } : item));
      this.selectedCandidate.set({ ...candidate, latestReview: review });
      this.successMessage.set('Reconciliation review recorded.');
      await this.loadPayments(this.filters().status, this.filters().orderId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  protected isCandidateSnoozed(candidate: AdminPaymentReconciliationCandidateResponse): boolean {
    const nextReview = candidate.latestReview?.nextReviewAfterUtc;
    return nextReview ? Date.parse(nextReview) > Date.now() : false;
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Refunded', 'PartiallyRefunded'].includes(status)) {
      return 'success';
    }

    if (['Failed', 'Cancelled', 'Disputed'].includes(status)) {
      return 'danger';
    }

    if (['Pending', 'Authorized'].includes(status)) {
      return 'warning';
    }

    return 'neutral';
  }

  private async loadPayments(status = '', orderId = ''): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [payments, reconciliationCandidates] = await Promise.all([
        this.adminOrderPaymentService.getPayments(status, orderId),
        this.adminOrderPaymentService.getPaymentReconciliationCandidates(30, this.includeSnoozed())
      ]);
      this.payments.set(payments);
      this.reconciliationCandidates.set(reconciliationCandidates);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private toUtcIso(value: string): string | null {
    return value.trim() ? new Date(value).toISOString() : null;
  }
}
