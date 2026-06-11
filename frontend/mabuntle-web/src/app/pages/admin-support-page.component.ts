import { DOCUMENT, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminQueueSavedViewResponse } from '../admin/admin-moderation-queue.models';
import {
  AdminSupportQualityBreakdownResponse,
  AdminSupportQualityReportResponse,
  AdminSupportQueueItemResponse,
  AdminSupportQueueResponse,
  AdminSupportSummaryResponse
} from '../admin/admin-support.models';
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

      <a class="admin-back-link" routerLink="">Back to dashboard</a>

      <app-page-header
        eyebrow="Support operations"
        heading="Support tickets"
        description="Review buyer and seller tickets, triage status, and open case detail for public replies or internal notes."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/audit-logs">Audit logs</a>
          <a data-ui-button="secondary" routerLink="/disputes">Disputes</a>
        </div>
      </app-page-header>

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-support-filters" novalidate>
        <label class="ui-field">
          <span>Saved view</span>
          <select formControlName="savedViewId" (change)="applySavedView($any($event.target).value)">
            <option value="">Current filters</option>
            @for (view of savedViews(); track view.viewId) {
              <option [value]="view.viewId">{{ view.name }}{{ view.isDefault ? ' (default)' : '' }}</option>
            }
          </select>
        </label>

        <label class="ui-field">
          <span>View name</span>
          <input formControlName="savedViewName" placeholder="Overdue checkout tickets" />
        </label>

        <label class="ui-field">
          <span>Queue view</span>
          <select formControlName="view">
            <option value="NeedsAttention">Needs attention</option>
            <option value="All">All tickets</option>
          </select>
        </label>

        <label class="ui-field">
          <span>Search</span>
          <input formControlName="search" />
        </label>

        <label class="ui-field">
          <span>Status</span>
          <input formControlName="status" placeholder="Open, Escalated, Resolved" />
        </label>

        <label class="ui-field">
          <span>Category</span>
          <input formControlName="category" placeholder="OrderIssue, SellerIssue" />
        </label>

        <label class="ui-field">
          <span>Assigned</span>
          <input formControlName="assigned" placeholder="Any, Mine, Unassigned" />
        </label>

        <label class="ui-field">
          <span>Priority</span>
          <input formControlName="priority" placeholder="Normal, High, Urgent" />
        </label>

        <label class="ui-field">
          <span>SLA</span>
          <input formControlName="sla" placeholder="OnTrack, DueSoon, Overdue" />
        </label>

        <div class="admin-audit-actions">
          <button data-ui-button="primary" type="submit">Apply filters</button>
          <button data-ui-button="secondary" type="button" (click)="clearFilters()">Clear</button>
          <button data-ui-button="secondary" type="button" [disabled]="isSavingView()" (click)="saveView(false)">Save view</button>
          <button data-ui-button="secondary" type="button" [disabled]="!filtersForm.controls.savedViewId.value || isSavingView()" (click)="saveView(true)">Update view</button>
          <button data-ui-button="secondary" type="button" [disabled]="!filtersForm.controls.savedViewId.value || isSavingView()" (click)="makeDefaultView()">Make default</button>
          <button data-ui-button="secondary" type="button" [disabled]="!filtersForm.controls.savedViewId.value || isSavingView()" (click)="deleteView()">Delete view</button>
          <button data-ui-button="secondary" type="button" [disabled]="isExporting()" (click)="exportQueue()">Export CSV</button>
        </div>
      </form>

      @if (summary()) {
        <div class="admin-metric-grid">
          <article class="admin-metric-card">
            <span>Open tickets</span>
            <strong>{{ summary()!.openTicketCount }}</strong>
            <small>Active buyer and seller cases.</small>
          </article>
          <article class="admin-metric-card">
            <span>Escalated</span>
            <strong>{{ summary()!.escalatedTicketCount }}</strong>
            <small>Senior review context required.</small>
          </article>
          <article class="admin-metric-card">
            <span>Overdue SLA</span>
            <strong>{{ summary()!.overdueTicketCount }}</strong>
            <small>Operational guidance only.</small>
          </article>
          <article class="admin-metric-card">
            <span>Mine</span>
            <strong>{{ summary()!.myOpenTicketCount }}</strong>
            <small>Open tickets assigned to you.</small>
          </article>
          <article class="admin-metric-card">
            <span>Unassigned</span>
            <strong>{{ summary()!.unassignedOpenTicketCount }}</strong>
            <small>Open tickets waiting to be claimed.</small>
          </article>
          <article class="admin-metric-card">
            <span>Resolved today</span>
            <strong>{{ summary()!.resolvedTodayCount }}</strong>
            <small>{{ summary()!.resolvedLast7DaysCount }} resolved in 7 days.</small>
          </article>
          <article class="admin-metric-card">
            <span>First response</span>
            <strong>{{ renderHours(summary()!.averageFirstResponseHours) }}</strong>
            <small>Average public support response.</small>
          </article>
          <article class="admin-metric-card">
            <span>Resolution time</span>
            <strong>{{ renderHours(summary()!.averageResolutionHours) }}</strong>
            <small>Average time to resolve.</small>
          </article>
        </div>
      }

      <section class="route-card admin-support-quality-panel" aria-labelledby="support-quality-heading">
        <div class="admin-section-heading">
          <div>
            <span class="eyebrow">Quality</span>
            <h2 id="support-quality-heading">SLA outcome reporting</h2>
            <p>Read-only support performance context. Targets do not automate escalation, assignment, refunds, or ticket closure.</p>
          </div>
          <button data-ui-button="secondary" type="button" [disabled]="isExportingQuality()" (click)="exportQualityReport()">Export quality CSV</button>
        </div>

        <form [formGroup]="qualityForm" (ngSubmit)="applyQualityFilters()" class="admin-support-filters" novalidate>
          <label class="ui-field">
            <span>From UTC</span>
            <input formControlName="fromUtc" placeholder="2026-05-01T00:00:00Z" />
          </label>
          <label class="ui-field">
            <span>To UTC</span>
            <input formControlName="toUtc" placeholder="2026-05-31T23:59:59Z" />
          </label>
          <label class="ui-field">
            <span>Bucket</span>
            <select formControlName="bucket">
              <option value="Day">Day</option>
              <option value="Week">Week</option>
            </select>
          </label>
          <label class="ui-field">
            <span>Category</span>
            <input formControlName="category" placeholder="OrderIssue" />
          </label>
          <label class="ui-field">
            <span>Priority</span>
            <input formControlName="priority" placeholder="High" />
          </label>
          <label class="ui-field">
            <span>Assignee ID</span>
            <input formControlName="assignedSupportUserId" />
          </label>
          <label class="ui-field">
            <span>Requester role</span>
            <select formControlName="createdByRole">
              <option value="">Any</option>
              <option value="Buyer">Buyer</option>
              <option value="Seller">Seller</option>
              <option value="SupportAgent">Support agent</option>
              <option value="Admin">Admin</option>
              <option value="SuperAdmin">Super admin</option>
            </select>
          </label>
          <div class="admin-audit-actions">
            <button data-ui-button="primary" type="submit">Apply quality filters</button>
            <button data-ui-button="secondary" type="button" (click)="clearQualityFilters()">Clear quality filters</button>
          </div>
        </form>

        @if (isQualityLoading()) {
          <p>Loading support quality report...</p>
        } @else if (qualityReport()) {
          <div class="admin-metric-grid">
            <article class="admin-metric-card">
              <span>Created</span>
              <strong>{{ qualityReport()!.summary.createdCount }}</strong>
              <small>{{ qualityReport()!.summary.resolvedCount }} resolved in range.</small>
            </article>
            <article class="admin-metric-card">
              <span>First response</span>
              <strong>{{ renderHours(qualityReport()!.summary.averageFirstResponseHours) }}</strong>
              <small>{{ qualityReport()!.summary.firstResponseTargetMissedCount }} missed target.</small>
            </article>
            <article class="admin-metric-card">
              <span>Resolution</span>
              <strong>{{ renderHours(qualityReport()!.summary.averageResolutionHours) }}</strong>
              <small>{{ qualityReport()!.summary.resolutionTargetMissedCount }} missed target.</small>
            </article>
            <article class="admin-metric-card">
              <span>Escalated</span>
              <strong>{{ qualityReport()!.summary.escalatedCount }}</strong>
              <small>{{ qualityReport()!.summary.currentlyOverdueCount }} currently overdue.</small>
            </article>
          </div>

          @if (qualityReport()!.summary.createdCount === 0) {
            <app-empty-state
              eyebrow="Quality"
              heading="No support activity in this range"
              message="Broaden the date range or remove filters to review support quality outcomes."
            />
          } @else {
            <div class="admin-support-quality-grid">
              <div>
                <h3>Category outcomes</h3>
                <div class="admin-table compact" role="table" aria-label="Support quality by category">
                  @for (row of qualityReport()!.categoryBreakdown; track row.key) {
                    <div class="admin-table-row" role="row">
                      <span role="cell"><strong>{{ row.key }}</strong></span>
                      <span role="cell">{{ row.createdCount }} created</span>
                      <span role="cell">{{ row.resolvedCount }} resolved</span>
                      <span role="cell">{{ row.firstResponseTargetMissedCount }} first-response misses</span>
                    </div>
                  }
                </div>
              </div>

              <div>
                <h3>Assignee workload</h3>
                <div class="admin-table compact" role="table" aria-label="Support quality by assignee">
                  @for (row of qualityReport()!.assigneeBreakdown; track row.assignedSupportUserId) {
                    <div class="admin-table-row" role="row">
                      <span role="cell"><strong>{{ row.assignedSupportDisplayName ?? row.assignedSupportUserId }}</strong></span>
                      <span role="cell">{{ row.createdCount }} created</span>
                      <span role="cell">{{ renderHours(row.averageFirstResponseHours) }} first response</span>
                      <span role="cell">{{ row.resolutionTargetMissedCount }} resolution misses</span>
                    </div>
                  }
                </div>
              </div>
            </div>

            <div class="admin-support-trend">
              <h3>{{ qualityReport()!.bucket }} trend</h3>
              @for (row of qualityReport()!.trend; track row.bucketStartUtc) {
                <div class="support-trend-row">
                  <span>{{ row.bucketStartUtc | date:'mediumDate' }}</span>
                  <strong>{{ row.createdCount }} created</strong>
                  <small>{{ row.resolvedCount }} resolved / {{ row.escalatedCount }} escalated</small>
                </div>
              }
            </div>
          }
        }
      </section>

      @if (isLoading()) {
        <div class="route-card">Loading support tickets...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (queue()?.items?.length === 0 && !errorMessage()) {
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
              <span role="columnheader">Triage</span>
              <span role="columnheader">Opened</span>
              <span role="columnheader">Action</span>
            </div>

            @for (ticket of queue()?.items ?? []; track ticket.supportTicketId) {
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
                  <small>{{ ticket.messageCount }} message{{ ticket.messageCount === 1 ? '' : 's' }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="ticket.priority" [tone]="priorityTone(ticket.priority)" />
                  <app-status-badge [label]="ticket.slaStatus" [tone]="slaTone(ticket.slaStatus)" />
                  <small>{{ assignmentSummary(ticket) }}</small>
                </span>
                <span role="cell">
                  {{ ticket.openedAtUtc | date:'medium' }}
                  <small>{{ ticket.ageHours }}h old</small>
                </span>
                <span role="cell">
                  <button data-ui-button="secondary" type="button" [disabled]="isSaving() === ticket.supportTicketId" (click)="claimTicket(ticket.supportTicketId)">Claim</button>
                  <a data-ui-button="secondary" [routerLink]="['/support', ticket.supportTicketId]">Open</a>
                </span>
              </div>
            }
          </div>

          <p class="audit-count">{{ queue()?.items?.length ?? 0 }} of {{ queue()?.totalCount ?? 0 }} ticket{{ queue()?.totalCount === 1 ? '' : 's' }}</p>
        }
      }
    </section>
  `
})
export class AdminSupportPageComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly supportService = inject(AdminSupportService);

  protected readonly queue = signal<AdminSupportQueueResponse | null>(null);
  protected readonly summary = signal<AdminSupportSummaryResponse | null>(null);
  protected readonly qualityReport = signal<AdminSupportQualityReportResponse | null>(null);
  protected readonly savedViews = signal<AdminQueueSavedViewResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly isQualityLoading = signal(true);
  protected readonly isSaving = signal<string | null>(null);
  protected readonly isSavingView = signal(false);
  protected readonly isExporting = signal(false);
  protected readonly isExportingQuality = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    savedViewId: [''],
    savedViewName: [''],
    view: ['NeedsAttention'],
    search: [''],
    status: [''],
    category: [''],
    assigned: ['Any'],
    priority: [''],
    sla: ['']
  });

  protected readonly qualityForm = this.formBuilder.group({
    fromUtc: [''],
    toUtc: [''],
    bucket: ['Day'],
    category: [''],
    priority: [''],
    assignedSupportUserId: [''],
    createdByRole: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadSavedViews();
    await Promise.all([
      this.loadTickets(),
      this.loadQualityReport()
    ]);
  }

  protected async applyFilters(): Promise<void> {
    await this.loadTickets();
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset({ savedViewId: '', savedViewName: '', view: 'NeedsAttention', search: '', status: '', category: '', assigned: 'Any', priority: '', sla: '' });
    await this.applyFilters();
  }

  protected async applyQualityFilters(): Promise<void> {
    await this.loadQualityReport();
  }

  protected async clearQualityFilters(): Promise<void> {
    this.qualityForm.reset({ fromUtc: '', toUtc: '', bucket: 'Day', category: '', priority: '', assignedSupportUserId: '', createdByRole: '' });
    await this.loadQualityReport();
  }

  protected async applySavedView(viewId: string): Promise<void> {
    if (!viewId) {
      this.filtersForm.patchValue({ savedViewName: '' });
      await this.applyFilters();
      return;
    }

    const view = this.savedViews().find(item => item.viewId === viewId);
    if (!view) {
      return;
    }

    this.filtersForm.patchValue({
      savedViewId: view.viewId,
      savedViewName: view.name,
      view: view.filters.view ?? 'NeedsAttention',
      search: view.filters.search ?? '',
      status: view.filters.status ?? '',
      category: view.filters.category ?? '',
      assigned: view.filters.assigned ?? 'Any',
      priority: view.filters.priority ?? '',
      sla: view.filters.sla ?? ''
    });
    await this.applyFilters();
  }

  protected async saveView(updateExisting: boolean): Promise<void> {
    const name = this.filtersForm.controls.savedViewName.value.trim();
    if (!name) {
      this.errorMessage.set('Enter a saved view name before saving.');
      return;
    }

    const viewId = this.filtersForm.controls.savedViewId.value;
    this.isSavingView.set(true);
    this.errorMessage.set(null);

    try {
      const filters = this.filtersForm.getRawValue();
      const request = {
        queue: 'Support',
        name,
        filters: {
          view: filters.view || 'NeedsAttention',
          status: filters.status || null,
          category: filters.category || null,
          search: filters.search || null,
          assigned: filters.assigned || 'Any',
          priority: filters.priority || null,
          sla: filters.sla || null,
          sort: 'PriorityDesc',
          pageSize: 25
        }
      };
      const response = updateExisting && viewId
        ? await this.supportService.updateSavedView(viewId, request)
        : await this.supportService.createSavedView(request);
      await this.loadSavedViews();
      this.filtersForm.patchValue({ savedViewId: response.viewId, savedViewName: response.name });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingView.set(false);
    }
  }

  protected async makeDefaultView(): Promise<void> {
    const viewId = this.filtersForm.controls.savedViewId.value;
    if (!viewId) {
      return;
    }

    this.isSavingView.set(true);
    this.errorMessage.set(null);
    try {
      await this.supportService.makeDefault(viewId);
      await this.loadSavedViews();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingView.set(false);
    }
  }

  protected async deleteView(): Promise<void> {
    const viewId = this.filtersForm.controls.savedViewId.value;
    if (!viewId) {
      return;
    }

    this.isSavingView.set(true);
    this.errorMessage.set(null);
    try {
      await this.supportService.deleteSavedView(viewId);
      await this.loadSavedViews();
      this.filtersForm.patchValue({ savedViewId: '', savedViewName: '' });
      await this.applyFilters();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingView.set(false);
    }
  }

  protected async exportQueue(): Promise<void> {
    this.isExporting.set(true);
    this.errorMessage.set(null);
    try {
      const blob = await this.supportService.exportQueue(this.buildQueueFilters());
      const url = URL.createObjectURL(blob);
      const anchor = this.document.createElement('a');
      anchor.href = url;
      anchor.download = `support-queue-${new Date().toISOString().slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isExporting.set(false);
    }
  }

  protected async exportQualityReport(): Promise<void> {
    this.isExportingQuality.set(true);
    this.errorMessage.set(null);
    try {
      const blob = await this.supportService.exportQualityReport(this.buildQualityFilters());
      const url = URL.createObjectURL(blob);
      const anchor = this.document.createElement('a');
      anchor.href = url;
      anchor.download = `support-quality-${new Date().toISOString().slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isExportingQuality.set(false);
    }
  }

  protected ownerSummary(ticket: AdminSupportQueueItemResponse): string {
    if (ticket.buyerId) {
      return `Buyer ${ticket.buyerId}`;
    }

    if (ticket.sellerId) {
      return `Seller ${ticket.sellerId}`;
    }

    return 'No linked profile';
  }

  protected assignmentSummary(ticket: AdminSupportQueueItemResponse): string {
    return ticket.assignedSupportDisplayName ?? (ticket.assignedSupportUserId ? `Assigned ${ticket.assignedSupportUserId}` : 'Unassigned');
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

  protected priorityTone(priority: string): StatusBadgeTone {
    if (priority === 'Urgent') {
      return 'danger';
    }

    if (priority === 'High') {
      return 'warning';
    }

    return 'neutral';
  }

  protected slaTone(sla: string): StatusBadgeTone {
    if (sla === 'Overdue') {
      return 'danger';
    }

    if (sla === 'DueSoon') {
      return 'warning';
    }

    return 'success';
  }

  protected renderHours(value: number | null): string {
    return value === null || value === undefined ? 'n/a' : `${value.toFixed(1)}h`;
  }

  protected missedTone(row: AdminSupportQualityBreakdownResponse): StatusBadgeTone {
    return row.firstResponseTargetMissedCount > 0 || row.resolutionTargetMissedCount > 0 ? 'warning' : 'success';
  }

  protected async claimTicket(ticketId: string): Promise<void> {
    this.isSaving.set(ticketId);
    this.errorMessage.set(null);

    try {
      await this.supportService.claimTicket(ticketId);
      await this.loadTickets(false);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(null);
    }
  }

  private async loadTickets(showLoader = true): Promise<void> {
    if (showLoader) {
      this.isLoading.set(true);
    }
    this.errorMessage.set(null);

    try {
      const [queue, summary] = await Promise.all([
        this.supportService.listQueue(this.buildQueueFilters()),
        this.supportService.getSummary()
      ]);
      this.queue.set(queue);
      this.summary.set(summary);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.queue.set(null);
    } finally {
      if (showLoader) {
        this.isLoading.set(false);
      }
    }
  }

  private async loadSavedViews(): Promise<void> {
    try {
      const views = await this.supportService.getSavedViews();
      this.savedViews.set(views);
      const defaultView = views.find(view => view.isDefault);
      if (defaultView && !this.filtersForm.controls.savedViewId.value) {
        await this.applySavedView(defaultView.viewId);
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    }
  }

  private async loadQualityReport(): Promise<void> {
    this.isQualityLoading.set(true);
    this.errorMessage.set(null);
    try {
      this.qualityReport.set(await this.supportService.getQualityReport(this.buildQualityFilters()));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.qualityReport.set(null);
    } finally {
      this.isQualityLoading.set(false);
    }
  }

  private buildQueueFilters(): {
    view: 'NeedsAttention' | 'All';
    search: string;
    status: string;
    category: string;
    assigned: 'Any' | 'Mine' | 'Unassigned';
    priority: string;
    sla: string;
    savedViewId: string;
    page: number;
    pageSize: number;
    sort: string;
  } {
    const filters = this.filtersForm.getRawValue();
    return {
      view: filters.view as 'NeedsAttention' | 'All',
      search: filters.search,
      status: filters.status,
      category: filters.category,
      assigned: filters.assigned as 'Any' | 'Mine' | 'Unassigned',
      priority: filters.priority,
      sla: filters.sla,
      savedViewId: filters.savedViewId,
      page: 1,
      pageSize: 25,
      sort: 'PriorityDesc'
    };
  }

  private buildQualityFilters(): {
    fromUtc: string;
    toUtc: string;
    bucket: 'Day' | 'Week';
    category: string;
    priority: string;
    assignedSupportUserId: string;
    createdByRole: string;
  } {
    const filters = this.qualityForm.getRawValue();
    return {
      fromUtc: filters.fromUtc,
      toUtc: filters.toUtc,
      bucket: filters.bucket as 'Day' | 'Week',
      category: filters.category,
      priority: filters.priority,
      assignedSupportUserId: filters.assignedSupportUserId,
      createdByRole: filters.createdByRole
    };
  }
}
