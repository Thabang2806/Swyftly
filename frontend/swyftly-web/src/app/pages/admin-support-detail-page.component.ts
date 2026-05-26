import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminSupportTicketResponse } from '../admin/admin-support.models';
import { AdminSupportService } from '../admin/admin-support.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-support-detail-page',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    AdminWorkspaceNavComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-support-page">
      <app-admin-workspace-nav />

      <a class="admin-back-link" routerLink="/admin/support">Back to support tickets</a>

      <app-page-header
        eyebrow="Support case"
        [heading]="ticket()?.subject ?? 'Support ticket'"
        [description]="ticket()?.description ?? 'Review support ticket detail.'"
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
          <div class="admin-finance-layout">
            <section class="admin-finance-action-panel">
              <h2>Ticket context</h2>
              <dl class="seller-facts">
                <div><dt>Ticket id</dt><dd>{{ ticket()!.supportTicketId }}</dd></div>
                <div><dt>Created by</dt><dd>{{ ticket()!.createdByRole }}</dd></div>
                <div><dt>Category</dt><dd>{{ ticket()!.category }}</dd></div>
                <div><dt>Opened</dt><dd>{{ ticket()!.openedAtUtc | date:'medium' }}</dd></div>
                <div><dt>Buyer</dt><dd>{{ ticket()!.buyerId ?? 'None' }}</dd></div>
                <div><dt>Seller</dt><dd>{{ ticket()!.sellerId ?? 'None' }}</dd></div>
                <div><dt>Order</dt><dd>{{ ticket()!.linkedOrderId ?? 'None' }}</dd></div>
                <div><dt>Product</dt><dd>{{ ticket()!.linkedProductId ?? 'None' }}</dd></div>
                <div><dt>Payment</dt><dd>{{ ticket()!.linkedPaymentId ?? 'None' }}</dd></div>
              </dl>

              <div class="admin-finance-actions">
                <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="resolveTicket()">Resolve</button>
                <button mat-flat-button type="button" [disabled]="isSaving()" (click)="closeTicket()">Close</button>
              </div>
            </section>

            <aside class="admin-finance-action-panel">
              <h2>Add support response</h2>
              <form [formGroup]="publicMessageForm" (ngSubmit)="addPublicMessage()" class="admin-finance-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Public message</mat-label>
                  <textarea matInput rows="4" formControlName="message"></textarea>
                </mat-form-field>
                <button mat-flat-button type="submit" [disabled]="isSaving()">Send public reply</button>
              </form>

              <h2>Internal note</h2>
              <form [formGroup]="internalNoteForm" (ngSubmit)="addInternalNote()" class="admin-finance-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Internal note</mat-label>
                  <textarea matInput rows="4" formControlName="message"></textarea>
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Add internal note</button>
              </form>
            </aside>
          </div>

          <section class="admin-support-messages">
            <h2>Conversation and notes</h2>
            @for (message of ticket()!.messages; track message.supportMessageId) {
              <article class="admin-support-message" [class.internal]="message.isInternal">
                <div>
                  <strong>{{ message.senderRole }}</strong>
                  <span>{{ message.createdAtUtc | date:'medium' }}</span>
                  @if (message.isInternal) {
                    <app-status-badge label="Internal" tone="warning" />
                  }
                </div>
                <p>{{ message.message }}</p>
              </article>
            }
          </section>
        }
      }
    </section>
  `
})
export class AdminSupportDetailPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly supportService = inject(AdminSupportService);

  protected readonly ticket = signal<AdminSupportTicketResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly publicMessageForm = this.formBuilder.group({
    message: ['', [Validators.required]]
  });

  protected readonly internalNoteForm = this.formBuilder.group({
    message: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadTicket();
  }

  protected async addPublicMessage(): Promise<void> {
    if (this.publicMessageForm.invalid || this.isSaving()) {
      this.publicMessageForm.markAllAsTouched();
      return;
    }

    await this.runAction(
      () => this.supportService.addPublicMessage(this.ticketId(), this.publicMessageForm.getRawValue()),
      'Public reply sent.');
    this.publicMessageForm.reset({ message: '' });
  }

  protected async addInternalNote(): Promise<void> {
    if (this.internalNoteForm.invalid || this.isSaving()) {
      this.internalNoteForm.markAllAsTouched();
      return;
    }

    await this.runAction(
      () => this.supportService.addInternalNote(this.ticketId(), this.internalNoteForm.getRawValue()),
      'Internal note added.');
    this.internalNoteForm.reset({ message: '' });
  }

  protected async resolveTicket(): Promise<void> {
    await this.runAction(() => this.supportService.resolveTicket(this.ticketId()), 'Ticket resolved.');
  }

  protected async closeTicket(): Promise<void> {
    await this.runAction(() => this.supportService.closeTicket(this.ticketId()), 'Ticket closed.');
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Open', 'WaitingForCustomer', 'WaitingForSeller', 'Escalated'].includes(status)) {
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

  private async runAction(
    action: () => Promise<AdminSupportTicketResponse>,
    successMessage: string
  ): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.ticket.set(await action());
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private ticketId(): string {
    return this.route.snapshot.paramMap.get('ticketId') ?? '';
  }
}
