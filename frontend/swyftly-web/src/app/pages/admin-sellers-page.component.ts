import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AdminModerationQueueService } from '../admin/admin-moderation-queue.service';
import { AdminQueueSavedViewResponse, AdminQueueSummaryResponse } from '../admin/admin-moderation-queue.models';
import { AdminOperationalView, AdminStatusCountResponse } from '../admin/admin-operational-list.models';
import { AdminQueueTriagePanelComponent } from '../admin/admin-queue-triage-panel.component';
import { AdminQueueTriageService } from '../admin/admin-queue-triage.service';
import { AdminSellerOperationalSummaryResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { MetricTileComponent } from '../shared/ui/metric-tile.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';
import { WorkspaceShellComponent } from '../shared/ui/workspace-shell.component';

@Component({
  selector: 'app-admin-sellers-page',
  imports: [
    AdminQueueTriagePanelComponent,
    AdminWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MetricTileComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent,
    WorkspaceShellComponent
  ],
  template: `
    <section class="page admin-review hf-admin-console">
      <app-workspace-shell>
        <app-admin-workspace-nav workspaceNav />

        <app-page-header
          eyebrow="Admin console"
          heading="Seller operations"
          description="Review pending sellers and inspect verified, rejected, or suspended seller accounts without leaving the admin console."
        >
          <div pageHeaderActions>
            <a mat-stroked-button routerLink="/admin/products">Product queue</a>
            <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
          </div>
        </app-page-header>

        <div class="hf-metric-grid">
          @for (metric of sellerMetrics(); track metric.label) {
            <app-metric-tile [label]="metric.label" [value]="metric.value" [badge]="metric.badge" [badgeTone]="metric.tone" />
          }
        </div>

        @if (queueSummary()) {
          <div class="admin-audit-actions">
            <app-status-badge [label]="slaCountLabel('Overdue')" [tone]="slaTone('Overdue')" />
            <app-status-badge [label]="slaCountLabel('DueSoon')" [tone]="slaTone('DueSoon')" />
            <span class="audit-count">Reviewed today: {{ queueSummary()!.reviewedToday }} / Last 7 days: {{ queueSummary()!.reviewedLast7Days }}</span>
          </div>
        }

        <div class="admin-audit-actions">
          <button mat-flat-button type="button" [disabled]="view() === 'NeedsAttention'" (click)="setView('NeedsAttention')">Needs attention</button>
          <button mat-stroked-button type="button" [disabled]="view() === 'All'" (click)="setView('All')">All sellers</button>
        </div>

        <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-moderation-filters" novalidate>
          <mat-form-field appearance="outline">
            <mat-label>Search sellers</mat-label>
            <input matInput formControlName="search" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <input matInput formControlName="status" placeholder="UnderReview, Verified, Rejected" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Seller ID</mat-label>
            <input matInput formControlName="sellerId" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Assignment</mat-label>
            <mat-select formControlName="assigned">
              <mat-option value="Any">Any</mat-option>
              <mat-option value="Mine">Mine</mat-option>
              <mat-option value="Unassigned">Unassigned</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority">
              <mat-option value="">Any</mat-option>
              <mat-option value="Normal">Normal</mat-option>
              <mat-option value="High">High</mat-option>
              <mat-option value="Urgent">Urgent</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>SLA</mat-label>
            <mat-select formControlName="sla">
              <mat-option value="">Any</mat-option>
              <mat-option value="OnTrack">On track</mat-option>
              <mat-option value="DueSoon">Due soon</mat-option>
              <mat-option value="Overdue">Overdue</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Notes</mat-label>
            <mat-select formControlName="hasNotes">
              <mat-option value="">Any</mat-option>
              <mat-option value="true">Has notes</mat-option>
              <mat-option value="false">No notes</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Saved view</mat-label>
            <mat-select formControlName="savedViewId" (selectionChange)="applySavedView($event.value)">
              <mat-option value="">Manual filters</mat-option>
              @for (view of savedViews(); track view.viewId) {
                <mat-option [value]="view.viewId">{{ view.name }}{{ view.isDefault ? ' / default' : '' }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>View name</mat-label>
            <input matInput formControlName="savedViewName" />
          </mat-form-field>

          <div class="admin-audit-actions">
            <button mat-flat-button type="submit">Apply filters</button>
            <button mat-stroked-button type="button" (click)="clearFilters()">Clear</button>
            <button mat-stroked-button type="button" [disabled]="isSavingView()" (click)="saveView()">Save view</button>
            <button mat-stroked-button type="button" [disabled]="!filtersForm.controls.savedViewId.value || isSavingView()" (click)="updateView()">Update view</button>
            <button mat-stroked-button type="button" [disabled]="!filtersForm.controls.savedViewId.value || isSavingView()" (click)="makeDefaultView()">Make default</button>
            <button mat-stroked-button type="button" [disabled]="!filtersForm.controls.savedViewId.value || isSavingView()" (click)="deleteView()">Delete view</button>
          </div>
        </form>

        @if (statusCounts().length > 0) {
          <div class="admin-audit-actions">
            @for (count of statusCounts(); track count.status) {
              <button mat-stroked-button type="button" (click)="filterByStatus(count.status)">
                {{ count.status }} ({{ count.count }})
              </button>
            }
          </div>
        }

        @if (isLoading()) {
          <div class="route-card">Loading sellers...</div>
        } @else {
          @if (errorMessage()) {
            <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
          }

          @if (sellers().length === 0 && !errorMessage()) {
            <app-empty-state
              eyebrow="Clear"
              heading="No sellers found"
              message="Adjust the view or filters to inspect seller records beyond the needs-attention queue."
            />
          } @else {
            <div class="hf-admin-review-layout">
              <div class="hf-admin-queue-card">
                <div class="hf-admin-card-heading">
                  <div>
                    <span>{{ view() === 'NeedsAttention' ? 'Needs attention' : 'All-state view' }}</span>
                    <h2>Seller records</h2>
                  </div>
                  <app-status-badge [label]="totalCount() + ' total'" tone="accent" />
                </div>

                <div class="admin-audit-actions">
                  <button mat-stroked-button type="button" [disabled]="selectedQueueItemIds().length === 0 || isBulkSaving()" (click)="bulkClaim()">Claim selected</button>
                  <button mat-stroked-button type="button" [disabled]="selectedQueueItemIds().length === 0 || isBulkSaving()" (click)="bulkPriority('High')">Mark high</button>
                  <span class="audit-count">{{ selectedQueueItemIds().length }} selected</span>
                </div>

                <div class="admin-table admin-moderation-table" role="table" aria-label="Seller operational queue">
                  <div class="admin-table-row heading admin-moderation-table-row" role="row">
                    <span role="columnheader">Seller</span>
                    <span role="columnheader">Storefront</span>
                    <span role="columnheader">Submitted</span>
                    <span role="columnheader">Status</span>
                    <span role="columnheader">Action</span>
                  </div>

                  @for (seller of sellers(); track seller.sellerId) {
                    <div
                      class="admin-table-row admin-moderation-table-row hf-admin-select-row"
                      role="row"
                      [class.active]="selectedSeller()?.sellerId === seller.sellerId"
                      (click)="selectSeller(seller)"
                    >
                      <span role="cell">
                        <mat-checkbox
                          [checked]="isQueueItemSelected(seller.sellerId)"
                          (click)="$event.stopPropagation()"
                          (change)="toggleQueueItem(seller.sellerId, $event.checked)"
                        />
                        <strong>{{ seller.displayName ?? 'Unnamed seller' }}</strong>
                        <small>{{ seller.contactEmail ?? 'No contact email' }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ seller.storeName ?? 'No storefront' }}</strong>
                        <small>{{ seller.storeSlug ?? seller.sellerId }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ seller.submittedAtUtc ? (seller.submittedAtUtc | date:'mediumDate') : 'Not submitted' }}</strong>
                        <small>Updated {{ seller.updatedAtUtc | date:'short' }}</small>
                      </span>
                      <span role="cell">
                        <app-status-badge [label]="seller.verificationStatus" [tone]="sellerStatusTone(seller.verificationStatus)" />
                        <app-status-badge [label]="slaLabel(seller.slaStatus)" [tone]="slaTone(seller.slaStatus)" />
                        <small>{{ seller.priority }} / {{ seller.assignedToDisplayName ?? 'Unassigned' }}</small>
                        <small>Age {{ seller.ageHours ?? 0 }}h / due {{ seller.slaDueAtUtc | date:'short' }}</small>
                      </span>
                      <span role="cell">
                        <a mat-stroked-button [routerLink]="['/admin/sellers', seller.sellerId]" (click)="$event.stopPropagation()">Review</a>
                      </span>
                    </div>
                  }
                </div>

                <div class="admin-audit-actions">
                  <button mat-stroked-button type="button" [disabled]="page() <= 1 || isLoading()" (click)="previousPage()">Previous</button>
                  <span class="audit-count">Page {{ page() }} / {{ totalPages() }}</span>
                  <button mat-stroked-button type="button" [disabled]="page() >= totalPages() || isLoading()" (click)="nextPage()">Next</button>
                </div>
              </div>

              @if (selectedSeller()) {
                <aside class="hf-admin-evidence-panel">
                  <div class="hf-admin-card-heading">
                    <div>
                      <span>Selected seller</span>
                      <h2>{{ selectedSeller()!.storeName ?? selectedSeller()!.displayName ?? 'Seller review' }}</h2>
                    </div>
                    <app-status-badge [label]="selectedSeller()!.verificationStatus" [tone]="sellerStatusTone(selectedSeller()!.verificationStatus)" />
                  </div>

                  <div class="hf-admin-storefront-visual">
                    <span>{{ selectedSeller()!.storeSlug ?? 'No slug' }}</span>
                    <strong>{{ selectedSeller()!.storeName ?? 'Storefront pending' }}</strong>
                  </div>

                  <div class="hf-admin-summary-panel">
                    <strong>Operational context</strong>
                    <span>{{ selectedSeller()!.displayName ?? 'Unnamed seller' }}</span>
                    <span>{{ selectedSeller()!.contactEmail ?? 'No contact email' }}</span>
                    <span>Updated {{ selectedSeller()!.updatedAtUtc | date:'medium' }}</span>
                  </div>

                  <div class="hf-admin-action-strip">
                    <a mat-flat-button [routerLink]="['/admin/sellers', selectedSeller()!.sellerId]">Open review</a>
                    <a mat-stroked-button routerLink="/admin/products">Product queue</a>
                  </div>

                  <app-admin-queue-triage-panel
                    itemType="Seller"
                    [itemId]="selectedSeller()!.sellerId"
                    [summary]="selectedSeller()!"
                    (triageChanged)="loadSellers()"
                  />
                </aside>
              }
            </div>
          }
        }
      </app-workspace-shell>
    </section>
  `
})
export class AdminSellersPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminSellerService = inject(AdminSellerService);
  private readonly adminQueueTriageService = inject(AdminQueueTriageService);
  private readonly adminModerationQueueService = inject(AdminModerationQueueService);

  protected readonly sellers = signal<AdminSellerOperationalSummaryResponse[]>([]);
  protected readonly statusCounts = signal<AdminStatusCountResponse[]>([]);
  protected readonly savedViews = signal<AdminQueueSavedViewResponse[]>([]);
  protected readonly queueSummary = signal<AdminQueueSummaryResponse | null>(null);
  protected readonly selectedSellerId = signal<string | null>(null);
  protected readonly view = signal<AdminOperationalView>('NeedsAttention');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly selectedQueueItemIds = signal<string[]>([]);
  protected readonly isBulkSaving = signal(false);
  protected readonly isSavingView = signal(false);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: [''],
    sellerId: [''],
    assigned: ['Any'],
    priority: [''],
    sla: [''],
    hasNotes: [''],
    savedViewId: [''],
    savedViewName: ['']
  });

  protected readonly selectedSeller = computed(() => {
    const sellers = this.sellers();
    if (sellers.length === 0) {
      return null;
    }

    const selectedSellerId = this.selectedSellerId();
    return sellers.find(seller => seller.sellerId === selectedSellerId) ?? sellers[0];
  });

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));

  protected readonly sellerMetrics = computed(() => {
    const needsAttention = this.statusCounts().find(count => count.status === 'UnderReview')?.count ?? 0;
    const verified = this.statusCounts().find(count => count.status === 'Verified')?.count ?? 0;
    const rejected = this.statusCounts().find(count => count.status === 'Rejected')?.count ?? 0;

    return [
      { label: 'Needs attention', value: needsAttention.toString(), badge: 'Review', tone: needsAttention > 0 ? 'warning' as StatusBadgeTone : 'success' as StatusBadgeTone },
      { label: 'Verified', value: verified.toString(), badge: 'Live', tone: 'success' as StatusBadgeTone },
      { label: 'Rejected', value: rejected.toString(), badge: rejected > 0 ? 'Context' : 'Clear', tone: rejected > 0 ? 'danger' as StatusBadgeTone : 'neutral' as StatusBadgeTone },
      { label: 'Visible page', value: this.sellers().length.toString(), badge: 'Rows', tone: 'accent' as StatusBadgeTone }
    ];
  });

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadSavedViews(), this.loadQueueSummary()]);
    await this.loadSellers();
  }

  protected async applyFilters(): Promise<void> {
    this.page.set(1);
    await this.loadSellers();
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset({ search: '', status: '', sellerId: '', assigned: 'Any', priority: '', sla: '', hasNotes: '', savedViewId: '', savedViewName: '' });
    this.page.set(1);
    await this.loadSellers();
  }

  protected async applySavedView(viewId: string): Promise<void> {
    const view = this.savedViews().find(item => item.viewId === viewId);
    if (!view) {
      return;
    }

    this.filtersForm.patchValue({
      search: view.filters.search ?? '',
      status: view.filters.status ?? '',
      sellerId: view.filters.sellerId ?? '',
      assigned: view.filters.assigned ?? 'Any',
      priority: view.filters.priority ?? '',
      sla: view.filters.sla ?? '',
      hasNotes: view.filters.hasNotes === null || view.filters.hasNotes === undefined ? '' : String(view.filters.hasNotes),
      savedViewId: view.viewId,
      savedViewName: view.name
    });
    this.view.set(view.filters.view === 'All' ? 'All' : 'NeedsAttention');
    this.pageSize.set(view.filters.pageSize ?? 25);
    await this.applyFilters();
  }

  protected async saveView(): Promise<void> {
    await this.persistView(false);
  }

  protected async updateView(): Promise<void> {
    await this.persistView(true);
  }

  protected async makeDefaultView(): Promise<void> {
    const viewId = this.filtersForm.controls.savedViewId.value;
    if (!viewId) {
      return;
    }

    this.isSavingView.set(true);
    try {
      await this.adminModerationQueueService.makeDefault(viewId);
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
    try {
      await this.adminModerationQueueService.deleteSavedView(viewId);
      await this.loadSavedViews();
      this.filtersForm.patchValue({ savedViewId: '', savedViewName: '' });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingView.set(false);
    }
  }

  protected async filterByStatus(status: string): Promise<void> {
    this.filtersForm.patchValue({ status });
    await this.applyFilters();
  }

  protected async setView(view: AdminOperationalView): Promise<void> {
    this.view.set(view);
    this.page.set(1);
    await this.loadSellers();
  }

  protected selectSeller(seller: AdminSellerOperationalSummaryResponse): void {
    this.selectedSellerId.set(seller.sellerId);
  }

  protected isQueueItemSelected(sellerId: string): boolean {
    return this.selectedQueueItemIds().includes(sellerId);
  }

  protected toggleQueueItem(sellerId: string, isSelected: boolean): void {
    const current = this.selectedQueueItemIds();
    this.selectedQueueItemIds.set(isSelected
      ? Array.from(new Set([...current, sellerId]))
      : current.filter(item => item !== sellerId));
  }

  protected async bulkClaim(): Promise<void> {
    await this.bulkTriage('Claim');
  }

  protected async bulkPriority(priority: 'High'): Promise<void> {
    await this.bulkTriage('SetPriority', priority);
  }

  protected async previousPage(): Promise<void> {
    this.page.set(Math.max(1, this.page() - 1));
    await this.loadSellers();
  }

  protected async nextPage(): Promise<void> {
    this.page.set(Math.min(this.totalPages(), this.page() + 1));
    await this.loadSellers();
  }

  protected sellerStatusTone(status: string): StatusBadgeTone {
    if (['Verified', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'Suspended'].includes(status)) {
      return 'danger';
    }

    return 'warning';
  }

  protected slaTone(status: string | undefined): StatusBadgeTone {
    if (status === 'Overdue') {
      return 'danger';
    }

    if (status === 'DueSoon') {
      return 'warning';
    }

    return 'success';
  }

  protected slaLabel(status: string | undefined): string {
    return status === 'DueSoon' ? 'Due soon' : status === 'Overdue' ? 'Overdue' : 'On track';
  }

  protected slaCountLabel(status: 'DueSoon' | 'Overdue'): string {
    const count = this.queueSummary()?.slaCounts.find(item => item.key === status)?.count ?? 0;
    return `${this.slaLabel(status)}: ${count}`;
  }

  protected async loadSellers(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const filters = this.filtersForm.getRawValue();
      const response = await this.adminSellerService.getSellers({
        view: this.view(),
        search: filters.search.trim(),
        status: filters.status.trim(),
        sellerId: filters.sellerId.trim(),
        assigned: filters.assigned,
        priority: filters.priority,
        hasNotes: this.parseBooleanFilter(filters.hasNotes),
        sla: filters.sla,
        page: this.page(),
        pageSize: this.pageSize(),
        sort: 'UpdatedDesc'
      });
      this.sellers.set(response.items);
      this.statusCounts.set(response.statusCounts);
      this.totalCount.set(response.totalCount);
      this.selectedSellerId.set(response.items[0]?.sellerId ?? null);
      this.selectedQueueItemIds.set(this.selectedQueueItemIds().filter(id => response.items.some(item => item.sellerId === id)));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async bulkTriage(action: 'Claim' | 'SetPriority', priority?: 'High'): Promise<void> {
    const ids = this.selectedQueueItemIds();
    if (ids.length === 0) {
      return;
    }

    this.isBulkSaving.set(true);
    this.errorMessage.set(null);
    try {
      await this.adminQueueTriageService.bulkTriage({
        action,
        priority,
        items: ids.map(itemId => ({ itemType: 'Seller', itemId }))
      });
      await this.loadSellers();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isBulkSaving.set(false);
    }
  }

  private async loadSavedViews(): Promise<void> {
    const views = await this.adminModerationQueueService.getSavedViews('Sellers');
    this.savedViews.set(views);
    const defaultView = views.find(view => view.isDefault);
    if (defaultView && !this.filtersForm.controls.savedViewId.value) {
      this.filtersForm.patchValue({ savedViewId: defaultView.viewId, savedViewName: defaultView.name });
      await this.applySavedView(defaultView.viewId);
    }
  }

  private async loadQueueSummary(): Promise<void> {
    try {
      this.queueSummary.set(await this.adminModerationQueueService.getSummary());
    } catch {
      this.queueSummary.set(null);
    }
  }

  private async persistView(updateExisting: boolean): Promise<void> {
    const filters = this.filtersForm.getRawValue();
    const name = filters.savedViewName.trim();
    if (!name) {
      this.errorMessage.set('Enter a saved view name before saving.');
      return;
    }

    this.isSavingView.set(true);
    try {
      const request = {
        queue: 'Sellers',
        name,
        isDefault: false,
        filters: {
          view: this.view(),
          search: filters.search.trim(),
          status: filters.status.trim(),
          sellerId: filters.sellerId.trim() || null,
          assigned: filters.assigned,
          priority: filters.priority,
          hasNotes: this.parseBooleanFilter(filters.hasNotes),
          sla: filters.sla,
          sort: 'UpdatedDesc',
          pageSize: this.pageSize()
        }
      };
      const response = updateExisting && filters.savedViewId
        ? await this.adminModerationQueueService.updateSavedView(filters.savedViewId, request)
        : await this.adminModerationQueueService.createSavedView(request);
      await this.loadSavedViews();
      this.filtersForm.patchValue({ savedViewId: response.viewId, savedViewName: response.name });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingView.set(false);
    }
  }

  private parseBooleanFilter(value: string): boolean | null {
    if (value === 'true') {
      return true;
    }

    if (value === 'false') {
      return false;
    }

    return null;
  }
}
