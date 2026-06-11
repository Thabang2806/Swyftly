import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminDisputeResponse } from '../admin/admin-dispute.models';
import { AdminDisputeService } from '../admin/admin-dispute.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-disputes-page',
  imports: [
    DatePipe,
    EmptyStateComponent,
    AdminWorkspaceNavComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-finance-page">
      <app-admin-workspace-nav />

      <a class="admin-back-link" routerLink="">Back to dashboard</a>

      <app-page-header
        eyebrow="Admin operations"
        heading="Disputes"
        description="Review buyer and seller evidence, then resolve the dispute with an auditable outcome."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/refunds">Refunds</a>
          <a data-ui-button="secondary" routerLink="/payouts">Payouts</a>
          <a data-ui-button="secondary" routerLink="/audit-logs">Audit logs</a>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading disputes...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (disputes().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Disputes"
            heading="No active disputes"
            message="Disputes opened by buyers or sellers will appear here for admin review."
          />
        } @else {
          <div class="admin-finance-layout">
            <div class="admin-table admin-finance-table" role="table" aria-label="Admin disputes">
              <div class="admin-table-row heading admin-finance-table-row" role="row">
                <span role="columnheader">Dispute</span>
                <span role="columnheader">Order</span>
                <span role="columnheader">Parties</span>
                <span role="columnheader">Status</span>
                <span role="columnheader">Action</span>
              </div>

              @for (dispute of disputes(); track dispute.disputeId) {
                <div class="admin-table-row admin-finance-table-row" role="row">
                  <span role="cell">
                    <strong>{{ dispute.reason }}</strong>
                    <small>{{ dispute.openedAtUtc | date:'medium' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ dispute.orderId }}</strong>
                    <small>{{ dispute.returnRequestId ? 'Return ' + dispute.returnRequestId : 'Order dispute' }}</small>
                  </span>
                  <span role="cell">
                    <strong>Buyer {{ dispute.buyerId }}</strong>
                    <small>Seller {{ dispute.sellerId }}</small>
                  </span>
                  <span role="cell">
                    <app-status-badge [label]="dispute.status" [tone]="statusTone(dispute.status)" />
                    @if (dispute.resolutionReason) {
                      <small>{{ dispute.resolutionReason }}</small>
                    }
                  </span>
                  <span role="cell">
                    <button data-ui-button="secondary" type="button" (click)="selectDispute(dispute)">Review</button>
                  </span>
                </div>
              }
            </div>

            <aside class="admin-finance-action-panel">
              <h2>Dispute review</h2>
              @if (!selectedDispute()) {
                <p>Select a dispute to review messages, evidence, and resolution options.</p>
              } @else {
                <app-status-badge [label]="selectedDispute()!.status" [tone]="statusTone(selectedDispute()!.status)" />
                <strong>{{ selectedDispute()!.disputeId }}</strong>
                <small>Opened {{ selectedDispute()!.openedAtUtc | date:'medium' }}</small>

                <section class="admin-finance-subsection">
                  <h3>Messages</h3>
                  @if (selectedDispute()!.messages.length === 0) {
                    <p>No dispute messages yet.</p>
                  } @else {
                    @for (message of selectedDispute()!.messages; track message.disputeMessageId) {
                      <article class="admin-finance-note-card">
                        <strong>{{ message.senderRole }}</strong>
                        <span>{{ message.createdAtUtc | date:'medium' }}</span>
                        <p>{{ message.message }}</p>
                      </article>
                    }
                  }
                </section>

                <section class="admin-finance-subsection">
                  <h3>Evidence</h3>
                  @if (selectedDispute()!.evidence.length === 0) {
                    <p>No evidence has been attached.</p>
                  } @else {
                    @for (item of selectedDispute()!.evidence; track item.disputeEvidenceId) {
                      <article class="admin-finance-note-card">
                        <strong>{{ item.evidenceType }}</strong>
                        <span>{{ item.submittedByRole }} - {{ item.createdAtUtc | date:'medium' }}</span>
                        <p>{{ item.storageReference }}</p>
                        @if (item.description) {
                          <small>{{ item.description }}</small>
                        }
                      </article>
                    }
                  }
                </section>

                <form [formGroup]="resolveForm" (ngSubmit)="resolveDispute()" class="admin-finance-form" novalidate>
                  <label class="ui-field">
                    <span>Outcome</span>
                    <select formControlName="outcome">
                      <option value="BuyerFavoured">Buyer favoured</option>
                      <option value="SellerFavoured">Seller favoured</option>
                    </select>
                  </label>

                  <label class="ui-field">
                    <span>Resolution reason</span>
                    <textarea rows="4" formControlName="reason"></textarea>
                  </label>

                  <button data-ui-button="primary" type="submit" [disabled]="isActing()">Resolve dispute</button>
                </form>
              }
            </aside>
          </div>
        }
      }
    </section>
  `
})
export class AdminDisputesPageComponent implements OnInit {
  private readonly disputeService = inject(AdminDisputeService);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly disputes = signal<AdminDisputeResponse[]>([]);
  protected readonly selectedDispute = signal<AdminDisputeResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly resolveForm = this.formBuilder.group({
    outcome: ['BuyerFavoured' as 'BuyerFavoured' | 'SellerFavoured', [Validators.required]],
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadDisputes();
  }

  protected selectDispute(dispute: AdminDisputeResponse): void {
    this.selectedDispute.set(dispute);
    this.resolveForm.reset({ outcome: 'BuyerFavoured', reason: '' });
  }

  protected async resolveDispute(): Promise<void> {
    const dispute = this.selectedDispute();
    if (!dispute || this.resolveForm.invalid || this.isActing()) {
      this.resolveForm.markAllAsTouched();
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.disputeService.resolveDispute(dispute.disputeId, this.resolveForm.getRawValue());
      this.disputes.set(this.disputes().map(item => item.disputeId === updated.disputeId ? updated : item));
      this.selectedDispute.set(updated);
      this.successMessage.set('Dispute resolved.');
      this.resolveForm.reset({ outcome: 'BuyerFavoured', reason: '' });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Open', 'UnderReview', 'Disputed'].includes(status)) {
      return 'warning';
    }

    if (['Resolved', 'Closed'].includes(status)) {
      return 'success';
    }

    return 'neutral';
  }

  private async loadDisputes(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.disputes.set(await this.disputeService.getDisputes());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
