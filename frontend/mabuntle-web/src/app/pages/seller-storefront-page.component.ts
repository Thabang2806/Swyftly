import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { StorefrontAnalyticsService } from '../analytics/storefront-analytics.service';
import { getApiErrorMessage } from '../auth/api-error';
import { ProductCardComponent } from '../shop/product-card.component';
import { PublicSellerStorefrontResponse } from '../shop/public-catalog.models';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-storefront-page',
  imports: [
    EmptyStateComponent,
    LuxuryPublicStylesComponent,
    ProductCardComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <app-luxury-public-styles />
    <section class="page shop-surface">
      <a class="admin-back-link" routerLink="/shop">Back to shop</a>

      @if (isLoading()) {
        <div class="route-card">Loading seller storefront...</div>
      } @else if (storefront()) {
        <div class="seller-storefront-hero">
          @if (storefront()?.bannerUrl) {
            <img [src]="storefront()?.bannerUrl" [alt]="storefront()?.storeName ?? 'Seller banner'">
          }
          <div class="seller-storefront-copy">
            <div class="seller-storefront-logo" aria-hidden="true">
              @if (storefront()?.logoUrl) {
                <img [src]="storefront()?.logoUrl" [alt]="storefront()?.storeName ?? 'Seller logo'">
              } @else {
                {{ storefrontInitial() }}
              }
            </div>
            <div>
              <app-status-badge label="Verified marketplace seller" tone="success" />
              <h1>{{ storefront()?.storeName }}</h1>
              <p>{{ storefront()?.description ?? 'Published products from this Mabuntle seller.' }}</p>
            </div>
          </div>
        </div>

        <section class="storefront-trust-grid" aria-label="Seller storefront details">
          <article>
            <strong>{{ storefront()?.products?.length ?? 0 }}</strong>
            <span>published product{{ (storefront()?.products?.length ?? 0) === 1 ? '' : 's' }}</span>
          </article>
          <article>
            <strong>{{ inStockProductCount() }}</strong>
            <span>currently in-stock listing{{ inStockProductCount() === 1 ? '' : 's' }}</span>
          </article>
          <article>
            <strong>Verified</strong>
            <span>seller visibility is checked before public storefront display</span>
          </article>
          <article>
            <strong>Marketplace support</strong>
            <span>buyer support remains available through order and account workflows</span>
          </article>
        </section>

        <section class="route-card seller-policy-context">
          <div class="storefront-products-header">
            <div>
              <h2>Store policies</h2>
              <p>Current seller guidance. Checkout keeps a copy of these policies on new orders.</p>
            </div>
          </div>
          @if (sellerPolicyEntries().length > 0) {
            <div class="settings-summary-list">
              @for (entry of sellerPolicyEntries(); track entry.label) {
                <div>
                  <span>{{ entry.label }}</span>
                  <strong>{{ entry.value }}</strong>
                </div>
              }
            </div>
          } @else {
            <app-ui-alert tone="info">This seller has not added detailed store policies yet. Buyer support remains available through order and account workflows.</app-ui-alert>
          }
        </section>

        @if ((storefront()?.products?.length ?? 0) === 0) {
          <app-empty-state
            eyebrow="No products"
            heading="No published products"
            message="This seller does not have public products yet."
          >
            <a data-ui-button="primary" routerLink="/shop">Browse other sellers</a>
          </app-empty-state>
        } @else {
          <div class="storefront-products-header">
            <div>
              <h2>Products from {{ storefront()?.storeName }}</h2>
              <p>{{ filteredProducts().length }} shown from this storefront. Each listing shows price, stock, and product detail before checkout.</p>
            </div>
            <a data-ui-button="secondary" routerLink="/shop">Browse marketplace</a>
          </div>

          <form [formGroup]="storefrontFiltersForm" class="storefront-filter-bar" aria-label="Storefront product filters" novalidate>
            <label class="ui-field">
              <span>Search this store</span>
              <input formControlName="query">
            </label>

            <label class="ui-field">
              <span>Availability</span>
              <select formControlName="availability">
                <option value="">All products</option>
                <option value="in_stock">In stock</option>
                <option value="out_of_stock">Out of stock</option>
              </select>
            </label>

            <label class="ui-field">
              <span>Sort</span>
              <select formControlName="sort">
                <option value="newest">Newest</option>
                <option value="price_asc">Price low to high</option>
                <option value="price_desc">Price high to low</option>
              </select>
            </label>

            <button data-ui-button="secondary" type="button" (click)="clearStorefrontFilters()">Clear</button>
          </form>

          @if (filteredProducts().length === 0) {
            <app-empty-state
              eyebrow="No matches"
              heading="No products match these store filters"
              message="Clear the storefront search or browse the wider marketplace."
            >
              <button data-ui-button="primary" type="button" (click)="clearStorefrontFilters()">Clear store filters</button>
            </app-empty-state>
          } @else {
            <div class="product-grid">
            @for (product of filteredProducts(); track product.productId) {
              <app-product-card [product]="product"></app-product-card>
            }
            </div>
          }
        }
      } @else {
        <app-ui-alert tone="error">{{ errorMessage() ?? 'Seller storefront was not found.' }}</app-ui-alert>
      }
    </section>
  `
})
export class SellerStorefrontPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly publicCatalogService = inject(PublicCatalogService);
  private readonly router = inject(Router);
  private readonly storefrontAnalytics = inject(StorefrontAnalyticsService);

  protected readonly storefront = signal<PublicSellerStorefrontResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly storefrontFiltersForm = this.formBuilder.group({
    query: [''],
    availability: [''],
    sort: ['newest']
  });
  protected inStockProductCount(): number {
    return this.storefront()?.products.filter(product => product.inStock).length ?? 0;
  }

  protected filteredProducts() {
    const products = [...(this.storefront()?.products ?? [])];
    const filters = this.storefrontFiltersForm.getRawValue();
    const query = filters.query.trim().toLowerCase();
    const filtered = products.filter(product => {
      const searchable = [
        product.title,
        product.shortDescription,
        product.categoryPath,
        product.merchandisingLabel,
        ...product.tags
      ].filter(Boolean).join(' ').toLowerCase();
      const queryMatches = !query || searchable.includes(query);
      const availabilityMatches =
        !filters.availability ||
        (filters.availability === 'in_stock' && product.inStock) ||
        (filters.availability === 'out_of_stock' && !product.inStock);
      return queryMatches && availabilityMatches;
    });

    return filtered.sort((left, right) => {
      if (filters.sort === 'price_asc') {
        return left.priceMin - right.priceMin;
      }

      if (filters.sort === 'price_desc') {
        return right.priceMin - left.priceMin;
      }

      return (right.publishedAtUtc ?? '').localeCompare(left.publishedAtUtc ?? '');
    });
  }

  async ngOnInit(): Promise<void> {
    const storeSlug = this.route.snapshot.paramMap.get('storeSlug');
    if (!storeSlug) {
      this.errorMessage.set('Seller slug is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.storefront.set(await this.publicCatalogService.getSellerStorefront(storeSlug));
      this.storefrontAnalytics.trackStorefrontView(storeSlug, this.router.url);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.storefront.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected storefrontInitial(): string {
    return this.storefront()?.storeName?.trim().charAt(0).toUpperCase() || 'S';
  }

  protected clearStorefrontFilters(): void {
    this.storefrontFiltersForm.reset({
      query: '',
      availability: '',
      sort: 'newest'
    });
  }

  protected sellerPolicyEntries(): { label: string; value: string }[] {
    const policy = this.storefront()?.sellerPolicy;
    if (!policy) {
      return [];
    }

    return [
      policy.returnWindowDays === null ? null : { label: 'Return window', value: `${policy.returnWindowDays} day${policy.returnWindowDays === 1 ? '' : 's'}` },
      policy.returnPolicy ? { label: 'Returns', value: policy.returnPolicy } : null,
      policy.exchangePolicy ? { label: 'Exchanges', value: policy.exchangePolicy } : null,
      policy.fulfilmentPolicy ? { label: 'Fulfilment', value: policy.fulfilmentPolicy } : null,
      policy.supportPolicy ? { label: 'Support', value: policy.supportPolicy } : null
    ].filter((entry): entry is { label: string; value: string } => entry !== null);
  }
}
