import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerAdCampaignService } from '../seller/seller-ad-campaign.service';
import { SellerAdCampaignsPageComponent } from './seller-ad-campaigns-page.component';

describe('SellerAdCampaignsPageComponent', () => {
  let fixture: ComponentFixture<SellerAdCampaignsPageComponent>;
  let adCampaignService: jasmine.SpyObj<SellerAdCampaignService>;

  beforeEach(async () => {
    adCampaignService = jasmine.createSpyObj<SellerAdCampaignService>('SellerAdCampaignService', ['listCampaigns']);
    adCampaignService.listCampaigns.and.resolveTo([createCampaign()]);

    await TestBed.configureTestingModule({
      imports: [SellerAdCampaignsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerAdCampaignService, useValue: adCampaignService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerAdCampaignsPageComponent);
  });

  it('loads and displays seller ad campaigns', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Launch campaign');
    expect(compiled.textContent).toContain('PendingReview');
    expect(compiled.querySelector('.hf-ads-layout')).not.toBeNull();
    expect(compiled.textContent).toContain('Ad spend');
    expect(compiled.querySelector('a[href="/ads/new"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/ads/campaign-id"]')).not.toBeNull();
  });
});

function createCampaign() {
  return {
    adCampaignId: 'campaign-id',
    sellerId: 'seller-id',
    name: 'Launch campaign',
    campaignType: 'FeaturedProduct',
    status: 'PendingReview',
    startsAtUtc: '2026-05-20T00:00:00Z',
    endsAtUtc: '2026-06-03T00:00:00Z',
    submittedAtUtc: '2026-05-19T00:00:00Z',
    approvedAtUtc: null,
    pausedAtUtc: null,
    completedAtUtc: null,
    cancelledAtUtc: null,
    rejectionReason: null,
    productIds: ['product-id'],
    budget: {
      currency: 'ZAR',
      dailyBudget: 100,
      totalBudget: 1000,
      maxCostPerClick: 5,
      spentAmount: 25
    },
    eligibility: {
      isEligible: true,
      sellerReasons: [],
      products: []
    },
    moderationEvents: []
  };
}
