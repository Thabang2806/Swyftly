import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { BuyerEngagementService } from './buyer-engagement.service';

describe('BuyerEngagementService', () => {
  let service: BuyerEngagementService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BuyerEngagementService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls wishlist endpoints', async () => {
    const listPromise = service.listWishlist();
    const listRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/wishlist`);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);
    await expectAsync(listPromise).toBeResolvedTo([]);

    const productIdsPromise = service.listWishlistProductIds();
    const productIdsRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/wishlist/product-ids`);
    expect(productIdsRequest.request.method).toBe('GET');
    productIdsRequest.flush({ productIds: ['product-id'] });
    await expectAsync(productIdsPromise).toBeResolvedTo({ productIds: ['product-id'] });

    const addPromise = service.addWishlistItem('product-id');
    const addRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/wishlist/product-id`);
    expect(addRequest.request.method).toBe('POST');
    addRequest.flush({ wishlistItemId: 'wishlist-id', createdAtUtc: '2026-05-19T10:00:00Z', product: createProduct(), availableVariants: [] });
    await expectAsync(addPromise).toBeResolved();

    const movePromise = service.moveWishlistItemToCart('product-id', { productVariantId: 'variant-id', quantity: 1 });
    const moveRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/wishlist/product-id/move-to-cart`);
    expect(moveRequest.request.method).toBe('POST');
    expect(moveRequest.request.body).toEqual({ productVariantId: 'variant-id', quantity: 1 });
    moveRequest.flush({ cartId: 'cart-id', buyerId: 'buyer-id', sellerId: 'seller-id', sellerStoreName: 'Seller Store', items: [], totalQuantity: 1, subtotal: 499 });
    await expectAsync(movePromise).toBeResolved();

    const removePromise = service.removeWishlistItem('product-id');
    const removeRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/wishlist/product-id`);
    expect(removeRequest.request.method).toBe('DELETE');
    removeRequest.flush(null);
    await expectAsync(removePromise).toBeResolved();
  });

  it('calls review endpoints', async () => {
    const createPromise = service.createReview('order-id', 'order-item-id', { rating: 5, title: 'Great', body: 'Loved it.' });
    const createRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/orders/order-id/items/order-item-id/review`);
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual({ rating: 5, title: 'Great', body: 'Loved it.' });
    createRequest.flush(createReview());
    await expectAsync(createPromise).toBeResolved();

    const summaryPromise = service.getProductReviewSummary('summer-dress');
    const summaryRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/products/summer-dress/review-summary`);
    expect(summaryRequest.request.method).toBe('GET');
    summaryRequest.flush({ productId: 'product-id', reviewCount: 1, averageRating: 5, ratingCounts: [] });
    await expectAsync(summaryPromise).toBeResolved();
  });

  it('calls notification endpoints', async () => {
    const listPromise = service.listNotifications();
    const listRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/notifications`);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);
    await expectAsync(listPromise).toBeResolvedTo([]);

    const countPromise = service.getUnreadNotificationCount();
    const countRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/notifications/unread-count`);
    expect(countRequest.request.method).toBe('GET');
    countRequest.flush({ unreadCount: 3 });
    await expectAsync(countPromise).toBeResolvedTo({ unreadCount: 3 });

    const readAllPromise = service.markAllNotificationsRead();
    const readAllRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/notifications/read-all`);
    expect(readAllRequest.request.method).toBe('POST');
    readAllRequest.flush({ updatedCount: 2 });
    await expectAsync(readAllPromise).toBeResolvedTo({ updatedCount: 2 });
  });
});

function createReview() {
  return {
    reviewId: 'review-id',
    productId: 'product-id',
    orderId: 'order-id',
    orderItemId: 'order-item-id',
    rating: 5,
    title: 'Great',
    body: 'Loved it.',
    status: 'Published',
    moderationReason: null,
    moderatedAtUtc: null,
    createdAtUtc: '2026-05-19T10:00:00Z',
    updatedAtUtc: '2026-05-19T10:00:00Z',
    product: null
  };
}

function createProduct() {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerStoreName: 'Seller Store',
    sellerStoreSlug: 'seller-store',
    categoryId: null,
    categoryPath: null,
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
