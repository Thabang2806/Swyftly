import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminProductReviewDetailResponse } from '../admin/admin-review.models';
import { AdminReviewService } from '../admin/admin-review.service';
import { AdminReviewsPageComponent } from './admin-reviews-page.component';

describe('AdminReviewsPageComponent', () => {
  let fixture: ComponentFixture<AdminReviewsPageComponent>;
  let reviewService: jasmine.SpyObj<AdminReviewService>;

  beforeEach(async () => {
    reviewService = jasmine.createSpyObj<AdminReviewService>('AdminReviewService', [
      'getPendingReviews',
      'approveReview',
      'rejectReview',
      'removeReview'
    ]);
    reviewService.getPendingReviews.and.resolveTo([createReview()]);
    reviewService.approveReview.and.resolveTo(createReview({ status: 'Published' }));
    reviewService.rejectReview.and.resolveTo(createReview({ status: 'Rejected', moderationReason: 'Personal information.' }));
    reviewService.removeReview.and.resolveTo(createReview({ status: 'Removed', moderationReason: 'Policy issue.' }));

    await TestBed.configureTestingModule({
      imports: [AdminReviewsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminReviewService, useValue: reviewService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminReviewsPageComponent);
  });

  it('renders pending review context and filters the queue', async () => {
    reviewService.getPendingReviews.and.resolveTo([
      createReview(),
      createReview({
        reviewId: 'other-review-id',
        title: 'Wrong shade',
        product: { title: 'Canvas Sneakers', slug: 'canvas-sneakers', categoryId: null, primaryImageUrl: null, primaryImageAltText: null },
        seller: { displayName: 'Second Seller', contactEmail: 'second@example.test', verificationStatus: 'Verified' }
      })
    ]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Great fit');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.querySelector('.hf-admin-evidence-panel')).toBeTruthy();
    expect(compiled.textContent).toContain('Selected review');

    const searchInput = compiled.querySelector('input[formControlName="search"]') as HTMLInputElement;
    searchInput.value = 'Canvas';
    searchInput.dispatchEvent(new Event('input'));
    compiled.querySelector('form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const tableText = compiled.querySelector('.admin-table')?.textContent ?? '';
    expect(tableText).toContain('Canvas Sneakers');
    expect(tableText).not.toContain('Seller Store');
  });

  it('approves and rejects selected reviews', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      approveSelected: () => Promise<void>;
      rejectSelected: () => Promise<void>;
      reasonForm: { setValue: (value: { reason: string }) => void };
    };

    await component.approveSelected();
    expect(reviewService.approveReview).toHaveBeenCalledWith('review-id');

    reviewService.getPendingReviews.and.resolveTo([createReview()]);
    await (fixture.componentInstance as unknown as { ngOnInit: () => Promise<void> }).ngOnInit();
    component.reasonForm.setValue({ reason: 'Personal information.' });
    await component.rejectSelected();
    expect(reviewService.rejectReview).toHaveBeenCalledWith('review-id', { reason: 'Personal information.' });
  });

  it('shows an empty state when no reviews are pending', async () => {
    reviewService.getPendingReviews.and.resolveTo([]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No reviews pending moderation');
  });
});

function createReview(overrides: Partial<AdminProductReviewDetailResponse> = {}): AdminProductReviewDetailResponse {
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
