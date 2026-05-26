import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminSupportTicketResponse } from '../admin/admin-support.models';
import { AdminSupportService } from '../admin/admin-support.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-support-page',
  imports: [
    DatePipe,
    EmptyStateComponent,
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

      <a class="admin-back-link" routerLink="/admin">Back to dashboard</a>

      <app-page-header
        eyebrow="Support operations"
        heading="Support tickets"
        description="Review buyer and seller tickets, triage status, and open case detail for public replies or internal notes."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
          <a mat-stroked-button routerLink="/admin/disputes">Disputes</a>
        </div>
      </app-page-header>

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-support-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Search</mat-label>
          <input matInput formControlName="search" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <input matInput formControlName="status" placeholder="Open, Escalated, Resolved" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Category</mat-label>
          <input matInput formControlName="category" placeholder="OrderIssue, SellerIssue" />
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit">Apply filters</button>
          <button mat-stroked-button type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading support tickets...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (filteredTickets().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Support"
            heading="No support tickets found"
            message="Tickets from buyers and sellers will appear here when they need marketplace support."
          />
        } @else {
          <div class="admin-table admin-support-table" role="table" aria-label="Support tickets">
            <div class="admin-table-row heading admin-support-table-row" role="row">
              <span role="columnheader">Ticket</span>
              <span role="columnheader">Customer</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Opened</span>
              <span role="columnheader">Action</span>
            </div>

            @for (ticket of filteredTickets(); track ticket.supportTicketId) {
              <div class="admin-table-row admin-support-table-row" role="row">
                <span role="cell">
                  <strong>{{ ticket.subject }}</strong>
                  <small>{{ ticket.category }} - {{ ticket.supportTicketId }}</small>
                </span>
                <span role="cell">
                  <strong>{{ ticket.createdByRole }}</strong>
                  <small>{{ ownerSummary(ticket) }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="ticket.status" [tone]="statusTone(ticket.status)" />
                  <small>{{ ticket.messages.length }} message{{ ticket.messages.length === 1 ? '' : 's' }}</small>
                </span>
                <span role="cell">{{ ticket.openedAtUtc | date:'medium' }}</span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/admin/support', ticket.supportTicketId]">Open</a>
                </span>
              </div>
            }
          </div>

          <p class="audit-count">{{ filteredTickets().length }} of {{ tickets().length }} ticket{{ tickets().length === 1 ? '' : 's' }}</p>
        }
      }
    </section>
  `
})
export class AdminSupportPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly supportService = inject(AdminSupportService);

  protected readonly tickets = signal<AdminSupportTicketResponse[]>([]);
  protected readonly filters = signal({ search: '', status: '', category: '' });
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: [''],
    category: ['']
  });

  protected readonly filteredTickets = computed(() => {
    const { search, status, category } = this.filters();
    const normalizedSearch = search.trim().toLowerCase();
    const normalizedStatus = status.trim().toLowerCase();
    const normalizedCategory = category.trim().toLowerCase();

    return this.tickets().filter(ticket => {
      const matchesSearch = normalizedSearch.length === 0 ||
        [
          ticket.subject,
          ticket.description,
          ticket.supportTicketId,
          ticket.createdByRole,
          ticket.buyerId,
          ticket.sellerId
        ]
          .filter(Boolean)
          .join(' ')
          .toLowerCase()
          .includes(normalizedSearch);
      const matchesStatus = normalizedStatus.length === 0 || ticket.status.toLowerCase().includes(normalizedStatus);
      const matchesCategory = normalizedCategory.length === 0 || ticket.category.toLowerCase().includes(normalizedCategory);

      return matchesSearch && matchesStatus && matchesCategory;
    });
  });

  async ngOnInit(): Promise<void> {
    await this.loadTickets();
  }

  protected applyFilters(): void {
    this.filters.set(this.filtersForm.getRawValue());
  }

  protected clearFilters(): void {
    this.filtersForm.reset({ search: '', status: '', category: '' });
    this.applyFilters();
  }

  protected ownerSummary(ticket: AdminSupportTicketResponse): string {
    if (ticket.buyerId) {
      return `Buyer ${ticket.buyerId}`;
    }

    if (ticket.sellerId) {
      return `Seller ${ticket.sellerId}`;
    }

    return 'No linked profile';
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

  private async loadTickets(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.tickets.set(await this.supportService.listTickets());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.tickets.set([]);
    } finally {
      this.isLoading.set(false);
    }
  }
}
