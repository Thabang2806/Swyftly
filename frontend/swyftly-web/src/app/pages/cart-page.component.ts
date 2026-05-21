import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerWishlistStateService } from '../buyer/buyer-wishlist-state.service';
import { CartItemResponse, CartResponse } from '../cart/cart.models';
import { CartService } from '../cart/cart.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-cart-page',
  imports: [
    CurrencyPipe,
    EmptyStateComponent,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    PageHeaderComponent,
    ProductVisualFallbackComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page cart-page">
      <app-page-header
        eyebrow="Cart"
        heading="Cart"
        description="Review product quantities, seller details, and subtotal before checkout."
      >
        <a pageHeaderActions mat-stroked-button routerLink="/shop">Continue shopping</a>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading cart...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (!cart()?.items?.length) {
          <app-empty-state
            eyebrow="Empty"
            heading="Your cart is empty"
            message="Products you add from the shop will appear here."
          >
            <a mat-flat-button routerLink="/shop">Shop products</a>
          </app-empty-state>
        } @else {
          <div class="cart-trust-strip" aria-label="Cart trust signals">
            <div>
              <app-status-badge label="Single-seller checkout" tone="accent" />
              <strong>{{ cart()?.sellerStoreName ?? 'Seller' }}</strong>
              <span>Items from one seller are checked out together so stock and fulfilment stay clear.</span>
            </div>
            <div>
              <app-status-badge label="Stock checked at checkout" tone="success" />
              <span>Inventory is reserved when checkout starts.</span>
            </div>
            <div>
              <app-status-badge label="Support path visible" />
              <span>Order support and returns are handled through the marketplace account area.</span>
            </div>
          </div>

          <div class="cart-layout">
            <div class="cart-items">
              @for (item of cart()?.items; track item.cartItemId) {
                <article class="cart-item">
                  <div class="cart-item-media" aria-hidden="true">
                    @if (item.primaryImageUrl) {
                      <img [src]="item.primaryImageUrl" [alt]="item.primaryImageAltText ?? item.productTitle ?? 'Cart product image'">
                    } @else {
                      <app-product-visual-fallback
                        [label]="item.size + ' / ' + item.colour"
                        [title]="item.productTitle ?? 'Cart item'"
                        [tone]="visualTone(item)"
                      />
                    }
                  </div>

                  <div class="cart-item-copy">
                    @if (item.productSlug) {
                      <a [routerLink]="['/product', item.productSlug]"><strong>{{ item.productTitle ?? 'Product' }}</strong></a>
                    } @else {
                      <strong>{{ item.productTitle ?? 'Product' }}</strong>
                    }
                    <span>{{ item.size }} / {{ item.colour }}</span>
                    <small>SKU {{ item.sku }} - {{ item.unitPrice | currency:'ZAR':'symbol-narrow' }} each</small>
                  </div>

                  <mat-form-field appearance="outline">
                    <mat-label>Qty</mat-label>
                    <input
                      matInput
                      type="number"
                      min="1"
                      [ngModel]="item.quantity"
                      (ngModelChange)="setLocalQuantity(item.cartItemId, $event)"
                      [disabled]="updatingItemId() === item.cartItemId"
                    >
                  </mat-form-field>

                  <div class="cart-item-total">
                    <span>Line total</span>
                    <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>
                  </div>

                  <div class="cart-item-actions">
                    <button
                      mat-stroked-button
                      type="button"
                      [disabled]="updatingItemId() === item.cartItemId"
                      (click)="updateQuantity(item.cartItemId)"
                    >
                      Update
                    </button>
                    <button
                      mat-button
                      type="button"
                      [disabled]="updatingItemId() === item.cartItemId"
                      (click)="removeItem(item.cartItemId)"
                    >
                      Remove
                    </button>
                    <button
                      mat-button
                      type="button"
                      [disabled]="savingItemId() === item.cartItemId || updatingItemId() === item.cartItemId"
                      (click)="saveForLater(item)"
                    >
                      {{ savingItemId() === item.cartItemId ? 'Saving...' : 'Save for later' }}
                    </button>
                  </div>
                </article>
              }
            </div>

            <aside class="order-summary">
              <h2>Order summary</h2>
              <span class="product-card-seller">{{ cart()?.sellerStoreName ?? 'Seller' }}</span>
              <div class="summary-row">
                <span>Items</span>
                <strong>{{ cart()?.totalQuantity }}</strong>
              </div>
              <div class="summary-row">
                <span>Subtotal</span>
                <strong>{{ cart()?.subtotal | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
              <div class="summary-row">
                <span>Delivery</span>
                <strong>Confirmed next</strong>
              </div>
              <div class="summary-row">
                <span>Payment</span>
                <strong>After order creation</strong>
              </div>
              <div class="summary-row total">
                <span>Estimated total</span>
                <strong>{{ cart()?.subtotal | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
              <p>Delivery, discounts, fees, and payment confirmation are finalized during checkout.</p>
              <a mat-flat-button routerLink="/checkout" [class.disabled-link]="!cart()?.items?.length">Checkout</a>
            </aside>
          </div>
        }
      }
    </section>
  `
})
export class CartPageComponent implements OnInit {
  private readonly cartService = inject(CartService);
  private readonly wishlistState = inject(BuyerWishlistStateService);

  protected readonly cart = signal<CartResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly updatingItemId = signal<string | null>(null);
  protected readonly savingItemId = signal<string | null>(null);
  private readonly localQuantities = new Map<string, number>();

  async ngOnInit(): Promise<void> {
    await this.loadCart();
  }

  protected productInitial(title: string | null): string {
    return title?.trim().charAt(0).toUpperCase() || 'S';
  }

  protected visualTone(item: CartItemResponse): ProductVisualTone {
    const text = `${item.productTitle ?? ''} ${item.colour} ${item.size}`.toLowerCase();
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
  }

  protected setLocalQuantity(cartItemId: string, rawValue: string | number): void {
    const quantity = Number(rawValue);
    if (Number.isFinite(quantity)) {
      this.localQuantities.set(cartItemId, quantity);
    }
  }

  protected async updateQuantity(cartItemId: string): Promise<void> {
    const currentItem = this.cart()?.items.find(item => item.cartItemId === cartItemId);
    const quantity = this.localQuantities.get(cartItemId) ?? currentItem?.quantity ?? 0;
    if (quantity <= 0) {
      this.errorMessage.set('Quantity must be at least 1.');
      return;
    }

    this.updatingItemId.set(cartItemId);
    this.errorMessage.set(null);
    try {
      this.cart.set(await this.cartService.updateItem(cartItemId, { quantity }));
      this.localQuantities.delete(cartItemId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.updatingItemId.set(null);
    }
  }

  protected async removeItem(cartItemId: string): Promise<void> {
    this.updatingItemId.set(cartItemId);
    this.errorMessage.set(null);
    try {
      this.cart.set(await this.cartService.removeItem(cartItemId));
      this.localQuantities.delete(cartItemId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.updatingItemId.set(null);
    }
  }

  protected async saveForLater(item: CartItemResponse): Promise<void> {
    this.savingItemId.set(item.cartItemId);
    this.errorMessage.set(null);
    try {
      this.cart.set(await this.cartService.moveItemToWishlist(item.cartItemId));
      this.wishlistState.markSaved(item.productId);
      this.localQuantities.delete(item.cartItemId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.savingItemId.set(null);
    }
  }

  private async loadCart(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    try {
      this.cart.set(await this.cartService.getCart());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.cart.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }
}
