import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminSellerSummaryResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
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
    AdminWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
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
          heading="Seller review queue"
          description="Triage seller verification submissions before they can operate as verified sellers."
        >
          <div pageHeaderActions>
            <a mat-stroked-button routerLink="/admin/products">Product queue</a>
            <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
          </div>
        </app-page-header>

        <div class="hf-metric-grid">
          @for (metric of sellerMetrics(); track metric.label) {
            <app-metric-tile
              [label]="metric.label"
              [value]="metric.value"
              [badge]="metric.badge"
              [badgeTone]="metric.tone"
            />
          }
        </div>

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-moderation-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Search sellers</mat-label>
          <input matInput formControlName="search" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <input matInput formControlName="status" placeholder="UnderReview" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Storefront</mat-label>
          <input matInput formControlName="storefront" placeholder="Store name or slug" />
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit">Apply filters</button>
          <button mat-stroked-button type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading pending sellers...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (filteredSellers().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Clear"
            heading="No pending sellers"
            message="New seller submissions will appear here after onboarding is submitted for verification."
          />
        } @else {
          <div class="hf-admin-review-layout">
            <div class="hf-admin-queue-card">
              <div class="hf-admin-card-heading">
                <div>
                  <span>Seller list</span>
                  <h2>Verification submissions</h2>
                </div>
                <app-status-badge [label]="filteredSellers().length + ' visible'" tone="accent" />
              </div>

              <div class="admin-table admin-moderation-table" role="table" aria-label="Pending seller approvals">
                <div class="admin-table-row heading admin-moderation-table-row" role="row">
                  <span role="columnheader">Seller</span>
                  <span role="columnheader">Storefront</span>
                  <span role="columnheader">Submitted</span>
                  <span role="columnheader">Status</span>
                  <span role="columnheader">Action</span>
                </div>

                @for (seller of filteredSellers(); track seller.sellerId) {
                  <div
                    class="admin-table-row admin-moderation-table-row hf-admin-select-row"
                    role="row"
                    [class.active]="selectedSeller()?.sellerId === seller.sellerId"
                    (click)="selectSeller(seller)"
                  >
                    <span role="cell">
                      <strong>{{ seller.displayName ?? 'Unnamed seller' }}</strong>
                      <small>{{ seller.contactEmail ?? 'No contact email' }}</small>
                    </span>
                    <span role="cell">
                      <strong>{{ seller.storeName ?? 'No storefront' }}</strong>
                      <small>{{ seller.storeSlug ?? 'No slug' }}</small>
                    </span>
                    <span role="cell">
                      <strong>{{ seller.submittedAtUtc ? (seller.submittedAtUtc | date:'mediumDate') : 'Not recorded' }}</strong>
                      <small>{{ seller.submittedAtUtc ? (seller.submittedAtUtc | date:'shortTime') : 'No submission time' }}</small>
                    </span>
                    <span role="cell">
                      <app-status-badge [label]="seller.verificationStatus" [tone]="sellerStatusTone(seller.verificationStatus)" />
                    </span>
                    <span role="cell">
                      <a mat-stroked-button [routerLink]="['/admin/sellers', seller.sellerId]" (click)="$event.stopPropagation()">Review</a>
                    </span>
                  </div>
                }
              </div>

              <p class="audit-count">{{ filteredSellers().length }} of {{ pendingSellers().length }} seller{{ pendingSellers().length === 1 ? '' : 's' }}</p>
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
                  <strong>Evidence snapshot</strong>
                  <span>{{ selectedSeller()!.displayName ?? 'Unnamed seller' }}</span>
                  <span>{{ selectedSeller()!.contactEmail ?? 'No contact email' }}</span>
                  <span>Submitted {{ selectedSeller()!.submittedAtUtc ? (selectedSeller()!.submittedAtUtc | date:'mediumDate') : 'not recorded' }}</span>
                </div>

                <div class="hf-admin-action-strip">
                  <a mat-flat-button [routerLink]="['/admin/sellers', selectedSeller()!.sellerId]">Open review</a>
                  <a mat-stroked-button routerLink="/admin/products">Product queue</a>
                </div>
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

  protected readonly pendingSellers = signal<AdminSellerSummaryResponse[]>([]);
  protected readonly selectedSellerId = signal<string | null>(null);
  protected readonly filters = signal({ search: '', status: '', storefront: '' });
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: [''],
    storefront: ['']
  });

  protected readonly filteredSellers = computed(() => {
    const { search, status, storefront } = this.filters();
    const normalizedSearch = search.trim().toLowerCase();
    const normalizedStatus = status.trim().toLowerCase();
    const normalizedStorefront = storefront.trim().toLowerCase();

    return this.pendingSellers().filter(seller => {
      const haystack = [
        seller.displayName,
        seller.contactEmail,
        seller.storeName,
        seller.storeSlug,
        seller.verificationStatus
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();

      const matchesSearch = normalizedSearch.length === 0 || haystack.includes(normalizedSearch);
      const matchesStatus = normalizedStatus.length === 0 || seller.verificationStatus.toLowerCase().includes(normalizedStatus);
      const matchesStorefront = normalizedStorefront.length === 0 ||
        [seller.storeName, seller.storeSlug].filter(Boolean).join(' ').toLowerCase().includes(normalizedStorefront);

      return matchesSearch && matchesStatus && matchesStorefront;
    });
  });

  protected readonly selectedSeller = computed(() => {
    const filteredSellers = this.filteredSellers();
    if (filteredSellers.length === 0) {
      return null;
    }

    const selectedSellerId = this.selectedSellerId();
    return filteredSellers.find(seller => seller.sellerId === selectedSellerId) ?? filteredSellers[0];
  });

  protected readonly sellerMetrics = computed(() => {
    const sellers = this.pendingSellers();
    const withStorefront = sellers.filter(seller => seller.storeName || seller.storeSlug).length;
    const missingContact = sellers.filter(seller => !seller.contactEmail).length;

    return [
      {
        label: 'Pending sellers',
        value: sellers.length.toString(),
        badge: 'Review',
        tone: 'warning' as StatusBadgeTone
      },
      {
        label: 'Storefronts',
        value: withStorefront.toString(),
        badge: 'Submitted',
        tone: 'accent' as StatusBadgeTone
      },
      {
        label: 'Missing contact',
        value: missingContact.toString(),
        badge: missingContact > 0 ? 'Check' : 'Clear',
        tone: missingContact > 0 ? 'warning' as StatusBadgeTone : 'success' as StatusBadgeTone
      },
      {
        label: 'Filtered view',
        value: this.filteredSellers().length.toString(),
        badge: 'Visible',
        tone: 'neutral' as StatusBadgeTone
      }
    ];
  });

  async ngOnInit(): Promise<void> {
    await this.loadPendingSellers();
  }

  protected applyFilters(): void {
    this.filters.set(this.filtersForm.getRawValue());
  }

  protected clearFilters(): void {
    this.filtersForm.reset({ search: '', status: '', storefront: '' });
    this.applyFilters();
  }

  protected selectSeller(seller: AdminSellerSummaryResponse): void {
    this.selectedSellerId.set(seller.sellerId);
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

  private async loadPendingSellers(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const sellers = await this.adminSellerService.getPendingSellers();
      this.pendingSellers.set(sellers);
      this.selectedSellerId.set(sellers[0]?.sellerId ?? null);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
