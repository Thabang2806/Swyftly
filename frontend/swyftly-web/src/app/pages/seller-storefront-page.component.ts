import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
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
  imports: [EmptyStateComponent, LuxuryPublicStylesComponent, MatButtonModule, ProductCardComponent, RouterLink, StatusBadgeComponent, UiAlertComponent],
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
              <p>{{ storefront()?.description ?? 'Published products from this Swyftly seller.' }}</p>
            </div>
          </div>
        </div>

        <section class="storefront-trust-grid" aria-label="Seller storefront details">
          <article>
            <strong>{{ storefront()?.products?.length ?? 0 }}</strong>
            <span>published product{{ (storefront()?.products?.length ?? 0) === 1 ? '' : 's' }}</span>
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

        @if ((storefront()?.products?.length ?? 0) === 0) {
          <app-empty-state
            eyebrow="No products"
            heading="No published products"
            message="This seller does not have public products yet."
          >
            <a mat-flat-button routerLink="/shop">Browse other sellers</a>
          </app-empty-state>
        } @else {
          <div class="storefront-products-header">
            <div>
              <h2>Products from {{ storefront()?.storeName }}</h2>
              <p>Each listing shows price, stock, and product detail before checkout.</p>
            </div>
            <a mat-stroked-button routerLink="/shop">Browse marketplace</a>
          </div>

          <div class="product-grid">
            @for (product of storefront()?.products; track product.productId) {
              <app-product-card [product]="product"></app-product-card>
            }
          </div>
        }
      } @else {
        <app-ui-alert tone="error">{{ errorMessage() ?? 'Seller storefront was not found.' }}</app-ui-alert>
      }
    </section>
  `
})
export class SellerStorefrontPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly publicCatalogService = inject(PublicCatalogService);

  protected readonly storefront = signal<PublicSellerStorefrontResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

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
}
