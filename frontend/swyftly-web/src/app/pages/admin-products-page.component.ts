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
import { AdminProductModerationItemResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { AdminQueueTriagePanelComponent } from '../admin/admin-queue-triage-panel.component';
import { AdminQueueTriageService } from '../admin/admin-queue-triage.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { MetricTileComponent } from '../shared/ui/metric-tile.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';
import { WorkspaceShellComponent } from '../shared/ui/workspace-shell.component';

@Component({
  selector: 'app-admin-products-page',
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
    ProductVisualFallbackComponent,
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
          heading="Product moderation operations"
          description="Review submitted products, published listing revisions, and variant pricing revisions from one operational queue."
        >
          <div pageHeaderActions>
            <a mat-stroked-button routerLink="/admin/sellers">Seller queue</a>
            <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
          </div>
        </app-page-header>

        <div class="hf-metric-grid">
          @for (metric of productMetrics(); track metric.label) {
            <app-metric-tile
              [label]="metric.label"
              [value]="metric.value"
              [badge]="metric.badge"
              [badgeTone]="metric.tone"
            />
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
          <button mat-stroked-button type="button" [disabled]="view() === 'All'" (click)="setView('All')">All moderation items</button>
        </div>

        <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-moderation-filters" novalidate>
          <mat-form-field appearance="outline">
            <mat-label>Search items</mat-label>
            <input matInput formControlName="search" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <input matInput formControlName="status" placeholder="PendingReview, Published, Rejected" />
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
          <div class="route-card">Loading product moderation items...</div>
        } @else {
          @if (errorMessage()) {
            <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
          }

          @if (items().length === 0 && !errorMessage()) {
            <app-empty-state
              eyebrow="Clear"
              heading="No product moderation items found"
              message="Adjust the view or filters to inspect submitted products, listing revisions, and variant pricing revisions."
            />
          } @else {
            <div class="hf-admin-review-layout">
              <div class="hf-admin-queue-card">
                <div class="hf-admin-card-heading">
                  <div>
                    <span>{{ view() === 'NeedsAttention' ? 'Needs attention' : 'All-state view' }}</span>
                    <h2>Product moderation records</h2>
                  </div>
                  <app-status-badge [label]="totalCount() + ' total'" tone="accent" />
                </div>

                <div class="admin-audit-actions">
                  <button mat-stroked-button type="button" [disabled]="selectedQueueItemKeys().length === 0 || isBulkSaving()" (click)="bulkClaim()">Claim selected</button>
                  <button mat-stroked-button type="button" [disabled]="selectedQueueItemKeys().length === 0 || isBulkSaving()" (click)="bulkPriority('High')">Mark high</button>
                  <span class="audit-count">{{ selectedQueueItemKeys().length }} selected</span>
                </div>

                <div class="admin-table admin-moderation-table" role="table" aria-label="Product moderation queue">
                  <div class="admin-table-row heading admin-moderation-table-row" role="row">
                    <span role="columnheader">Item</span>
                    <span role="columnheader">Seller</span>
                    <span role="columnheader">Submitted</span>
                    <span role="columnheader">Status</span>
                    <span role="columnheader">Action</span>
                  </div>

                  @for (item of items(); track item.itemType + item.id) {
                    <div
                      class="admin-table-row admin-moderation-table-row hf-admin-select-row"
                      role="row"
                      [class.active]="selectedItem()?.id === item.id"
                      (click)="selectItem(item)"
                    >
                      <span role="cell">
                        <mat-checkbox
                          [checked]="isQueueItemSelected(item)"
                          (click)="$event.stopPropagation()"
                          (change)="toggleQueueItem(item, $event.checked)"
                        />
                        <strong>{{ item.title ?? 'Untitled product' }}</strong>
                        <small>{{ itemTypeLabel(item.itemType) }} / {{ item.categoryPath ?? 'No category' }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ item.sellerDisplayName ?? 'Unnamed seller' }}</strong>
                        <small>{{ item.sellerVerificationStatus ?? item.sellerId }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ item.submittedAtUtc ? (item.submittedAtUtc | date:'mediumDate') : 'Not submitted' }}</strong>
                        <small>Updated {{ item.updatedAtUtc | date:'short' }}</small>
                      </span>
                      <span role="cell">
                        <app-status-badge [label]="item.status" [tone]="productStatusTone(item.status)" />
                        <app-status-badge [label]="slaLabel(item.slaStatus)" [tone]="slaTone(item.slaStatus)" />
                        <small>{{ item.priority }} / {{ item.assignedToDisplayName ?? 'Unassigned' }}</small>
                        <small>Age {{ item.ageHours ?? 0 }}h / due {{ item.slaDueAtUtc | date:'short' }}</small>
                        @if (item.riskFlagCount > 0) {
                          <small>{{ item.riskFlagCount }} risk flag{{ item.riskFlagCount === 1 ? '' : 's' }}</small>
                        } @else if (item.itemCount > 0) {
                          <small>{{ item.itemCount }} staged item{{ item.itemCount === 1 ? '' : 's' }}</small>
                        } @else {
                          <small>No risk flags</small>
                        }
                      </span>
                      <span role="cell">
                        <a mat-stroked-button [routerLink]="item.detailRoute" (click)="$event.stopPropagation()">{{ reviewLabel(item) }}</a>
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

              @if (selectedItem()) {
                <aside class="hf-admin-evidence-panel">
                  <div class="hf-admin-card-heading">
                    <div>
                      <span>Selected moderation item</span>
                      <h2>{{ selectedItem()!.title ?? 'Product review' }}</h2>
                    </div>
                    <app-status-badge [label]="itemTypeLabel(selectedItem()!.itemType)" tone="accent" />
                  </div>

                  <app-product-visual-fallback
                    [title]="selectedItem()!.title ?? 'Submitted listing'"
                    label="Review image"
                    [tone]="productVisualTone(selectedItem()!)"
                  />

                  <div class="hf-admin-summary-panel">
                    <strong>Review context</strong>
                    <span>Status: {{ selectedItem()!.status }}</span>
                    <span>{{ selectedItem()!.sellerDisplayName ?? 'Unnamed seller' }} / {{ selectedItem()!.sellerVerificationStatus ?? 'Unknown seller status' }}</span>
                    <span>{{ selectedItem()!.riskFlagCount }} risk flag{{ selectedItem()!.riskFlagCount === 1 ? '' : 's' }}</span>
                    @if (selectedItem()!.itemCount > 0) {
                      <span>{{ selectedItem()!.itemCount }} staged change{{ selectedItem()!.itemCount === 1 ? '' : 's' }}</span>
                    }
                  </div>

                  <div class="hf-admin-action-strip">
                    <a mat-flat-button [routerLink]="selectedItem()!.detailRoute">{{ reviewLabel(selectedItem()!) }}</a>
                    <a mat-stroked-button routerLink="/admin/audit-logs">Audit trail</a>
                  </div>

                  <app-admin-queue-triage-panel
                    [itemType]="selectedItem()!.itemType"
                    [itemId]="selectedItem()!.id"
                    [summary]="selectedItem()!"
                    (triageChanged)="loadModerationItems()"
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
export class AdminProductsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminProductService = inject(AdminProductService);
  private readonly adminQueueTriageService = inject(AdminQueueTriageService);
  private readonly adminModerationQueueService = inject(AdminModerationQueueService);

  protected readonly items = signal<AdminProductModerationItemResponse[]>([]);
  protected readonly statusCounts = signal<AdminStatusCountResponse[]>([]);
  protected readonly savedViews = signal<AdminQueueSavedViewResponse[]>([]);
  protected readonly queueSummary = signal<AdminQueueSummaryResponse | null>(null);
  protected readonly selectedItemId = signal<string | null>(null);
  protected readonly view = signal<AdminOperationalView>('NeedsAttention');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly selectedQueueItemKeys = signal<string[]>([]);
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

  protected readonly selectedItem = computed(() => {
    const items = this.items();
    if (items.length === 0) {
      return null;
    }

    const selectedItemId = this.selectedItemId();
    return items.find(item => item.id === selectedItemId) ?? items[0];
  });

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));

  protected readonly productMetrics = computed(() => {
    const pendingReview = this.statusCounts().find(count => count.status === 'PendingReview')?.count ?? 0;
    const needsAdminReview = this.statusCounts().find(count => count.status === 'NeedsAdminReview')?.count ?? 0;
    const revisionCount = this.items().filter(item => item.itemType !== 'Product').length;
    const riskCount = this.items().filter(item => item.riskFlagCount > 0).length;

    return [
      { label: 'Pending review', value: pendingReview.toString(), badge: 'Queue', tone: pendingReview > 0 ? 'warning' as StatusBadgeTone : 'success' as StatusBadgeTone },
      { label: 'Needs admin review', value: needsAdminReview.toString(), badge: needsAdminReview > 0 ? 'Risk' : 'Clear', tone: needsAdminReview > 0 ? 'danger' as StatusBadgeTone : 'success' as StatusBadgeTone },
      { label: 'Published revisions', value: revisionCount.toString(), badge: 'Staged edits', tone: revisionCount > 0 ? 'warning' as StatusBadgeTone : 'neutral' as StatusBadgeTone },
      { label: 'Risk flags', value: riskCount.toString(), badge: riskCount > 0 ? 'Review' : 'Clear', tone: riskCount > 0 ? 'danger' as StatusBadgeTone : 'success' as StatusBadgeTone }
    ];
  });

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadSavedViews(), this.loadQueueSummary()]);
    await this.loadModerationItems();
  }

  protected async applyFilters(): Promise<void> {
    this.page.set(1);
    await this.loadModerationItems();
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset({ search: '', status: '', sellerId: '', assigned: 'Any', priority: '', sla: '', hasNotes: '', savedViewId: '', savedViewName: '' });
    this.page.set(1);
    await this.loadModerationItems();
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
    await this.loadModerationItems();
  }

  protected selectItem(item: AdminProductModerationItemResponse): void {
    this.selectedItemId.set(item.id);
  }

  protected isQueueItemSelected(item: AdminProductModerationItemResponse): boolean {
    return this.selectedQueueItemKeys().includes(this.queueItemKey(item));
  }

  protected toggleQueueItem(item: AdminProductModerationItemResponse, isSelected: boolean): void {
    const key = this.queueItemKey(item);
    const current = this.selectedQueueItemKeys();
    this.selectedQueueItemKeys.set(isSelected
      ? Array.from(new Set([...current, key]))
      : current.filter(itemKey => itemKey !== key));
  }

  protected async bulkClaim(): Promise<void> {
    await this.bulkTriage('Claim');
  }

  protected async bulkPriority(priority: 'High'): Promise<void> {
    await this.bulkTriage('SetPriority', priority);
  }

  protected async previousPage(): Promise<void> {
    this.page.set(Math.max(1, this.page() - 1));
    await this.loadModerationItems();
  }

  protected async nextPage(): Promise<void> {
    this.page.set(Math.min(this.totalPages(), this.page() + 1));
    await this.loadModerationItems();
  }

  protected itemTypeLabel(itemType: string): string {
    if (itemType === 'ListingRevision') {
      return 'Listing revision';
    }

    if (itemType === 'VariantRevision') {
      return 'Variant revision';
    }

    return 'Product';
  }

  protected reviewLabel(item: AdminProductModerationItemResponse): string {
    if (item.itemType === 'ListingRevision') {
      return 'Review revision';
    }

    if (item.itemType === 'VariantRevision') {
      return 'Review variants';
    }

    return 'Review product';
  }

  protected productStatusTone(status: string): StatusBadgeTone {
    if (['Published', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'NeedsAdminReview'].includes(status)) {
      return 'danger';
    }

    if (['Draft', 'Cancelled', 'Archived'].includes(status)) {
      return 'neutral';
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

  protected productVisualTone(item: AdminProductModerationItemResponse): ProductVisualTone {
    const text = `${item.title ?? ''} ${item.categoryPath ?? ''}`.toLowerCase();

    if (text.includes('jewel') || text.includes('earring') || text.includes('ring')) {
      return 'jewel';
    }

    if (text.includes('beauty') || text.includes('serum') || text.includes('skin')) {
      return 'beauty';
    }

    if (text.includes('bag')) {
      return 'bag';
    }

    if (text.includes('shoe') || text.includes('sneaker')) {
      return 'shoe';
    }

    if (text.includes('dress') || text.includes('clothing')) {
      return 'dress';
    }

    return 'neutral';
  }

  protected async loadModerationItems(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const filters = this.filtersForm.getRawValue();
      const response = await this.adminProductService.getModerationItems({
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
      this.items.set(response.items);
      this.statusCounts.set(response.statusCounts);
      this.totalCount.set(response.totalCount);
      this.selectedItemId.set(response.items[0]?.id ?? null);
      const visibleKeys = response.items.map(item => this.queueItemKey(item));
      this.selectedQueueItemKeys.set(this.selectedQueueItemKeys().filter(key => visibleKeys.includes(key)));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async bulkTriage(action: 'Claim' | 'SetPriority', priority?: 'High'): Promise<void> {
    const items = this.items().filter(item => this.selectedQueueItemKeys().includes(this.queueItemKey(item)));
    if (items.length === 0) {
      return;
    }

    this.isBulkSaving.set(true);
    this.errorMessage.set(null);
    try {
      await this.adminQueueTriageService.bulkTriage({
        action,
        priority,
        items: items.map(item => ({ itemType: item.itemType, itemId: item.id }))
      });
      await this.loadModerationItems();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isBulkSaving.set(false);
    }
  }

  private queueItemKey(item: AdminProductModerationItemResponse): string {
    return `${item.itemType}:${item.id}`;
  }

  private async loadSavedViews(): Promise<void> {
    const views = await this.adminModerationQueueService.getSavedViews('Products');
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
        queue: 'Products',
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
