import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminModerationQueueService } from '../admin/admin-moderation-queue.service';
import { AdminPagedResponse } from '../admin/admin-operational-list.models';
import { AdminQueueTriageService } from '../admin/admin-queue-triage.service';
import { AdminSellerOperationalSummaryResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { AdminSellersPageComponent } from './admin-sellers-page.component';

describe('AdminSellersPageComponent', () => {
  let fixture: ComponentFixture<AdminSellersPageComponent>;
  let adminSellerService: jasmine.SpyObj<AdminSellerService>;
  let adminModerationQueueService: jasmine.SpyObj<AdminModerationQueueService>;

  beforeEach(async () => {
    adminSellerService = jasmine.createSpyObj<AdminSellerService>('AdminSellerService', ['getSellers']);
    adminSellerService.getSellers.and.resolveTo(createPagedResponse([createSellerSummary()]));
    adminModerationQueueService = jasmine.createSpyObj<AdminModerationQueueService>('AdminModerationQueueService', ['getSavedViews', 'getSummary', 'createSavedView', 'updateSavedView', 'deleteSavedView', 'makeDefault']);
    adminModerationQueueService.getSavedViews.and.resolveTo([]);
    adminModerationQueueService.getSummary.and.resolveTo(createQueueSummary());

    await TestBed.configureTestingModule({
      imports: [AdminSellersPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminModerationQueueService, useValue: adminModerationQueueService },
        { provide: AdminQueueTriageService, useValue: jasmine.createSpyObj<AdminQueueTriageService>('AdminQueueTriageService', ['bulkTriage', 'claim', 'unclaim', 'updateTriage']) },
        { provide: AdminSellerService, useValue: adminSellerService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSellersPageComponent);
  });

  it('loads and displays seller operational records', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(adminSellerService.getSellers).toHaveBeenCalledWith(jasmine.objectContaining({ view: 'NeedsAttention' }));
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('UnderReview');
    expect(compiled.querySelector('.hf-admin-review-layout')).toBeTruthy();
    expect(compiled.textContent).toContain('Selected seller');
    const reviewLink = Array.from(compiled.querySelectorAll('a'))
      .find(link => link.getAttribute('href') === '/sellers/seller-id');
    expect(reviewLink).toBeTruthy();
  });

  it('requests all sellers when the all-state view is selected', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const allButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('All sellers')) as HTMLButtonElement;
    allButton.click();
    await fixture.whenStable();

    expect(adminSellerService.getSellers).toHaveBeenCalledWith(jasmine.objectContaining({ view: 'All' }));
  });

  it('sends status filters to the operational list endpoint', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const statusInput = compiled.querySelector('input[formControlName="status"]') as HTMLInputElement;
    statusInput.value = 'Verified';
    statusInput.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(adminSellerService.getSellers).toHaveBeenCalledWith(jasmine.objectContaining({ status: 'Verified' }));
  });

  it('shows an empty state when there are no matching sellers', async () => {
    adminSellerService.getSellers.and.resolveTo(createPagedResponse([]));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No sellers found');
  });
});

function createPagedResponse(
  items: AdminSellerOperationalSummaryResponse[]
): AdminPagedResponse<AdminSellerOperationalSummaryResponse> {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 25,
    statusCounts: [
      { status: 'UnderReview', count: items.filter(item => item.verificationStatus === 'UnderReview').length },
      { status: 'Verified', count: items.filter(item => item.verificationStatus === 'Verified').length }
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

function createSellerSummary(overrides: Partial<AdminSellerOperationalSummaryResponse> = {}): AdminSellerOperationalSummaryResponse {
  return {
    sellerId: 'seller-id',
    displayName: 'Seller Store',
    contactEmail: 'seller@example.test',
    storeName: 'Seller Store',
    storeSlug: 'seller-store',
    verificationStatus: 'UnderReview',
    submittedAtUtc: '2026-05-18T12:00:00Z',
    updatedAtUtc: '2026-05-18T13:00:00Z',
    detailRoute: '/sellers/seller-id',
    ...overrides
  };
}
