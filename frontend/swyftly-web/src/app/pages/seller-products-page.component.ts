import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerProductSummaryResponse } from '../seller/seller-product.models';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { ProductVisualFallbackComponent } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-products-page',
  imports: [
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ProductVisualFallbackComponent,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page seller-products hf-seller-products-page">
      <app-seller-workspace-nav />

      <div class="page-header seller-products-header">
        <div>
          <span class="eyebrow">Seller catalog</span>
          <h1>Products</h1>
          <p>Manage draft listings, variants, images, stock, and review submission.</p>
        </div>
        <a mat-flat-button routerLink="/seller/products/new">New product</a>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading products...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        <section class="seller-filter-bar" aria-label="Product filters">
          <mat-form-field appearance="outline">
            <mat-label>Search products</mat-label>
            <input matInput [value]="searchTerm()" (input)="updateSearch($event)" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select [value]="statusFilter()" (selectionChange)="updateStatus($event)">
              <mat-option value="All">All statuses</mat-option>
              @for (status of statusOptions(); track status) {
                <mat-option [value]="status">{{ status }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </section>

        @if (products().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Drafts"
            heading="No products yet"
            message="Create your first product draft before adding images and variants."
          >
            <a mat-flat-button routerLink="/seller/products/new">Create product</a>
          </app-empty-state>
        } @else if (filteredProducts().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Filters"
            heading="No products match"
            message="Adjust the search or status filter to find another listing."
          />
        } @else {
          <div class="admin-table seller-ops-table seller-products-table seller-products-table--visual" role="table" aria-label="Seller products">
            <div class="admin-table-row heading seller-ops-table-row" role="row">
              <span role="columnheader">Product</span>
              <span role="columnheader">Inventory</span>
              <span role="columnheader">Preview</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (product of filteredProducts(); track product.productId) {
              <div class="admin-table-row seller-ops-table-row" role="row">
                <span role="cell">
                  <div class="seller-product-identity">
                    <div class="seller-product-thumb">
                      @if (product.primaryImageUrl) {
                        <img [src]="product.primaryImageUrl" [alt]="product.primaryImageAltText ?? product.title ?? 'Product image'" loading="lazy">
                      } @else {
                        <app-product-visual-fallback [title]="product.title ?? 'Draft product'" label="Product" tone="dress" />
                      }
                    </div>
                    <span>
                      @if (product.merchandisingLabel) {
                        <app-status-badge [label]="product.merchandisingLabel" tone="accent" />
                      }
                      <strong>{{ product.title ?? 'Untitled product' }}</strong>
                      <small>{{ product.slug ?? product.productId }}</small>
                    </span>
                  </div>
                </span>
                <span role="cell">
                  <strong>{{ product.availableQuantity ?? 0 }} available</strong>
                  <small>{{ product.reservedQuantity ?? 0 }} reserved / {{ product.totalStockQuantity ?? 0 }} stock</small>
                  @if ((product.lowStockVariantCount ?? 0) > 0 || (product.outOfStockVariantCount ?? 0) > 0) {
                    <small>{{ product.lowStockVariantCount ?? 0 }} low, {{ product.outOfStockVariantCount ?? 0 }} out</small>
                  }
                </span>
                <span role="cell">
                  @if (product.status === 'Published' && product.slug) {
                    <a [routerLink]="['/product', product.slug]">Public preview</a>
                  } @else {
                    <small>Preview appears after publishing.</small>
                  }
                  <small>Updated {{ product.updatedAtUtc | date:'mediumDate' }}</small>
                </span>
                <span role="cell"><app-status-badge [label]="product.status" [tone]="statusTone(product.status)" /></span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/seller/products', product.productId, 'edit']">Edit</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class SellerProductsPageComponent implements OnInit {
  private readonly productService = inject(SellerProductService);

  protected readonly products = signal<SellerProductSummaryResponse[]>([]);
  protected readonly searchTerm = signal('');
  protected readonly statusFilter = signal('All');
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly statusOptions = computed(() =>
    Array.from(new Set(this.products().map(product => product.status))).sort());

  protected readonly filteredProducts = computed(() => {
    const search = this.searchTerm().trim().toLowerCase();
    const status = this.statusFilter();

    return this.products().filter(product => {
      const matchesStatus = status === 'All' || product.status === status;
      const matchesSearch = search.length === 0 ||
        (product.title ?? '').toLowerCase().includes(search) ||
        (product.slug ?? '').toLowerCase().includes(search) ||
        product.productId.toLowerCase().includes(search);

      return matchesStatus && matchesSearch;
    });
  });

  async ngOnInit(): Promise<void> {
    await this.loadProducts();
  }

  protected updateSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  protected updateStatus(event: MatSelectChange): void {
    this.statusFilter.set(event.value as string);
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Published', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['PendingReview', 'NeedsAdminReview', 'ChangesRequested'].includes(status)) {
      return 'warning';
    }

    if (['Rejected', 'Archived'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  private async loadProducts(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.products.set(await this.productService.listProducts());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
