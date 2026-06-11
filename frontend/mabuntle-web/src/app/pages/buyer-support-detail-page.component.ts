import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerSupportTicketResponse } from '../buyer/buyer-support.models';
import { BuyerSupportService } from '../buyer/buyer-support.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-support-detail-page',
  imports: [
    BuyerWorkspaceNavComponent,
    DatePipe,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <a class="admin-back-link" routerLink="/account/support">Back to support</a>

      <app-page-header
        eyebrow="Buyer support"
        [heading]="ticket()?.subject ?? 'Support ticket'"
        [description]="ticket()?.description ?? 'Review support ticket details and messages.'"
      >
        <div pageHeaderActions>
          @if (ticket()) {
            <app-status-badge [label]="ticket()!.status" [tone]="statusTone(ticket()!.status)" />
          }
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading support ticket...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (ticket()) {
          <div class="buyer-detail-grid">
            <section class="buyer-panel">
              <h2>Ticket context</h2>
              <dl class="seller-facts">
                <div><dt>Category</dt><dd>{{ ticket()!.category }}</dd></div>
                <div><dt>Opened</dt><dd>{{ ticket()!.openedAtUtc | date:'medium' }}</dd></div>
                <div><dt>Linked order</dt><dd>{{ ticket()!.linkedOrderId ?? 'None' }}</dd></div>
                <div><dt>Linked seller</dt><dd>{{ ticket()!.linkedSellerId ?? 'None' }}</dd></div>
              </dl>
            </section>

            <section class="buyer-panel">
              <h2>Add message</h2>
              <form [formGroup]="messageForm" (ngSubmit)="addMessage()" class="buyer-form-grid" novalidate>
                <label class="ui-field">
                  <span>Message</span>
                  <textarea rows="5" formControlName="message"></textarea>
                </label>
                <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Send message</button>
              </form>
            </section>
          </div>

          <section class="buyer-panel">
            <h2>Conversation</h2>
            @if (ticket()!.messages.length === 0) {
              <p>No public messages yet.</p>
            } @else {
              <div class="seller-message-list">
                @for (message of ticket()!.messages; track message.supportMessageId) {
                  <article>
                    <strong>{{ message.senderRole }}</strong>
                    <span>{{ message.createdAtUtc | date:'medium' }}</span>
                    <p>{{ message.message }}</p>
                  </article>
                }
              </div>
            }
          </section>
        }
      }
    </section>
  `
})
export class BuyerSupportDetailPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly supportService = inject(BuyerSupportService);

  protected readonly ticket = signal<BuyerSupportTicketResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly messageForm = this.formBuilder.group({
    message: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadTicket();
  }

  protected async addMessage(): Promise<void> {
    if (this.messageForm.invalid || this.isSaving()) {
      this.messageForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const value = this.messageForm.getRawValue();
      this.ticket.set(await this.supportService.addMessage(this.ticketId(), { message: value.message }));
      this.messageForm.reset({ message: '' });
      this.successMessage.set('Message sent.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Open', 'WaitingForBuyer', 'Escalated'].includes(status)) {
      return 'warning';
    }

    if (['Resolved', 'Closed'].includes(status)) {
      return 'success';
    }

    return 'neutral';
  }

  private async loadTicket(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.ticket.set(await this.supportService.getTicket(this.ticketId()));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private ticketId(): string {
    return this.route.snapshot.paramMap.get('ticketId') ?? '';
  }
}
