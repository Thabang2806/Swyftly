import { DatePipe, DecimalPipe } from '@angular/common';
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
    DecimalPipe,
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
                <div><dt>Priority</dt><dd>{{ ticket()!.priority }}</dd></div>
                <div><dt>Assignee</dt><dd>{{ ticket()!.assignedSupportUserId ?? 'Unassigned' }}</dd></div>
                <div><dt>Escalation</dt><dd>{{ ticket()!.escalationReason ?? 'None' }}</dd></div>
                <div><dt>Buyer</dt><dd>{{ ticket()!.buyerId ?? 'None' }}</dd></div>
                <div><dt>Seller</dt><dd>{{ ticket()!.sellerId ?? 'None' }}</dd></div>
                <div><dt>Order</dt><dd>{{ ticket()!.linkedOrderId ?? 'None' }}</dd></div>
                <div><dt>Product</dt><dd>{{ ticket()!.linkedProductId ?? 'None' }}</dd></div>
                <div><dt>Payment</dt><dd>{{ ticket()!.linkedPaymentId ?? 'None' }}</dd></div>
              </dl>

              <div class="admin-finance-actions">
                <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="claimTicket()">Claim</button>
                <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="unclaimTicket()">Unclaim</button>
                <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="resolveTicket()">Resolve</button>
                <button mat-flat-button type="button" [disabled]="isSaving()" (click)="closeTicket()">Close</button>
              </div>
            </section>

            <aside class="admin-finance-action-panel">
              <h2>Operational triage</h2>
              <form [formGroup]="triageForm" (ngSubmit)="triageTicket()" class="admin-finance-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Priority</mat-label>
                  <input matInput formControlName="priority" placeholder="Normal, High, Urgent" />
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Internal note</mat-label>
                  <textarea matInput rows="3" formControlName="internalNote"></textarea>
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Save triage</button>
              </form>

              <h2>Escalation</h2>
              <form [formGroup]="escalationForm" (ngSubmit)="escalateTicket()" class="admin-finance-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Escalation reason</mat-label>
                  <textarea matInput rows="3" formControlName="reason"></textarea>
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Escalate</button>
              </form>

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

          @if (ticket()!.customerContext) {
            <section class="admin-finance-action-panel">
              <h2>Customer context</h2>
              <div class="admin-metric-grid">
                @if (ticket()!.customerContext!.buyer) {
                  <article class="admin-metric-card">
                    <span>Buyer</span>
                    <strong>{{ ticket()!.customerContext!.buyer!.displayName ?? ticket()!.customerContext!.buyer!.email ?? 'Buyer profile' }}</strong>
                    <small>{{ ticket()!.customerContext!.buyer!.buyerId }}</small>
                  </article>
                }
                @if (ticket()!.customerContext!.seller) {
                  <article class="admin-metric-card">
                    <span>Seller</span>
                    <strong>{{ ticket()!.customerContext!.seller!.displayName ?? 'Seller profile' }}</strong>
                    <small>{{ ticket()!.customerContext!.seller!.verificationStatus }}</small>
                    <a [routerLink]="ticket()!.customerContext!.seller!.adminRoute">Open seller</a>
                  </article>
                }
                @if (ticket()!.customerContext!.order) {
                  <article class="admin-metric-card">
                    <span>Order</span>
                    <strong>{{ ticket()!.customerContext!.order!.status }}</strong>
                    <small>{{ ticket()!.customerContext!.order!.totalAmount | number:'1.2-2' }}</small>
                    <a [routerLink]="ticket()!.customerContext!.order!.adminRoute">Open order</a>
                  </article>
                }
                @if (ticket()!.customerContext!.payment) {
                  <article class="admin-metric-card">
                    <span>Payment</span>
                    <strong>{{ ticket()!.customerContext!.payment!.status }}</strong>
                    <small>{{ ticket()!.customerContext!.payment!.provider }} {{ ticket()!.customerContext!.payment!.amount | number:'1.2-2' }} {{ ticket()!.customerContext!.payment!.currency }}</small>
                    <a [routerLink]="ticket()!.customerContext!.payment!.adminRoute">Open payment</a>
                  </article>
                }
                @if (ticket()!.customerContext!.product) {
                  <article class="admin-metric-card">
                    <span>Product</span>
                    <strong>{{ ticket()!.customerContext!.product!.title ?? 'Product' }}</strong>
                    <small>{{ ticket()!.customerContext!.product!.status }}</small>
                    <a [routerLink]="ticket()!.customerContext!.product!.adminRoute">Open product</a>
                  </article>
                }
              </div>
            </section>
          }

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

  protected readonly triageForm = this.formBuilder.group({
    priority: ['Normal', [Validators.required]],
    internalNote: ['']
  });

  protected readonly escalationForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
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

  protected async claimTicket(): Promise<void> {
    await this.runAction(() => this.supportService.claimTicket(this.ticketId()), 'Ticket claimed.');
  }

  protected async unclaimTicket(): Promise<void> {
    await this.runAction(() => this.supportService.unclaimTicket(this.ticketId()), 'Ticket unclaimed.');
  }

  protected async triageTicket(): Promise<void> {
    if (this.triageForm.invalid || this.isSaving()) {
      this.triageForm.markAllAsTouched();
      return;
    }

    const value = this.triageForm.getRawValue();
    await this.runAction(
      () => this.supportService.triageTicket(this.ticketId(), {
        priority: value.priority,
        internalNote: value.internalNote?.trim() || null
      }),
      'Triage saved.');
    this.triageForm.patchValue({ internalNote: '' });
  }

  protected async escalateTicket(): Promise<void> {
    if (this.escalationForm.invalid || this.isSaving()) {
      this.escalationForm.markAllAsTouched();
      return;
    }

    await this.runAction(
      () => this.supportService.escalateTicket(this.ticketId(), this.escalationForm.getRawValue()),
      'Ticket escalated.');
    this.escalationForm.reset({ reason: '' });
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
      const ticket = await this.supportService.getTicket(this.ticketId());
      this.ticket.set(ticket);
      this.triageForm.patchValue({ priority: ticket.priority || 'Normal' });
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
      const ticket = await action();
      this.ticket.set({
        ...ticket,
        customerContext: ticket.customerContext ?? this.ticket()?.customerContext ?? null
      });
      this.triageForm.patchValue({ priority: ticket.priority || 'Normal' });
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
