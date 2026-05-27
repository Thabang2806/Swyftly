import { CurrencyPipe } from '@angular/common';
import { Component, Injector, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { BuyerWishlistStateService } from '../buyer/buyer-wishlist-state.service';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { ProductSearchItemResponse } from './public-catalog.models';

@Component({
  selector: 'app-product-card',
  imports: [CurrencyPipe, MatButtonModule, ProductVisualFallbackComponent, RouterLink, StatusBadgeComponent],
  template: `
    <article class="product-card">
      <a class="product-card-media" [routerLink]="['/product', product().slug]">
        @if (product().primaryImageUrl) {
          <img [src]="product().primaryImageUrl" [alt]="product().primaryImageAltText ?? product().title ?? 'Product image'" loading="lazy">
        } @else {
          <app-product-visual-fallback
            [label]="product().categoryPath ?? 'Swyftly edit'"
            [title]="product().title ?? 'Product'"
            [tone]="visualTone()"
          />
        }
      </a>

      <div class="product-card-body">
        <div class="product-card-heading">
          <app-status-badge [label]="product().merchandisingLabel ?? product().categoryPath ?? 'Marketplace'" tone="accent" />
          <a class="product-card-title" [routerLink]="['/product', product().slug]">{{ product().title ?? 'Untitled product' }}</a>
          @if (product().sellerStoreSlug) {
            <a class="product-card-seller" [routerLink]="['/seller', product().sellerStoreSlug]">{{ product().sellerStoreName ?? 'Seller' }}</a>
          } @else {
            <span class="product-card-seller">{{ product().sellerStoreName ?? 'Seller' }}</span>
          }
        </div>

        @if (product().shortDescription) {
          <p class="product-card-description">{{ product().shortDescription }}</p>
        }

        <div class="product-card-price">
          <strong>{{ product().priceMin | currency:'ZAR':'symbol-narrow' }}</strong>
          @if (product().compareAtPriceMin) {
            <span>{{ product().compareAtPriceMin | currency:'ZAR':'symbol-narrow' }}</span>
          }
        </div>

        <div class="product-card-meta">
          <app-status-badge [label]="product().inStock ? 'In stock' : 'Out of stock'" [tone]="product().inStock ? 'success' : 'warning'" />
          <span>{{ product().publishedAtUtc ? 'Recently listed' : 'Marketplace item' }}</span>
        </div>

        <div class="product-card-actions">
          <a mat-stroked-button [routerLink]="['/product', product().slug]">View details</a>
          @if (wishlistAction() !== 'hidden') {
            <button
              mat-button
              type="button"
              [disabled]="isSavingWishlist()"
              [attr.aria-label]="isWishlisted() ? 'Remove product from wishlist' : 'Save product to wishlist'"
              (click)="toggleWishlist()"
            >
              {{ isSavingWishlist() ? 'Saving...' : isWishlisted() ? 'Remove' : 'Save' }}
            </button>
          }
        </div>

        @if (wishlistError()) {
          <small class="product-card-feedback">{{ wishlistError() }}</small>
        }
      </div>
    </article>
  `
})
export class ProductCardComponent implements OnInit {
  private readonly injector = inject(Injector);

  readonly product = input.required<ProductSearchItemResponse>();
  readonly wishlistAction = input<'save' | 'hidden'>('save');
  readonly isInitiallyWishlisted = input(false);

  protected readonly isSavingWishlist = signal(false);
  protected readonly localWishlisted = signal<boolean | null>(null);
  protected readonly isWishlisted = computed(() => this.localWishlisted() ?? this.isInitiallyWishlisted());
  protected readonly wishlistError = signal<string | null>(null);
  protected readonly visualTone = computed<ProductVisualTone>(() => {
    const product = this.product();
    const text = [
      product.title,
      product.categoryPath,
      product.shortDescription,
      ...product.tags
    ].filter(Boolean).join(' ').toLowerCase();

    if (/(jewel|ring|earring|necklace|bracelet|gold|silver)/.test(text)) {
      return 'jewel';
    }

    if (/(beauty|skin|makeup|lip|hair|fragrance|serum)/.test(text)) {
      return 'beauty';
    }

    if (/(bag|tote|clutch|purse|wallet)/.test(text)) {
      return 'bag';
    }

    if (/(shoe|heel|sneaker|boot|sandal)/.test(text)) {
      return 'shoe';
    }

    if (/(dress|coat|shirt|denim|fashion|clothing|linen|silk)/.test(text)) {
      return 'dress';
    }

    return 'neutral';
  });

  ngOnInit(): void {
    void this.initializeWishlistState();
  }

  protected async toggleWishlist(): Promise<void> {
    this.wishlistError.set(null);

    const authService = this.injector.get(AuthService);
    const router = this.injector.get(Router);

    await authService.initialize();
    if (!authService.hasAnyRole(['Buyer'])) {
      await router.navigate(['/login'], {
        queryParams: { returnUrl: router.url }
      });
      return;
    }

    if (this.isSavingWishlist()) {
      return;
    }

    if (this.isWishlisted()) {
      await this.removeFromWishlist();
      return;
    }

    this.isSavingWishlist.set(true);
    try {
      await this.injector.get(BuyerWishlistStateService).save(this.product().productId);
      this.localWishlisted.set(true);
    } catch (error) {
      this.wishlistError.set(getApiErrorMessage(error));
    } finally {
      this.isSavingWishlist.set(false);
    }
  }

  private async removeFromWishlist(): Promise<void> {
    this.isSavingWishlist.set(true);
    try {
      await this.injector.get(BuyerWishlistStateService).remove(this.product().productId);
      this.localWishlisted.set(false);
    } catch (error) {
      this.wishlistError.set(getApiErrorMessage(error));
    } finally {
      this.isSavingWishlist.set(false);
    }
  }

  private async initializeWishlistState(): Promise<void> {
    if (this.wishlistAction() === 'hidden') {
      return;
    }

    try {
      const authService = this.injector.get(AuthService);
      await authService.initialize();
      if (!authService.hasAnyRole(['Buyer'])) {
        return;
      }

      const wishlistState = this.injector.get(BuyerWishlistStateService);
      await wishlistState.load();
      this.localWishlisted.set(wishlistState.isSaved(this.product().productId));
    } catch {
      // Wishlist state is opportunistic on public cards; explicit actions still surface errors.
    }
  }
}
