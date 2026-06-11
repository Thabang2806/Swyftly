import { TestBed } from '@angular/core/testing';
import { createCart } from '../cart/cart.service.spec';
import { BuyerEngagementService } from './buyer-engagement.service';
import { BuyerWishlistStateService } from './buyer-wishlist-state.service';

describe('BuyerWishlistStateService', () => {
  let service: BuyerWishlistStateService;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;

  beforeEach(() => {
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', [
      'listWishlistProductIds',
      'addWishlistItem',
      'removeWishlistItem',
      'moveWishlistItemToCart'
    ]);
    engagementService.listWishlistProductIds.and.resolveTo({ productIds: ['product-id'] });
    engagementService.addWishlistItem.and.resolveTo({
      wishlistItemId: 'wishlist-id',
      createdAtUtc: '2026-05-19T10:00:00Z',
      product: createProduct(),
      availableVariants: []
    });
    engagementService.removeWishlistItem.and.resolveTo();
    engagementService.moveWishlistItemToCart.and.resolveTo(createCart());

    TestBed.configureTestingModule({
      providers: [
        { provide: BuyerEngagementService, useValue: engagementService }
      ]
    });

    service = TestBed.inject(BuyerWishlistStateService);
  });

  it('loads and caches wishlist product ids', async () => {
    await service.load();
    await service.load();

    expect(service.isLoaded()).toBeTrue();
    expect(service.isSaved('product-id')).toBeTrue();
    expect(engagementService.listWishlistProductIds).toHaveBeenCalledTimes(1);
  });

  it('allows wishlist product-id hydration to retry after a failed load', async () => {
    engagementService.listWishlistProductIds.calls.reset();
    engagementService.listWishlistProductIds.and.rejectWith(new Error('offline'));

    await expectAsync(service.load()).toBeRejected();

    engagementService.listWishlistProductIds.and.resolveTo({ productIds: ['retry-product-id'] });
    await service.load();

    expect(service.isSaved('retry-product-id')).toBeTrue();
    expect(engagementService.listWishlistProductIds).toHaveBeenCalledTimes(2);
  });

  it('marks products saved and removed after mutations', async () => {
    await service.save('new-product-id');
    expect(service.isSaved('new-product-id')).toBeTrue();

    await service.remove('new-product-id');
    expect(service.isSaved('new-product-id')).toBeFalse();
  });

  it('removes wishlist state after moving a product to cart', async () => {
    service.markSaved('product-id');

    const cart = await service.moveToCart('product-id', {
      productVariantId: 'variant-id',
      quantity: 1
    });

    expect(cart.cartId).toBe('cart-id');
    expect(service.isSaved('product-id')).toBeFalse();
    expect(engagementService.moveWishlistItemToCart).toHaveBeenCalledWith('product-id', {
      productVariantId: 'variant-id',
      quantity: 1
    });
  });
});

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
