import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerWishlistStateService } from '../buyer/buyer-wishlist-state.service';
import { CartService } from '../cart/cart.service';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { ProductDetailPageComponent } from './product-detail-page.component';
import { createProduct } from './shop-page.component.spec';

describe('ProductDetailPageComponent', () => {
  let fixture: ComponentFixture<ProductDetailPageComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;
  let wishlistState: jasmine.SpyObj<BuyerWishlistStateService>;
  let cartService: jasmine.SpyObj<CartService>;
  let publicCatalogService: jasmine.SpyObj<PublicCatalogService>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['initialize', 'hasAnyRole']);
    authService.initialize.and.resolveTo();
    authService.hasAnyRole.and.returnValue(true);
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['addWishlistItem', 'getProductReviewSummary', 'listProductReviews']);
    engagementService.addWishlistItem.and.resolveTo({
      wishlistItemId: 'wishlist-id',
      createdAtUtc: '2026-05-19T10:00:00Z',
      product: createProduct(),
      availableVariants: []
    });
    wishlistState = jasmine.createSpyObj<BuyerWishlistStateService>('BuyerWishlistStateService', ['load', 'isSaved', 'save', 'remove']);
    wishlistState.load.and.resolveTo();
    wishlistState.isSaved.and.returnValue(false);
    wishlistState.save.and.resolveTo({
      wishlistItemId: 'wishlist-id',
      createdAtUtc: '2026-05-19T10:00:00Z',
      product: createProduct(),
      availableVariants: []
    });
    wishlistState.remove.and.resolveTo();
    engagementService.getProductReviewSummary.and.resolveTo({
      productId: 'product-id',
      reviewCount: 1,
      averageRating: 5,
      ratingCounts: [
        { rating: 1, count: 0 },
        { rating: 2, count: 0 },
        { rating: 3, count: 0 },
        { rating: 4, count: 0 },
        { rating: 5, count: 1 }
      ]
    });
    engagementService.listProductReviews.and.resolveTo([{
      reviewId: 'review-id',
      productId: 'product-id',
      rating: 5,
      title: 'Great fit',
      body: 'Loved the fabric.',
      createdAtUtc: '2026-05-19T10:00:00Z'
    }]);
    cartService = jasmine.createSpyObj<CartService>('CartService', ['addItem']);
    cartService.addItem.and.resolveTo({
      cartId: 'cart-id',
      buyerId: 'buyer-id',
      sellerId: 'seller-id',
      sellerStoreName: 'Seller Store',
      items: [],
      totalQuantity: 1,
      subtotal: 499
    });
    publicCatalogService = jasmine.createSpyObj<PublicCatalogService>('PublicCatalogService', ['getProduct']);
    publicCatalogService.getProduct.and.resolveTo({
      product: createProduct(),
      fullDescription: 'A full product description.',
      attributes: {
        material: '"Cotton"'
      },
      images: [{
        imageId: 'image-id',
        url: 'https://example.test/summer-dress.jpg',
        altText: 'Summer dress',
        isPrimary: true
      }, {
        imageId: 'image-id-2',
        url: 'https://example.test/summer-dress-detail.jpg',
        altText: 'Summer dress detail',
        isPrimary: false
      }],
      variants: [{
        variantId: 'variant-id',
        size: 'M',
        colour: 'Black',
        price: 499,
        compareAtPrice: 599,
        inStock: true
      }]
    });

    await TestBed.configureTestingModule({
      imports: [ProductDetailPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ slug: 'summer-dress' })
            }
          }
        },
        { provide: AuthService, useValue: authService },
        { provide: BuyerEngagementService, useValue: engagementService },
        { provide: BuyerWishlistStateService, useValue: wishlistState },
        { provide: CartService, useValue: cartService },
        { provide: PublicCatalogService, useValue: publicCatalogService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductDetailPageComponent);
  });

  it('loads product detail by slug', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(publicCatalogService.getProduct).toHaveBeenCalledWith('summer-dress');
    expect(wishlistState.load).toHaveBeenCalled();
    expect(wishlistState.isSaved).toHaveBeenCalledWith('product-id');
    expect(engagementService.getProductReviewSummary).toHaveBeenCalledWith('summer-dress');
    expect(engagementService.listProductReviews).toHaveBeenCalledWith('summer-dress');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('A full product description.');
    expect(compiled.textContent).toContain('Cotton');
    expect(compiled.textContent).toContain('Great fit');
    expect(compiled.querySelector('.product-gallery-shell')).not.toBeNull();
    expect(compiled.querySelector('.product-purchase-panel')).not.toBeNull();
    expect(compiled.querySelector('.variant-option.active')?.textContent).toContain('M');
    expect(compiled.textContent).toContain('Complete the look');
  });

  it('adds the selected variant to cart', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(element => element.textContent?.includes('Add to cart')) as HTMLButtonElement;
    button.click();
    await fixture.whenStable();

    expect(cartService.addItem).toHaveBeenCalledWith({
      productVariantId: 'variant-id',
      quantity: 1
    });
  });

  it('saves the product to wishlist', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(element => element.textContent?.includes('Save to wishlist')) as HTMLButtonElement;
    button.click();
    await fixture.whenStable();

    expect(wishlistState.save).toHaveBeenCalledWith('product-id');
  });

  it('switches the selected gallery image', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const thumbnailButtons = (fixture.nativeElement as HTMLElement).querySelectorAll('.product-thumbnail-row button');
    expect(thumbnailButtons.length).toBe(2);

    (thumbnailButtons[1] as HTMLButtonElement).click();
    fixture.detectChanges();

    const mainImage = (fixture.nativeElement as HTMLElement).querySelector('.product-gallery img') as HTMLImageElement;
    expect(mainImage.src).toContain('summer-dress-detail.jpg');
  });

  it('renders a gallery fallback when product images are unavailable', async () => {
    publicCatalogService.getProduct.and.resolveTo({
      product: {
        ...createProduct(),
        primaryImageUrl: null,
        primaryImageAltText: null
      },
      fullDescription: 'A full product description.',
      attributes: {},
      images: [],
      variants: [{
        variantId: 'variant-id',
        size: 'M',
        colour: 'Black',
        price: 499,
        compareAtPrice: null,
        inStock: true
      }]
    });

    fixture = TestBed.createComponent(ProductDetailPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.product-gallery-placeholder')).not.toBeNull();
    expect(compiled.querySelector('.product-gallery-placeholder .hf-product-visual')).not.toBeNull();
    expect(compiled.textContent).toContain('Summer Dress');
  });
});
