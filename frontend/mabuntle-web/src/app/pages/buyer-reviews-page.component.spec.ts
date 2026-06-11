import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerReviewsPageComponent } from './buyer-reviews-page.component';

describe('BuyerReviewsPageComponent', () => {
  let fixture: ComponentFixture<BuyerReviewsPageComponent>;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;

  beforeEach(async () => {
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['listBuyerReviews', 'updateReview', 'deleteReview']);
    engagementService.listBuyerReviews.and.resolveTo([createReview({ status: 'Rejected', moderationReason: 'Please remove personal information.' })]);
    engagementService.updateReview.and.resolveTo({ ...createReview(), rating: 4, title: 'Updated fit', status: 'PendingReview' });
    engagementService.deleteReview.and.resolveTo();

    await TestBed.configureTestingModule({
      imports: [BuyerReviewsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerEngagementService, useValue: engagementService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerReviewsPageComponent);
  });

  it('renders reviews and updates an edited review', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Great fit');
    expect(compiled.textContent).toContain('Please remove personal information.');

    const editButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Edit')) as HTMLButtonElement;
    editButton.click();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      reviewForm: { patchValue: (value: unknown) => void };
      saveReview: (review: unknown) => Promise<void>;
      reviews: () => unknown[];
    };
    component.reviewForm.patchValue({ rating: 4, title: 'Updated fit', body: 'Still good.' });
    await component.saveReview(component.reviews()[0]);

    expect(engagementService.updateReview).toHaveBeenCalledWith('review-id', {
      rating: 4,
      title: 'Updated fit',
      body: 'Still good.'
    });
  });
});

function createReview(overrides = {}) {
  return {
    reviewId: 'review-id',
    productId: 'product-id',
    orderId: 'order-id',
    orderItemId: 'order-item-id',
    rating: 5,
    title: 'Great fit',
    body: 'Loved it.',
    status: 'Published',
    moderationReason: null,
    moderatedAtUtc: null,
    createdAtUtc: '2026-05-19T10:00:00Z',
    updatedAtUtc: '2026-05-19T10:00:00Z',
    product: {
      productId: 'product-id',
      sellerId: 'seller-id',
      title: 'Summer Dress',
      slug: 'summer-dress',
      primaryImageUrl: null,
      primaryImageAltText: null
    },
    ...overrides
  };
}
