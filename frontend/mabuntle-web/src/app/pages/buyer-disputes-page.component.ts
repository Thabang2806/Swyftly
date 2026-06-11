import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerDisputeResponse } from '../buyer/buyer-dispute.models';
import { BuyerDisputeService } from '../buyer/buyer-dispute.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-disputes-page',
  imports: [
    BuyerWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Disputes"
        description="Review dispute messages and evidence. Buyer-favoured outcomes may create refund requests that still require finance processing."
      />

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
            heading="No disputes"
            message="Standalone dispute cases will appear here when one is opened. Return escalations stay on the related return until support creates a formal dispute case, and any refund outcome appears under Refunds after finance starts processing it."
          />
        } @else {
          <div class="admin-finance-layout buyer-case-layout">
            <div class="admin-table buyer-ops-table" role="table" aria-label="Buyer disputes">
              <div class="admin-table-row heading buyer-ops-table-row" role="row">
                <span role="columnheader">Dispute</span>
                <span role="columnheader">Order</span>
                <span role="columnheader">Opened</span>
                <span role="columnheader">Status</span>
                <span role="columnheader">Action</span>
              </div>

              @for (dispute of disputes(); track dispute.disputeId) {
                <div class="admin-table-row buyer-ops-table-row" role="row">
                  <span role="cell">
                    <strong>{{ dispute.reason }}</strong>
                    <small>{{ dispute.disputeId }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ dispute.orderId }}</strong>
                    <small>{{ dispute.returnRequestId ? 'Return ' + dispute.returnRequestId : 'Order dispute' }}</small>
                  </span>
                  <span role="cell">{{ dispute.openedAtUtc | date:'medium' }}</span>
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
              <h2>Case detail</h2>
              @if (!selectedDispute()) {
                <p>Select a dispute to review messages and add evidence.</p>
              } @else {
                <app-status-badge [label]="selectedDispute()!.status" [tone]="statusTone(selectedDispute()!.status)" />
                <strong>{{ selectedDispute()!.reason }}</strong>
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

                <form [formGroup]="messageForm" (ngSubmit)="addMessage()" class="admin-finance-form" novalidate>
                  <label class="ui-field">
                    <span>Message</span>
                    <textarea rows="3" formControlName="message"></textarea>
                  </label>
                  <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Send message</button>
                </form>

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

                <form [formGroup]="evidenceForm" (ngSubmit)="addEvidence()" class="admin-finance-form" novalidate>
                  <label class="ui-field">
                    <span>Evidence type</span>
                    <select formControlName="evidenceType">
                      <option value="Image">Image</option>
                      <option value="Document">Document</option>
                      <option value="Message">Message</option>
                      <option value="Other">Other</option>
                    </select>
                  </label>

                  <label class="ui-field">
                    <span>Storage reference or URL</span>
                    <input formControlName="storageReference" />
                  </label>

                  <label class="ui-field">
                    <span>Description</span>
                    <textarea rows="2" formControlName="description"></textarea>
                  </label>

                  <button data-ui-button="secondary" type="submit" [disabled]="isSaving()">Add evidence</button>
                </form>
              }
            </aside>
          </div>
        }
      }
    </section>
  `
})
export class BuyerDisputesPageComponent implements OnInit {
  private readonly disputeService = inject(BuyerDisputeService);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly disputes = signal<BuyerDisputeResponse[]>([]);
  protected readonly selectedDispute = signal<BuyerDisputeResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly messageForm = this.formBuilder.group({
    message: ['', [Validators.required]]
  });

  protected readonly evidenceForm = this.formBuilder.group({
    evidenceType: ['Image', [Validators.required]],
    storageReference: ['', [Validators.required]],
    description: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadDisputes();
  }

  protected selectDispute(dispute: BuyerDisputeResponse): void {
    this.selectedDispute.set(dispute);
    this.messageForm.reset({ message: '' });
    this.evidenceForm.reset({ evidenceType: 'Image', storageReference: '', description: '' });
  }

  protected async addMessage(): Promise<void> {
    const dispute = this.selectedDispute();
    if (!dispute || this.messageForm.invalid || this.isSaving()) {
      this.messageForm.markAllAsTouched();
      return;
    }

    await this.runAction(
      () => this.disputeService.addMessage(dispute.disputeId, this.messageForm.getRawValue()),
      'Message added.');
    this.messageForm.reset({ message: '' });
  }

  protected async addEvidence(): Promise<void> {
    const dispute = this.selectedDispute();
    if (!dispute || this.evidenceForm.invalid || this.isSaving()) {
      this.evidenceForm.markAllAsTouched();
      return;
    }

    const value = this.evidenceForm.getRawValue();
    await this.runAction(
      () => this.disputeService.addEvidence(dispute.disputeId, {
        evidenceType: value.evidenceType,
        storageReference: value.storageReference,
        description: emptyToNull(value.description)
      }),
      'Evidence added.');
    this.evidenceForm.reset({ evidenceType: 'Image', storageReference: '', description: '' });
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
      const disputes = await this.disputeService.listDisputes();
      this.disputes.set(disputes);
      this.selectedDispute.set(disputes[0] ?? null);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(action: () => Promise<BuyerDisputeResponse>, successMessage: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await action();
      this.disputes.set(this.disputes().map(item => item.disputeId === updated.disputeId ? updated : item));
      this.selectedDispute.set(updated);
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
