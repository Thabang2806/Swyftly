import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerWishlistStateService } from '../buyer/buyer-wishlist-state.service';
import { BuyerWishlistPageComponent } from './buyer-wishlist-page.component';

describe('BuyerWishlistPageComponent', () => {
  let fixture: ComponentFixture<BuyerWishlistPageComponent>;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;
  let wishlistState: jasmine.SpyObj<BuyerWishlistStateService>;

  beforeEach(async () => {
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['listWishlist', 'removeWishlistItem']);
    wishlistState = jasmine.createSpyObj<BuyerWishlistStateService>('BuyerWishlistStateService', ['markSaved', 'markRemoved', 'moveToCart']);
    engagementService.listWishlist.and.resolveTo([createWishlistItem()]);
    engagementService.removeWishlistItem.and.resolveTo();
    wishlistState.moveToCart.and.resolveTo({
      cartId: 'cart-id',
      buyerId: 'buyer-id',
      sellerId: 'seller-id',
      sellerStoreName: 'Seller Store',
      items: [],
      totalQuantity: 1,
      subtotal: 499
    });

    await TestBed.configureTestingModule({
      imports: [BuyerWishlistPageComponent],
      providers: [
        provideRouter([]),
        { provide: BuyerEngagementService, useValue: engagementService },
        { provide: BuyerWishlistStateService, useValue: wishlistState }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerWishlistPageComponent);
  });

  it('loads and removes wishlist items', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');

    const removeButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Remove')) as HTMLButtonElement;
    removeButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(engagementService.removeWishlistItem).toHaveBeenCalledWith('product-id');
    expect(wishlistState.markRemoved).toHaveBeenCalledWith('product-id');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No saved products yet');
  });

  it('moves a wishlist item to cart with the selected variant and quantity', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const quantityInput = (fixture.nativeElement as HTMLElement).querySelector('input[type="number"]') as HTMLInputElement;
    quantityInput.value = '2';
    quantityInput.dispatchEvent(new Event('input'));

    const moveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Move to cart')) as HTMLButtonElement;
    moveButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(wishlistState.moveToCart).toHaveBeenCalledWith('product-id', {
      productVariantId: 'variant-id',
      quantity: 2
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No saved products yet');
  });
});

function createWishlistItem() {
  return {
    wishlistItemId: 'wishlist-id',
    createdAtUtc: '2026-05-19T10:00:00Z',
    product: createProduct(),
    availableVariants: [{
      productVariantId: 'variant-id',
      size: 'M',
      colour: 'Black',
      price: 499,
      compareAtPrice: null,
      inStock: true,
      availableQuantity: 4
    }]
  };
}

function createProduct() {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerStoreName: 'Seller Store',
    sellerStoreSlug: 'seller-store',
    categoryId: null,
    categoryPath: 'Women > Dresses',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: null,
    primaryImageUrl: null,
    primaryImageAltText: null,
    priceMin: 499,
    compareAtPriceMin: null,
    inStock: true,
    tags: [],
    publishedAtUtc: '2026-05-19T10:00:00Z'
  };
}
