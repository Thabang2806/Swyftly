import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminProductRevisionSummaryResponse, AdminProductSummaryResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
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
    AdminWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
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
          heading="Product moderation queue"
          description="Triage submitted products and AI-flagged listings before marketplace publication."
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

        @if (pendingRevisions().length > 0) {
          <article class="route-card admin-detail-card">
            <div class="hf-admin-card-heading">
              <div>
                <span>Published listing edits</span>
                <h2>Pending revisions</h2>
              </div>
              <app-status-badge [label]="pendingRevisions().length + ' pending'" tone="warning" />
            </div>
            <div class="admin-table" role="table" aria-label="Pending product revisions">
              <div class="admin-table-row heading" role="row">
                <span role="columnheader">Listing</span>
                <span role="columnheader">Seller</span>
                <span role="columnheader">Submitted</span>
                <span role="columnheader">Action</span>
              </div>
              @for (revision of pendingRevisions(); track revision.revisionId) {
                <div class="admin-table-row" role="row">
                  <span role="cell">
                    <strong>{{ revision.proposedTitle ?? revision.currentTitle ?? 'Untitled listing' }}</strong>
                    <small>Current: {{ revision.currentTitle ?? 'Untitled listing' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ revision.sellerDisplayName ?? 'Unnamed seller' }}</strong>
                    <small>{{ revision.sellerVerificationStatus ?? 'Unknown seller status' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ revision.submittedAtUtc ? (revision.submittedAtUtc | date:'mediumDate') : 'Not submitted' }}</strong>
                    <small>{{ revision.status }}</small>
                  </span>
                  <span role="cell">
                    <a mat-stroked-button [routerLink]="['/admin/products/revisions', revision.revisionId]">Review revision</a>
                  </span>
                </div>
              }
            </div>
          </article>
        }

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-moderation-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Search products</mat-label>
          <input matInput formControlName="search" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <input matInput formControlName="status" placeholder="PendingReview" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Seller</mat-label>
          <input matInput formControlName="seller" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Risk</mat-label>
          <input matInput formControlName="risk" placeholder="high, none" />
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit">Apply filters</button>
          <button mat-stroked-button type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading product reviews...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (filteredProducts().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Clear"
            heading="No products pending review"
            message="Submitted products and AI-flagged listings will appear here."
          />
        } @else {
          <div class="hf-admin-review-layout">
            <div class="hf-admin-queue-card">
              <div class="hf-admin-card-heading">
                <div>
                  <span>Review list</span>
                  <h2>Submitted listings</h2>
                </div>
                <app-status-badge [label]="filteredProducts().length + ' visible'" tone="accent" />
              </div>

              <div class="admin-table admin-moderation-table" role="table" aria-label="Pending product reviews">
                <div class="admin-table-row heading admin-moderation-table-row" role="row">
                  <span role="columnheader">Product</span>
                  <span role="columnheader">Seller</span>
                  <span role="columnheader">Updated</span>
                  <span role="columnheader">Status</span>
                  <span role="columnheader">Action</span>
                </div>

                @for (product of filteredProducts(); track product.productId) {
                  <div
                    class="admin-table-row admin-moderation-table-row hf-admin-select-row"
                    role="row"
                    [class.active]="selectedProduct()?.productId === product.productId"
                    (click)="selectProduct(product)"
                  >
                    <span role="cell">
                      <strong>{{ product.title ?? 'Untitled product' }}</strong>
                      <small>{{ product.categoryPath ?? 'No category' }}</small>
                    </span>
                    <span role="cell">
                      <strong>{{ product.sellerDisplayName ?? 'Unnamed seller' }}</strong>
                      <small>{{ product.sellerVerificationStatus ?? 'Unknown seller status' }}</small>
                    </span>
                    <span role="cell">
                      <strong>{{ product.updatedAtUtc | date:'mediumDate' }}</strong>
                      <small>{{ product.updatedAtUtc | date:'shortTime' }}</small>
                    </span>
                    <span role="cell">
                      <app-status-badge [label]="product.status" [tone]="productStatusTone(product.status)" />
                      @if (product.highRiskFlagCount > 0) {
                        <small>{{ product.highRiskFlagCount }} high-risk flag{{ product.highRiskFlagCount === 1 ? '' : 's' }}</small>
                      } @else {
                        <small>No high-risk flags</small>
                      }
                    </span>
                    <span role="cell">
                      <a mat-stroked-button [routerLink]="['/admin/products', product.productId]" (click)="$event.stopPropagation()">Review</a>
                    </span>
                  </div>
                }
              </div>

              <p class="audit-count">{{ filteredProducts().length }} of {{ pendingProducts().length }} product{{ pendingProducts().length === 1 ? '' : 's' }}</p>
            </div>

            @if (selectedProduct()) {
              <aside class="hf-admin-evidence-panel">
                <div class="hf-admin-card-heading">
                  <div>
                    <span>Selected review</span>
                    <h2>{{ selectedProduct()!.title ?? 'Untitled product' }}</h2>
                  </div>
                  <app-status-badge
                    [label]="selectedProduct()!.highRiskFlagCount > 0 ? 'Risk' : 'Clear'"
                    [tone]="selectedProduct()!.highRiskFlagCount > 0 ? 'danger' : 'success'"
                  />
                </div>

                <app-product-visual-fallback
                  [title]="selectedProduct()!.title ?? 'Submitted listing'"
                  label="Submitted image"
                  [tone]="productVisualTone(selectedProduct()!)"
                />

                <div class="hf-admin-summary-panel">
                  <strong>Review summary</strong>
                  <span>{{ selectedProduct()!.categoryPath ?? 'No category provided' }}</span>
                  <span>{{ selectedProduct()!.sellerDisplayName ?? 'Unnamed seller' }} - {{ selectedProduct()!.sellerVerificationStatus ?? 'Unknown seller status' }}</span>
                  <span>{{ selectedProduct()!.highRiskFlagCount }} high-risk flag{{ selectedProduct()!.highRiskFlagCount === 1 ? '' : 's' }}</span>
                </div>

                <div class="hf-admin-action-strip">
                  <a mat-flat-button [routerLink]="['/admin/products', selectedProduct()!.productId]">Open review</a>
                  <a mat-stroked-button routerLink="/admin/audit-logs">Audit trail</a>
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
export class AdminProductsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminProductService = inject(AdminProductService);

  protected readonly pendingProducts = signal<AdminProductSummaryResponse[]>([]);
  protected readonly pendingRevisions = signal<AdminProductRevisionSummaryResponse[]>([]);
  protected readonly selectedProductId = signal<string | null>(null);
  protected readonly filters = signal({ search: '', status: '', seller: '', risk: '' });
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: [''],
    seller: [''],
    risk: ['']
  });

  protected readonly filteredProducts = computed(() => {
    const { search, status, seller, risk } = this.filters();
    const normalizedSearch = search.trim().toLowerCase();
    const normalizedStatus = status.trim().toLowerCase();
    const normalizedSeller = seller.trim().toLowerCase();
    const normalizedRisk = risk.trim().toLowerCase();

    return this.pendingProducts().filter(product => {
      const haystack = [
        product.title,
        product.categoryPath,
        product.sellerDisplayName,
        product.sellerVerificationStatus,
        product.status
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      const sellerText = [product.sellerDisplayName, product.sellerVerificationStatus, product.sellerId]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      const riskText = product.highRiskFlagCount > 0 ? 'high risk flagged' : 'none no risk clear';

      return (normalizedSearch.length === 0 || haystack.includes(normalizedSearch)) &&
        (normalizedStatus.length === 0 || product.status.toLowerCase().includes(normalizedStatus)) &&
        (normalizedSeller.length === 0 || sellerText.includes(normalizedSeller)) &&
        (normalizedRisk.length === 0 || riskText.includes(normalizedRisk));
    });
  });

  protected readonly selectedProduct = computed(() => {
    const filteredProducts = this.filteredProducts();
    if (filteredProducts.length === 0) {
      return null;
    }

    const selectedProductId = this.selectedProductId();
    return filteredProducts.find(product => product.productId === selectedProductId) ?? filteredProducts[0];
  });

  protected readonly productMetrics = computed(() => {
    const products = this.pendingProducts();
    const highRiskCount = products.filter(product => product.highRiskFlagCount > 0).length;
    const sellerCount = new Set(products.map(product => product.sellerId)).size;

    return [
      {
        label: 'Pending products',
        value: products.length.toString(),
        badge: 'Review',
        tone: 'warning' as StatusBadgeTone
      },
      {
        label: 'AI flagged',
        value: highRiskCount.toString(),
        badge: highRiskCount > 0 ? 'Risk' : 'Clear',
        tone: highRiskCount > 0 ? 'danger' as StatusBadgeTone : 'success' as StatusBadgeTone
      },
      {
        label: 'Pending revisions',
        value: this.pendingRevisions().length.toString(),
        badge: 'Published edits',
        tone: this.pendingRevisions().length > 0 ? 'warning' as StatusBadgeTone : 'success' as StatusBadgeTone
      },
      {
        label: 'Seller count',
        value: sellerCount.toString(),
        badge: 'Queue',
        tone: 'accent' as StatusBadgeTone
      },
    ];
  });

  async ngOnInit(): Promise<void> {
    await this.loadPendingProducts();
  }

  protected applyFilters(): void {
    this.filters.set(this.filtersForm.getRawValue());
  }

  protected clearFilters(): void {
    this.filtersForm.reset({ search: '', status: '', seller: '', risk: '' });
    this.applyFilters();
  }

  protected selectProduct(product: AdminProductSummaryResponse): void {
    this.selectedProductId.set(product.productId);
  }

  protected productStatusTone(status: string): StatusBadgeTone {
    if (['Published', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'NeedsAdminReview'].includes(status)) {
      return 'danger';
    }

    return 'warning';
  }

  protected productVisualTone(product: AdminProductSummaryResponse): ProductVisualTone {
    const text = `${product.title ?? ''} ${product.categoryPath ?? ''}`.toLowerCase();

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

  private async loadPendingProducts(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [products, revisions] = await Promise.all([
        this.adminProductService.getPendingReviewProducts(),
        this.adminProductService.getPendingRevisions()
      ]);
      this.pendingProducts.set(products);
      this.pendingRevisions.set(revisions);
      this.selectedProductId.set(products[0]?.productId ?? null);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
