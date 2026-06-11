import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminPayoutProfileChangeRequestResponse } from '../admin/admin-payout-profile-change.models';
import { AdminPayoutProfileChangeService } from '../admin/admin-payout-profile-change.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';
import { WorkspaceShellComponent } from '../shared/ui/workspace-shell.component';

type ReviewAction = 'approve' | 'reject';

@Component({
  selector: 'app-admin-payout-profile-changes-page',
  imports: [
    AdminWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent,
    WorkspaceShellComponent
  ],
  template: `
    <section class="page admin-finance-page">
      <app-workspace-shell>
        <app-admin-workspace-nav workspaceNav />

        <app-page-header
          eyebrow="Finance review"
          heading="Payout profile changes"
          description="Review seller-submitted payout provider reference changes before they become the live payout profile."
        >
          <div pageHeaderActions>
            <a data-ui-button="secondary" routerLink="/payouts">Payouts</a>
            <a data-ui-button="secondary" routerLink="/audit-logs">Audit logs</a>
          </div>
        </app-page-header>

        <div class="admin-finance-policy">
          <app-status-badge [label]="canApprove() ? 'Finance approve' : 'Read only'" [tone]="canApprove() ? 'accent' : 'neutral'" />
          <span>Approve or reject requires FinanceApprover or SuperAdmin. The requester can never approve their own payout change.</span>
        </div>

        @if (isLoading()) {
          <div class="route-card">Loading payout profile change requests...</div>
        } @else {
          @if (errorMessage()) {
            <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
          }

          @if (successMessage()) {
            <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
          }

          @if (requests().length === 0 && !errorMessage()) {
            <app-empty-state
              eyebrow="Payout profile"
              heading="No pending changes"
              message="Seller payout-profile change requests will appear here after sellers submit them for finance review."
            />
          } @else {
            <div class="admin-finance-layout">
              <div class="hf-admin-queue-card">
                <div class="hf-admin-card-heading">
                  <div>
                    <span>Pending queue</span>
                    <h2>{{ requests().length }} change request{{ requests().length === 1 ? '' : 's' }}</h2>
                  </div>
                  <app-status-badge [label]="requests().length + ' pending'" [tone]="requests().length > 0 ? 'warning' : 'success'" />
                </div>

                <div class="admin-table admin-finance-table" role="table" aria-label="Payout profile change requests">
                  <div class="admin-table-row heading admin-finance-table-row" role="row">
                    <span role="columnheader">Seller</span>
                    <span role="columnheader">Submitted</span>
                    <span role="columnheader">Current</span>
                    <span role="columnheader">Proposed</span>
                    <span role="columnheader">Action</span>
                  </div>

                  @for (request of requests(); track request.requestId) {
                    <div
                      class="admin-table-row admin-finance-table-row hf-admin-select-row"
                      [class.active]="selectedRequest()?.requestId === request.requestId"
                      role="row"
                      (click)="selectRequest(request)"
                    >
                      <span role="cell">
                        <strong>{{ request.sellerDisplayName || request.sellerId }}</strong>
                        <small>{{ request.sellerContactEmail || 'No contact email' }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ request.submittedAtUtc | date:'medium' }}</strong>
                        <small>{{ request.status }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ request.currentPayoutProviderReference || 'Missing' }}</strong>
                        <small>{{ request.currentPayoutIsAdminApproved ? 'Approved' : 'Not approved' }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ request.proposedPayoutProviderReference }}</strong>
                        <small>{{ request.reason }}</small>
                      </span>
                      <span role="cell">
                        <button data-ui-button="secondary" type="button" (click)="selectRequest(request); $event.stopPropagation()">Review</button>
                      </span>
                    </div>
                  }
                </div>
              </div>

              <aside class="admin-finance-action-panel hf-admin-finance-panel">
                <div class="hf-admin-card-heading">
                  <div>
                    <span>Review detail</span>
                    <h2>Current vs proposed</h2>
                  </div>
                  @if (selectedRequest()) {
                    <app-status-badge [label]="selectedRequest()!.status" [tone]="statusTone(selectedRequest()!.status)" />
                  }
                </div>

                @if (!selectedRequest()) {
                  <p>Select a payout profile change request to review the seller evidence.</p>
                } @else {
                  <div class="payout-profile-change-compare">
                    <div>
                      <span>Current approved reference</span>
                      <strong>{{ selectedRequest()!.currentPayoutProviderReference || 'Missing' }}</strong>
                    </div>
                    <div>
                      <span>Proposed reference</span>
                      <strong>{{ selectedRequest()!.proposedPayoutProviderReference }}</strong>
                    </div>
                  </div>

                  <app-ui-alert tone="warning">
                    Approval changes the live payout provider reference. Existing payout records stay unchanged, but payout processing is blocked while this request is pending.
                  </app-ui-alert>

                  <p><strong>Seller reason:</strong> {{ selectedRequest()!.reason }}</p>
                  <small>Requested by {{ selectedRequest()!.requestedByUserId }}</small>

                  <form [formGroup]="reviewForm" class="admin-finance-form" novalidate>
                    <label class="ui-field">
                      <span>Review reason</span>
                      <textarea rows="4" formControlName="reason"></textarea>
                    </label>

                    <div class="admin-finance-actions">
                      <button data-ui-button="primary" type="button" [disabled]="!canApprove() || isActing()" (click)="review('approve')">Approve</button>
                      <button data-ui-button="secondary" type="button" [disabled]="!canApprove() || isActing()" (click)="review('reject')">Reject</button>
                    </div>
                  </form>

                  @if (!canApprove()) {
                    <p class="admin-finance-note">You can view this queue, but FinanceApprove is required to approve or reject payout-profile changes.</p>
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
export class AdminPayoutProfileChangesPageComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly service = inject(AdminPayoutProfileChangeService);

  protected readonly requests = signal<AdminPayoutProfileChangeRequestResponse[]>([]);
  protected readonly selectedRequest = signal<AdminPayoutProfileChangeRequestResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly canApprove = computed(() => this.authService.hasAnyRole(['FinanceApprover', 'SuperAdmin']));

  protected readonly reviewForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadRequests();
  }

  protected async selectRequest(request: AdminPayoutProfileChangeRequestResponse): Promise<void> {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.reviewForm.reset({ reason: '' });

    try {
      this.selectedRequest.set(await this.service.get(request.requestId));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    }
  }

  protected async review(action: ReviewAction): Promise<void> {
    const request = this.selectedRequest();
    if (!request || this.reviewForm.invalid || this.isActing()) {
      this.reviewForm.markAllAsTouched();
      return;
    }

    if (!this.canApprove()) {
      this.errorMessage.set('FinanceApprove is required to review payout-profile changes.');
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const payload = this.reviewForm.getRawValue();
      const updated = action === 'approve'
        ? await this.service.approve(request.requestId, payload)
        : await this.service.reject(request.requestId, payload);
      this.requests.set(this.requests().filter(item => item.requestId !== updated.requestId));
      this.selectedRequest.set(updated);
      this.successMessage.set(action === 'approve'
        ? 'Payout profile change approved.'
        : 'Payout profile change rejected.');
      this.reviewForm.reset({ reason: '' });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (status === 'Approved') {
      return 'success';
    }

    if (status === 'Rejected' || status === 'Cancelled') {
      return 'danger';
    }

    if (status === 'PendingReview') {
      return 'warning';
    }

    return 'neutral';
  }

  private async loadRequests(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const requests = await this.service.list();
      this.requests.set(requests);
      this.selectedRequest.set(requests[0] ?? null);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
