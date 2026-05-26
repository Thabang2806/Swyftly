import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { AuthService } from '../auth/auth.service';
import { PublicProductReviewResponse, PublicProductReviewSummaryResponse } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerWishlistStateService } from '../buyer/buyer-wishlist-state.service';
import { CartService } from '../cart/cart.service';
import { PublicProductDetailResponse, PublicProductImageResponse } from '../shop/public-catalog.models';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-product-detail-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    FormsModule,
    LuxuryPublicStylesComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    ProductVisualFallbackComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <app-luxury-public-styles />
    <section class="page product-detail-page">
      <a class="admin-back-link" routerLink="/shop">Back to shop</a>

      @if (isLoading()) {
        <div class="route-card">Loading product...</div>
      } @else if (productDetail()) {
        <div class="product-detail-layout hf-product-detail-layout">
          <div class="product-gallery-shell">
            @if (galleryImages().length > 1) {
              <div class="product-thumbnail-row product-thumbnail-rail" aria-label="Product images">
                @for (image of galleryImages(); track image.imageId) {
                  <button
                    type="button"
                    [class.active]="selectedImageId() === image.imageId"
                    (click)="selectImage(image.imageId)"
                  >
                    <img [src]="image.url" [alt]="image.altText ?? productDetail()?.product?.title ?? 'Product thumbnail'">
                  </button>
                }
              </div>
            }

            <div class="product-gallery product-main-gallery">
              @if (selectedImage()) {
                <img [src]="selectedImage()!.url" [alt]="selectedImage()!.altText ?? productDetail()?.product?.title ?? 'Product image'">
              } @else if (primaryImageUrl()) {
                <img [src]="primaryImageUrl()!" [alt]="productDetail()?.product?.primaryImageAltText ?? productDetail()?.product?.title ?? 'Product image'">
              } @else {
                <div class="product-gallery-placeholder">
                  <app-product-visual-fallback
                    [label]="productDetail()?.product?.categoryPath ?? 'Swyftly product'"
                    [title]="productDetail()?.product?.title ?? 'Product'"
                    [tone]="visualTone()"
                  />
                </div>
              }
            </div>
          </div>

          <div class="product-detail-info product-purchase-panel">
            <div class="product-detail-topline">
              <app-status-badge label="Published listing" tone="success" />
              @if (reviewSummary() && reviewSummary()!.reviewCount > 0) {
                <span>{{ reviewSummary()!.averageRating }}/5 from {{ reviewSummary()!.reviewCount }} review{{ reviewSummary()!.reviewCount === 1 ? '' : 's' }}</span>
              }
            </div>

            <app-status-badge [label]="productDetail()?.product?.categoryPath ?? 'Swyftly product'" tone="accent" />
            <h1>{{ productDetail()?.product?.title ?? 'Untitled product' }}</h1>

            <div class="product-detail-trust-row">
              <app-status-badge [label]="productDetail()?.product?.inStock ? 'In stock' : 'Out of stock'" [tone]="productDetail()?.product?.inStock ? 'success' : 'warning'" />
              <span>{{ availableVariantCount() }} available variant{{ availableVariantCount() === 1 ? '' : 's' }}</span>
            </div>

            @if (productDetail()?.product?.sellerStoreSlug) {
              <a class="product-card-seller" [routerLink]="['/seller', productDetail()?.product?.sellerStoreSlug]">
                Sold by {{ productDetail()?.product?.sellerStoreName ?? 'Seller' }}
              </a>
            }

            <div class="product-detail-price">
              <strong>{{ productDetail()?.product?.priceMin | currency:'ZAR':'symbol-narrow' }}</strong>
              @if (productDetail()?.product?.compareAtPriceMin) {
                <span>{{ productDetail()?.product?.compareAtPriceMin | currency:'ZAR':'symbol-narrow' }}</span>
                <app-status-badge label="Compare price" tone="warning" />
              }
            </div>

            <p>{{ productDetail()?.fullDescription ?? productDetail()?.product?.shortDescription }}</p>

            <div class="product-trust-grid">
              <div>
                <strong>Seller visibility</strong>
                <span>Open the seller storefront before buying to review the shop and its published products.</span>
              </div>
              <div>
                <strong>Shipping and returns</strong>
                <span>Delivery and return handling are kept visible through checkout and order support.</span>
              </div>
              <div>
                <strong>Secure checkout path</strong>
                <span>Cart and checkout keep seller, quantity, and stock context visible.</span>
              </div>
              <div>
                <strong>Reviewed marketplace listings</strong>
                <span>Published products come through seller and product review workflows.</span>
              </div>
            </div>

            @if (addToCartMessage()) {
              <app-ui-alert tone="success">{{ addToCartMessage() }}</app-ui-alert>
            }

            @if (addToCartError()) {
              <app-ui-alert tone="error">{{ addToCartError() }}</app-ui-alert>
            }

            @if (wishlistMessage()) {
              <app-ui-alert tone="success">{{ wishlistMessage() }}</app-ui-alert>
            }

            @if (wishlistError()) {
              <app-ui-alert tone="error">{{ wishlistError() }}</app-ui-alert>
            }

            <section class="product-purchase-section">
              <div class="product-section-heading product-section-heading--compact">
                <span>
                  <h2>Choose a variant</h2>
                  <p>{{ selectedVariant() ? 'Selected: ' + selectedVariant()!.size + ' / ' + selectedVariant()!.colour : 'Select an available size and colour.' }}</p>
                </span>
              </div>

              <div class="product-variant-picker" aria-label="Product variants">
                @for (variant of productDetail()?.variants; track variant.variantId) {
                  <button
                    type="button"
                    class="variant-option"
                    [class.active]="selectedVariantId() === variant.variantId"
                    [class.out-of-stock]="!variant.inStock"
                    [disabled]="!variant.inStock"
                    (click)="selectVariant(variant.variantId)"
                  >
                    <strong>{{ variant.size }}</strong>
                    <span>{{ variant.colour }}</span>
                    <small>{{ variant.price | currency:'ZAR':'symbol-narrow' }}</small>
                  </button>
                }
              </div>
            </section>

            <div class="product-detail-actions product-purchase-actions">
              <mat-form-field class="swyftly-field swyftly-field--compact" appearance="outline">
                <mat-label>Qty</mat-label>
                <input matInput type="number" min="1" [(ngModel)]="quantity">
              </mat-form-field>

              <button mat-flat-button type="button" [disabled]="isAddingToCart() || !selectedVariantId()" (click)="addToCart()">
                {{ isAddingToCart() ? 'Adding...' : 'Add to cart' }}
              </button>
              <button mat-stroked-button type="button" [disabled]="isSavingWishlist()" (click)="toggleWishlist()">
                {{ isSavingWishlist() ? 'Saving...' : isWishlisted() ? 'Remove from wishlist' : 'Save to wishlist' }}
              </button>
            </div>

            <section class="complete-look-panel">
              <h2>Complete the look</h2>
              <p>Styling prompts only. Add matching products from their own product pages.</p>
              <div class="complete-look-grid">
                @for (item of completeLookItems; track item.title) {
                  <span>
                    <strong>{{ item.title }}</strong>
                    <small>{{ item.detail }}</small>
                  </span>
                }
              </div>
            </section>

            <section>
              <h2>Variant details</h2>
              <div class="product-option-list product-variant-grid">
                @for (variant of productDetail()?.variants; track variant.variantId) {
                  <span [class.out-of-stock]="!variant.inStock">
                    <strong>{{ variant.size }} / {{ variant.colour }}</strong>
                    {{ variant.price | currency:'ZAR':'symbol-narrow' }}
                    @if (variant.compareAtPrice) {
                      <small>{{ variant.compareAtPrice | currency:'ZAR':'symbol-narrow' }}</small>
                    }
                    {{ variant.inStock ? 'Available' : 'Out of stock' }}
                  </span>
                }
              </div>
            </section>

            <section>
              <h2>Details</h2>
              <dl class="admin-facts">
                @for (attribute of attributeEntries(); track attribute.key) {
                  <div>
                    <dt>{{ attribute.key }}</dt>
                    <dd>{{ attribute.value }}</dd>
                  </div>
                } @empty {
                  <div>
                    <dt>Product details</dt>
                    <dd>Additional product attributes will appear here when the seller provides them.</dd>
                  </div>
                }
              </dl>
            </section>

            <section class="product-reviews-panel">
              <div class="product-section-heading">
                <span>
                  <h2>Buyer reviews</h2>
                  <p>Verified-purchase reviews from delivered Swyftly orders.</p>
                </span>
                @if (reviewSummary()) {
                  <strong>{{ reviewSummary()!.averageRating }}/5</strong>
                  <small>{{ reviewSummary()!.reviewCount }} review{{ reviewSummary()!.reviewCount === 1 ? '' : 's' }}</small>
                }
              </div>

              @if (reviewsError()) {
                <app-ui-alert tone="error">{{ reviewsError() }}</app-ui-alert>
              } @else if (!reviewSummary() || reviewSummary()!.reviewCount === 0) {
                <app-empty-state
                  eyebrow="Reviews"
                  heading="No reviews yet"
                  message="Reviews will appear after buyers receive orders and leave feedback."
                />
              } @else {
                <div class="product-rating-breakdown">
                  @for (count of ratingCountsDescending(); track count.rating) {
                    <div>
                      <span>{{ count.rating }}/5</span>
                      <i><b [style.width.%]="ratingShare(count.count)"></b></i>
                      <small>{{ count.count }}</small>
                    </div>
                  }
                </div>

                <div class="product-review-list">
                  @for (review of reviews(); track review.reviewId) {
                    <article class="product-review-card">
                      <div>
                        <strong>{{ review.rating }}/5</strong>
                        <small>{{ review.createdAtUtc | date:'mediumDate' }}</small>
                      </div>
                      @if (review.title) {
                        <h3>{{ review.title }}</h3>
                      }
                      @if (review.body) {
                        <p>{{ review.body }}</p>
                      }
                    </article>
                  }
                </div>
              }
            </section>
          </div>
        </div>
      } @else {
        <app-empty-state
          eyebrow="Not found"
          heading="Product was not found"
          [message]="errorMessage() ?? 'This product may no longer be available.'"
        >
          <a mat-flat-button routerLink="/shop">Back to shop</a>
        </app-empty-state>
      }
    </section>
  `
})
export class ProductDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly wishlistState = inject(BuyerWishlistStateService);
  private readonly cartService = inject(CartService);
  private readonly publicCatalogService = inject(PublicCatalogService);
  private readonly router = inject(Router);

  protected readonly productDetail = signal<PublicProductDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isAddingToCart = signal(false);
  protected readonly addToCartMessage = signal<string | null>(null);
  protected readonly addToCartError = signal<string | null>(null);
  protected readonly wishlistMessage = signal<string | null>(null);
  protected readonly wishlistError = signal<string | null>(null);
  protected readonly isSavingWishlist = signal(false);
  protected readonly isWishlisted = signal(false);
  protected readonly reviewSummary = signal<PublicProductReviewSummaryResponse | null>(null);
  protected readonly reviews = signal<PublicProductReviewResponse[]>([]);
  protected readonly reviewsError = signal<string | null>(null);
  protected readonly selectedImageId = signal<string | null>(null);
  protected readonly selectedVariantId = signal('');
  protected quantity = 1;
  protected readonly completeLookItems = [
    { title: 'Jewellery finish', detail: 'Search hoops, rings, and necklaces' },
    { title: 'Bag pairing', detail: 'Search clutches and shoulder bags' },
    { title: 'Shoe styling', detail: 'Search heels, sandals, and sneakers' }
  ];
  protected readonly galleryImages = computed(() => {
    const images = this.productDetail()?.images ?? [];
    return [...images].sort((left, right) => Number(right.isPrimary) - Number(left.isPrimary));
  });
  protected readonly selectedImage = computed<PublicProductImageResponse | null>(() => {
    const images = this.galleryImages();
    return images.find(image => image.imageId === this.selectedImageId()) ?? images[0] ?? null;
  });
  protected readonly primaryImageUrl = computed(() =>
    this.selectedImage()?.url ??
    this.productDetail()?.product.primaryImageUrl ??
    null);
  protected readonly availableVariantCount = computed(() =>
    this.productDetail()?.variants.filter(variant => variant.inStock).length ?? 0);
  protected readonly selectedVariant = computed(() =>
    this.productDetail()?.variants.find(variant => variant.variantId === this.selectedVariantId()) ?? null);
  protected readonly ratingCountsDescending = computed(() =>
    [...(this.reviewSummary()?.ratingCounts ?? [])].sort((left, right) => right.rating - left.rating));
  protected readonly attributeEntries = computed(() => {
    const attributes = this.productDetail()?.attributes ?? {};
    return Object.entries(attributes).map(([key, value]) => ({
      key,
      value: this.formatAttributeValue(value)
    }));
  });

  async ngOnInit(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get('slug');
    if (!slug) {
      this.errorMessage.set('Product slug is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const detail = await this.publicCatalogService.getProduct(slug);
      this.productDetail.set(detail);
      const primaryImage = detail.images.find(image => image.isPrimary) ?? detail.images[0] ?? null;
      this.selectedImageId.set(primaryImage?.imageId ?? null);
      this.selectedVariantId.set(detail.variants.find(variant => variant.inStock)?.variantId ?? '');
      await this.initializeWishlistState(detail.product.productId);
      await this.loadReviews(slug);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.productDetail.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected selectImage(imageId: string): void {
    this.selectedImageId.set(imageId);
  }

  protected selectVariant(variantId: string): void {
    const variant = this.productDetail()?.variants.find(item => item.variantId === variantId);
    if (variant?.inStock) {
      this.selectedVariantId.set(variantId);
    }
  }

  protected visualTone(): ProductVisualTone {
    const product = this.productDetail()?.product;
    const text = [
      product?.title,
      product?.categoryPath,
      product?.shortDescription,
      ...(product?.tags ?? [])
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
  }

  protected async addToCart(): Promise<void> {
    this.addToCartMessage.set(null);
    this.addToCartError.set(null);

    await this.authService.initialize();
    if (!this.authService.hasAnyRole(['Buyer'])) {
      await this.router.navigate(['/login'], {
        queryParams: { returnUrl: this.router.url }
      });
      return;
    }

    if (!this.selectedVariantId() || this.quantity <= 0) {
      this.addToCartError.set('Choose an available variant and quantity.');
      return;
    }

    this.isAddingToCart.set(true);
    try {
      await this.cartService.addItem({
        productVariantId: this.selectedVariantId(),
        quantity: this.quantity
      });
      this.addToCartMessage.set('Added to cart.');
    } catch (error) {
      this.addToCartError.set(getApiErrorMessage(error));
    } finally {
      this.isAddingToCart.set(false);
    }
  }

  protected async toggleWishlist(): Promise<void> {
    this.wishlistMessage.set(null);
    this.wishlistError.set(null);

    await this.authService.initialize();
    if (!this.authService.hasAnyRole(['Buyer'])) {
      await this.router.navigate(['/login'], {
        queryParams: { returnUrl: this.router.url }
      });
      return;
    }

    const productId = this.productDetail()?.product.productId;
    if (!productId || this.isSavingWishlist()) {
      return;
    }

    this.isSavingWishlist.set(true);
    try {
      if (this.isWishlisted()) {
        await this.wishlistState.remove(productId);
        this.isWishlisted.set(false);
        this.wishlistMessage.set('Removed from wishlist.');
      } else {
        await this.wishlistState.save(productId);
        this.isWishlisted.set(true);
        this.wishlistMessage.set('Saved to wishlist.');
      }
    } catch (error) {
      this.wishlistError.set(getApiErrorMessage(error));
    } finally {
      this.isSavingWishlist.set(false);
    }
  }

  protected ratingShare(count: number): number {
    const total = this.reviewSummary()?.reviewCount ?? 0;
    return total === 0 ? 0 : Math.round((count / total) * 100);
  }

  private async loadReviews(slug: string): Promise<void> {
    this.reviewsError.set(null);

    try {
      const [summary, reviews] = await Promise.all([
        this.engagementService.getProductReviewSummary(slug),
        this.engagementService.listProductReviews(slug)
      ]);
      this.reviewSummary.set(summary);
      this.reviews.set(reviews);
    } catch (error) {
      this.reviewsError.set(getApiErrorMessage(error));
      this.reviewSummary.set(null);
      this.reviews.set([]);
    }
  }

  private async initializeWishlistState(productId: string): Promise<void> {
    try {
      await this.authService.initialize();
      if (!this.authService.hasAnyRole(['Buyer'])) {
        return;
      }

      await this.wishlistState.load();
      this.isWishlisted.set(this.wishlistState.isSaved(productId));
    } catch {
      this.isWishlisted.set(false);
    }
  }

  private formatAttributeValue(valueJson: string): string {
    try {
      const parsed = JSON.parse(valueJson) as unknown;
      if (Array.isArray(parsed)) {
        return parsed.join(', ');
      }

      return String(parsed ?? '');
    } catch {
      return valueJson;
    }
  }
}
