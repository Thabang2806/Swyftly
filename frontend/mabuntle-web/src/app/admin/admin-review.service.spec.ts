import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminReviewService } from './admin-review.service';

describe('AdminReviewService', () => {
  let service: AdminReviewService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminReviewService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls moderation endpoints', async () => {
    const listPromise = service.getPendingReviews();
    const listRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/reviews/pending`);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);
    await expectAsync(listPromise).toBeResolvedTo([]);

    const approvePromise = service.approveReview('review-id');
    const approveRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/reviews/review-id/approve`);
    expect(approveRequest.request.method).toBe('POST');
    approveRequest.flush(createReview({ status: 'Published' }));
    await expectAsync(approvePromise).toBeResolved();

    const rejectPromise = service.rejectReview('review-id', { reason: 'Personal information.' });
    const rejectRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/reviews/review-id/reject`);
    expect(rejectRequest.request.method).toBe('POST');
    expect(rejectRequest.request.body).toEqual({ reason: 'Personal information.' });
    rejectRequest.flush(createReview({ status: 'Rejected' }));
    await expectAsync(rejectPromise).toBeResolved();
  });
});

function createReview(overrides = {}) {
  return {
    reviewId: 'review-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    productId: 'product-id',
    orderId: 'order-id',
    orderItemId: 'order-item-id',
    rating: 5,
    title: 'Great fit',
    body: 'Loved it.',
    status: 'PendingReview',
    moderationReason: null,
    moderatedByUserId: null,
    moderatedAtUtc: null,
    createdAtUtc: '2026-05-19T10:00:00Z',
    updatedAtUtc: '2026-05-19T10:00:00Z',
    product: { title: 'Summer Dress', slug: 'summer-dress', categoryId: null, primaryImageUrl: null, primaryImageAltText: null },
    seller: { displayName: 'Seller Store', contactEmail: 'seller@example.test', verificationStatus: 'Verified' },
    buyer: { userId: 'buyer-user-id' },
    order: { status: 'Delivered', totalAmount: 499, productTitle: 'Summer Dress', sku: 'SKU-1', size: 'M', colour: 'Black', quantity: 1 },
    auditTrail: [],
    ...overrides
  };
}
