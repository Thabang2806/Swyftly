import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerWishlistItemResponse } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerWishlistStateService } from '../buyer/buyer-wishlist-state.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { ProductCardComponent } from '../shop/product-card.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-wishlist-page',
  imports: [
    BuyerWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    ProductCardComponent,
    RouterLink,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Wishlist"
        description="Keep track of published marketplace products you want to revisit."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/shop">Find more products</a>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading wishlist...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (wishlist().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Wishlist"
            heading="No saved products yet"
            message="Save products from listing cards or product detail pages to compare them later."
          >
            <a mat-flat-button routerLink="/shop">Browse marketplace</a>
          </app-empty-state>
        } @else {
          <div class="wishlist-grid">
            @for (item of wishlist(); track item.wishlistItemId) {
              <article class="wishlist-item">
                <app-product-card [product]="item.product" wishlistAction="hidden" />
                <div class="wishlist-item-footer">
                  <small>Saved {{ item.createdAtUtc | date:'mediumDate' }}</small>
                  @if (item.availableVariants.length > 0) {
                    <div class="wishlist-move-controls">
                      <mat-form-field appearance="outline">
                        <mat-label>Variant</mat-label>
                        <mat-select
                          [ngModel]="selectedVariantId(item)"
                          (ngModelChange)="setSelectedVariant(item.product.productId, $event)"
                        >
                          @for (variant of item.availableVariants; track variant.productVariantId) {
                            <mat-option [value]="variant.productVariantId" [disabled]="!variant.inStock">
                              {{ variant.size }} / {{ variant.colour }} - R{{ variant.price }}
                            </mat-option>
                          }
                        </mat-select>
                      </mat-form-field>
                      <mat-form-field appearance="outline">
                        <mat-label>Qty</mat-label>
                        <input
                          matInput
                          type="number"
                          min="1"
                          [ngModel]="quantityFor(item.product.productId)"
                          (ngModelChange)="setQuantity(item.product.productId, $event)"
                        >
                      </mat-form-field>
                      <button
                        mat-flat-button
                        type="button"
                        [disabled]="movingProductId() === item.product.productId || !selectedVariantId(item)"
                        (click)="moveToCart(item)"
                      >
                        {{ movingProductId() === item.product.productId ? 'Moving...' : 'Move to cart' }}
                      </button>
                    </div>
                  } @else {
                    <span class="product-card-feedback">No available variants right now.</span>
                  }
                  <button
                    mat-stroked-button
                    type="button"
                    [disabled]="removingProductId() === item.product.productId"
                    (click)="remove(item)"
                  >
                    {{ removingProductId() === item.product.productId ? 'Removing...' : 'Remove' }}
                  </button>
                </div>
              </article>
            }
          </div>
        }
      }
    </section>
  `
})
export class BuyerWishlistPageComponent implements OnInit {
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly wishlistState = inject(BuyerWishlistStateService);

  protected readonly wishlist = signal<BuyerWishlistItemResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly removingProductId = signal<string | null>(null);
  protected readonly movingProductId = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  private readonly selectedVariants = new Map<string, string>();
  private readonly quantities = new Map<string, number>();

  async ngOnInit(): Promise<void> {
    await this.loadWishlist();
  }

  protected async remove(item: BuyerWishlistItemResponse): Promise<void> {
    if (this.removingProductId()) {
      return;
    }

    this.removingProductId.set(item.product.productId);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await this.engagementService.removeWishlistItem(item.product.productId);
      this.wishlistState.markRemoved(item.product.productId);
      this.wishlist.set(this.wishlist().filter(existing => existing.product.productId !== item.product.productId));
      this.successMessage.set('Removed from wishlist.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.removingProductId.set(null);
    }
  }

  protected selectedVariantId(item: BuyerWishlistItemResponse): string {
    const productId = item.product.productId;
    const selected = this.selectedVariants.get(productId);
    if (selected) {
      return selected;
    }

    const fallback = item.availableVariants.find(variant => variant.inStock)?.productVariantId ?? '';
    if (fallback) {
      this.selectedVariants.set(productId, fallback);
    }

    return fallback;
  }

  protected setSelectedVariant(productId: string, variantId: string): void {
    this.selectedVariants.set(productId, variantId);
  }

  protected quantityFor(productId: string): number {
    return this.quantities.get(productId) ?? 1;
  }

  protected setQuantity(productId: string, rawValue: string | number): void {
    const quantity = Number(rawValue);
    if (Number.isFinite(quantity)) {
      this.quantities.set(productId, quantity);
    }
  }

  protected async moveToCart(item: BuyerWishlistItemResponse): Promise<void> {
    const productId = item.product.productId;
    const productVariantId = this.selectedVariantId(item);
    const quantity = this.quantityFor(productId);
    if (!productVariantId || quantity <= 0 || this.movingProductId()) {
      this.errorMessage.set('Choose an available variant and quantity.');
      return;
    }

    this.movingProductId.set(productId);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await this.wishlistState.moveToCart(productId, { productVariantId, quantity });
      this.wishlist.set(this.wishlist().filter(existing => existing.product.productId !== productId));
      this.selectedVariants.delete(productId);
      this.quantities.delete(productId);
      this.successMessage.set('Moved to cart.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.movingProductId.set(null);
    }
  }

  private async loadWishlist(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const items = await this.engagementService.listWishlist();
      this.wishlist.set(items);
      for (const item of items) {
        this.wishlistState.markSaved(item.product.productId);
        const firstAvailable = item.availableVariants.find(variant => variant.inStock);
        if (firstAvailable) {
          this.selectedVariants.set(item.product.productId, firstAvailable.productVariantId);
        }
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
