import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminModerationQueueService } from '../admin/admin-moderation-queue.service';
import { AdminPagedResponse } from '../admin/admin-operational-list.models';
import { AdminProductModerationItemResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { AdminQueueTriageService } from '../admin/admin-queue-triage.service';
import { AdminProductsPageComponent } from './admin-products-page.component';

describe('AdminProductsPageComponent', () => {
  let fixture: ComponentFixture<AdminProductsPageComponent>;
  let adminProductService: jasmine.SpyObj<AdminProductService>;
  let adminModerationQueueService: jasmine.SpyObj<AdminModerationQueueService>;

  beforeEach(async () => {
    adminProductService = jasmine.createSpyObj<AdminProductService>('AdminProductService', ['getModerationItems']);
    adminProductService.getModerationItems.and.resolveTo(createPagedResponse([createProductItem()]));
    adminModerationQueueService = jasmine.createSpyObj<AdminModerationQueueService>('AdminModerationQueueService', ['getSavedViews', 'getSummary', 'createSavedView', 'updateSavedView', 'deleteSavedView', 'makeDefault']);
    adminModerationQueueService.getSavedViews.and.resolveTo([]);
    adminModerationQueueService.getSummary.and.resolveTo(createQueueSummary());

    await TestBed.configureTestingModule({
      imports: [AdminProductsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminModerationQueueService, useValue: adminModerationQueueService },
        { provide: AdminQueueTriageService, useValue: jasmine.createSpyObj<AdminQueueTriageService>('AdminQueueTriageService', ['bulkTriage', 'claim', 'unclaim', 'updateTriage']) },
        { provide: AdminProductService, useValue: adminProductService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminProductsPageComponent);
  });

  it('loads and displays product moderation records', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(adminProductService.getModerationItems).toHaveBeenCalledWith(jasmine.objectContaining({ view: 'NeedsAttention' }));
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('PendingReview');
    expect(compiled.textContent).toContain('1 risk flag');
    expect(compiled.querySelector('.hf-admin-review-layout')).toBeTruthy();
    expect(compiled.textContent).toContain('Selected moderation item');
    const reviewLink = Array.from(compiled.querySelectorAll('a'))
      .find(link => link.getAttribute('href') === '/admin/products/product-id');
    expect(reviewLink).toBeTruthy();
  });

  it('renders revision records with their existing detail routes', async () => {
    adminProductService.getModerationItems.and.resolveTo(createPagedResponse([
      createProductItem({
        id: 'variant-revision-id',
        itemType: 'VariantRevision',
        revisionId: 'variant-revision-id',
        riskFlagCount: 0,
        itemCount: 2,
        detailRoute: '/admin/products/variant-revisions/variant-revision-id'
      })
    ]));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Variant revision');
    expect(compiled.textContent).toContain('2 staged items');
    const reviewLink = Array.from(compiled.querySelectorAll('a'))
      .find(link => link.getAttribute('href') === '/admin/products/variant-revisions/variant-revision-id');
    expect(reviewLink).toBeTruthy();
  });

  it('requests all moderation items when the all-state view is selected', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const allButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('All moderation items')) as HTMLButtonElement;
    allButton.click();
    await fixture.whenStable();

    expect(adminProductService.getModerationItems).toHaveBeenCalledWith(jasmine.objectContaining({ view: 'All' }));
  });

  it('sends status filters to the moderation list endpoint', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const statusInput = compiled.querySelector('input[formControlName="status"]') as HTMLInputElement;
    statusInput.value = 'Published';
    statusInput.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(adminProductService.getModerationItems).toHaveBeenCalledWith(jasmine.objectContaining({ status: 'Published' }));
  });

  it('shows an empty state when there are no matching moderation items', async () => {
    adminProductService.getModerationItems.and.resolveTo(createPagedResponse([]));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No product moderation items found');
  });
});

function createPagedResponse(
  items: AdminProductModerationItemResponse[]
): AdminPagedResponse<AdminProductModerationItemResponse> {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 25,
    statusCounts: [
      { status: 'PendingReview', count: items.filter(item => item.status === 'PendingReview').length },
      { status: 'Published', count: items.filter(item => item.status === 'Published').length }
    ]
  };
}

function createQueueSummary() {
  return {
    generatedAtUtc: '2026-05-27T12:00:00Z',
    itemTypeCounts: [],
    statusCounts: [],
    priorityCounts: [],
    slaCounts: [{ key: 'OnTrack', count: 1 }],
    assigneeCounts: [],
    reviewedToday: 0,
    reviewedLast7Days: 0,
    averageReviewHours: null
  };
}

function createProductItem(overrides: Partial<AdminProductModerationItemResponse> = {}): AdminProductModerationItemResponse {
  return {
    id: 'product-id',
    itemType: 'Product',
    productId: 'product-id',
    revisionId: null,
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    sellerVerificationStatus: 'Verified',
    title: 'Summer Dress',
    categoryPath: 'Women > Clothing > Dresses',
    status: 'PendingReview',
    submittedAtUtc: '2026-05-18T12:00:00Z',
    updatedAtUtc: '2026-05-18T12:00:00Z',
    riskFlagCount: 1,
    itemCount: 0,
    detailRoute: '/admin/products/product-id',
    ...overrides
  };
}
