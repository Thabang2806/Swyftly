import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerSupportTicketCategory, SellerSupportTicketResponse } from '../seller/seller-support.models';
import { SellerSupportService } from '../seller/seller-support.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-support-page',
  imports: [
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page">
      <app-seller-workspace-nav />

      <app-page-header
        eyebrow="Seller support"
        heading="Support tickets"
        description="Create and track support requests for orders, products, returns, and seller operations."
      />

      @if (isLoading()) {
        <div class="route-card">Loading support tickets...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <div class="seller-detail-grid">
          <section class="seller-panel">
            <h2>Create ticket</h2>
            <form [formGroup]="ticketForm" (ngSubmit)="createTicket()" class="seller-form-grid" novalidate>
              <label class="ui-field">
                <span>Category</span>
                <select formControlName="category">
                  @for (category of categories; track category) {
                    <option [value]="category">{{ category }}</option>
                  }
                </select>
              </label>

              <label class="ui-field">
                <span>Subject</span>
                <input formControlName="subject" />
              </label>

              <label class="ui-field">
                <span>Description</span>
                <textarea rows="4" formControlName="description"></textarea>
              </label>

              <div class="form-grid">
                <label class="ui-field">
                  <span>Linked order ID</span>
                  <input formControlName="linkedOrderId" />
                </label>

                <label class="ui-field">
                  <span>Linked product ID</span>
                  <input formControlName="linkedProductId" />
                </label>
              </div>

              <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Create ticket</button>
            </form>
          </section>

          <section class="seller-panel">
            <h2>Support path</h2>
            <p>Seller tickets are visible to marketplace support. Private internal notes are not exposed on seller responses.</p>
            <div class="seller-result-steps">
              <div>
                <strong>Open</strong>
                <span>Create a ticket with clear order or product context.</span>
              </div>
              <div>
                <strong>Respond</strong>
                <span>Add seller-side messages as support asks for details.</span>
              </div>
              <div>
                <strong>Resolve</strong>
                <span>Resolved and closed states are managed by support staff.</span>
              </div>
            </div>
          </section>
        </div>

        @if (tickets().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Support"
            heading="No support tickets"
            message="Create a ticket when seller support needs marketplace visibility."
          />
        } @else {
          <div class="admin-table seller-ops-table" role="table" aria-label="Seller support tickets">
            <div class="admin-table-row heading seller-ops-table-row" role="row">
              <span role="columnheader">Ticket</span>
              <span role="columnheader">Category</span>
              <span role="columnheader">Opened</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (ticket of tickets(); track ticket.supportTicketId) {
              <div class="admin-table-row seller-ops-table-row" role="row">
                <span role="cell">
                  <strong>{{ ticket.subject }}</strong>
                  <small>{{ ticket.description }}</small>
                </span>
                <span role="cell">{{ ticket.category }}</span>
                <span role="cell">{{ ticket.openedAtUtc | date:'medium' }}</span>
                <span role="cell">
                  <app-status-badge [label]="ticket.status" [tone]="statusTone(ticket.status)" />
                  <small>{{ ticket.messages.length }} message{{ ticket.messages.length === 1 ? '' : 's' }}</small>
                </span>
                <span role="cell">
                  <a data-ui-button="secondary" [routerLink]="['/support', ticket.supportTicketId]">Open</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class SellerSupportPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly supportService = inject(SellerSupportService);

  protected readonly categories: readonly SellerSupportTicketCategory[] = [
    'OrderIssue',
    'PaymentIssue',
    'ReturnIssue',
    'SellerIssue',
    'ProductIssue',
    'TechnicalIssue',
    'Other'
  ];

  protected readonly tickets = signal<SellerSupportTicketResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly ticketForm = this.formBuilder.group({
    category: ['OrderIssue' as SellerSupportTicketCategory, [Validators.required]],
    subject: ['', [Validators.required]],
    description: ['', [Validators.required]],
    linkedOrderId: [''],
    linkedProductId: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadTickets();
  }

  protected async createTicket(): Promise<void> {
    if (this.ticketForm.invalid || this.isSaving()) {
      this.ticketForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const value = this.ticketForm.getRawValue();

    try {
      const ticket = await this.supportService.createTicket({
        category: value.category,
        subject: value.subject,
        description: value.description,
        linkedOrderId: emptyToNull(value.linkedOrderId),
        linkedProductId: emptyToNull(value.linkedProductId),
        linkedSellerId: null,
        linkedPaymentId: null
      });
      this.tickets.set([ticket, ...this.tickets()]);
      this.ticketForm.reset({
        category: 'OrderIssue',
        subject: '',
        description: '',
        linkedOrderId: '',
        linkedProductId: ''
      });
      this.successMessage.set('Support ticket created.');
      await this.router.navigate(['/support', ticket.supportTicketId]);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Open', 'WaitingForSeller', 'Escalated'].includes(status)) {
      return 'warning';
    }

    if (['Resolved', 'Closed'].includes(status)) {
      return 'success';
    }

    return 'neutral';
  }

  private async loadTickets(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.tickets.set(await this.supportService.listTickets());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
