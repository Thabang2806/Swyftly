import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminAdCampaignOperationalSummaryResponse } from '../admin/admin-ad-campaign.models';
import { AdminAdCampaignService } from '../admin/admin-ad-campaign.service';
import { AdminModerationQueueService } from '../admin/admin-moderation-queue.service';
import { AdminPagedResponse } from '../admin/admin-operational-list.models';
import { AdminQueueTriageService } from '../admin/admin-queue-triage.service';
import { AdminAdCampaignsPageComponent } from './admin-ad-campaigns-page.component';

describe('AdminAdCampaignsPageComponent', () => {
  let fixture: ComponentFixture<AdminAdCampaignsPageComponent>;
  let adminAdCampaignService: jasmine.SpyObj<AdminAdCampaignService>;
  let adminModerationQueueService: jasmine.SpyObj<AdminModerationQueueService>;

  beforeEach(async () => {
    adminAdCampaignService = jasmine.createSpyObj<AdminAdCampaignService>('AdminAdCampaignService', ['getCampaigns']);
    adminAdCampaignService.getCampaigns.and.resolveTo(createPagedResponse([createCampaignSummary()]));
    adminModerationQueueService = jasmine.createSpyObj<AdminModerationQueueService>('AdminModerationQueueService', ['getSavedViews', 'getSummary', 'createSavedView', 'updateSavedView', 'deleteSavedView', 'makeDefault']);
    adminModerationQueueService.getSavedViews.and.resolveTo([]);
    adminModerationQueueService.getSummary.and.resolveTo(createQueueSummary());

    await TestBed.configureTestingModule({
      imports: [AdminAdCampaignsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminModerationQueueService, useValue: adminModerationQueueService },
        { provide: AdminQueueTriageService, useValue: jasmine.createSpyObj<AdminQueueTriageService>('AdminQueueTriageService', ['bulkTriage', 'claim', 'unclaim', 'updateTriage']) },
        { provide: AdminAdCampaignService, useValue: adminAdCampaignService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminAdCampaignsPageComponent);
  });

  it('loads and displays ad campaign operational records', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(adminAdCampaignService.getCampaigns).toHaveBeenCalledWith(jasmine.objectContaining({ view: 'NeedsAttention' }));
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('Launch campaign');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('FeaturedProduct');
    const reviewLink = Array.from(compiled.querySelectorAll('a'))
      .find(link => link.getAttribute('href') === '/admin/ads/campaign-id');
    expect(reviewLink).toBeTruthy();
  });

  it('requests all campaigns when the all-state view is selected', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const allButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('All campaigns')) as HTMLButtonElement;
    allButton.click();
    await fixture.whenStable();

    expect(adminAdCampaignService.getCampaigns).toHaveBeenCalledWith(jasmine.objectContaining({ view: 'All' }));
  });

  it('sends status filters to the campaign list endpoint', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const statusInput = compiled.querySelector('input[formControlName="status"]') as HTMLInputElement;
    statusInput.value = 'Active';
    statusInput.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(adminAdCampaignService.getCampaigns).toHaveBeenCalledWith(jasmine.objectContaining({ status: 'Active' }));
  });

  it('shows an empty state when there are no matching campaigns', async () => {
    adminAdCampaignService.getCampaigns.and.resolveTo(createPagedResponse([]));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No ad campaigns found');
  });
});

function createPagedResponse(
  items: AdminAdCampaignOperationalSummaryResponse[]
): AdminPagedResponse<AdminAdCampaignOperationalSummaryResponse> {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 25,
    statusCounts: [
      { status: 'PendingReview', count: items.filter(item => item.status === 'PendingReview').length },
      { status: 'Active', count: items.filter(item => item.status === 'Active').length }
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

function createCampaignSummary(overrides: Partial<AdminAdCampaignOperationalSummaryResponse> = {}): AdminAdCampaignOperationalSummaryResponse {
  return {
    adCampaignId: 'campaign-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    sellerVerificationStatus: 'Verified',
    name: 'Launch campaign',
    campaignType: 'FeaturedProduct',
    status: 'PendingReview',
    startsAtUtc: '2026-05-20T12:00:00Z',
    endsAtUtc: '2026-06-03T12:00:00Z',
    submittedAtUtc: '2026-05-19T12:00:00Z',
    updatedAtUtc: '2026-05-19T12:30:00Z',
    productCount: 1,
    totalBudget: 1000,
    currency: 'ZAR',
    detailRoute: '/admin/ads/campaign-id',
    ...overrides
  };
}
