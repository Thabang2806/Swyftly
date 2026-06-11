import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { SellerAdCampaignService } from '../seller/seller-ad-campaign.service';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerAdCampaignDetailPageComponent } from './seller-ad-campaign-detail-page.component';

describe('SellerAdCampaignDetailPageComponent', () => {
  let fixture: ComponentFixture<SellerAdCampaignDetailPageComponent>;
  let adCampaignService: jasmine.SpyObj<SellerAdCampaignService>;
  let productService: jasmine.SpyObj<SellerProductService>;

  beforeEach(async () => {
    adCampaignService = jasmine.createSpyObj<SellerAdCampaignService>('SellerAdCampaignService', [
      'getCampaign',
      'getMetrics',
      'submitForReview',
      'pauseCampaign',
      'resumeCampaign',
      'cancelCampaign'
    ]);
    productService = jasmine.createSpyObj<SellerProductService>('SellerProductService', ['listProducts']);
    adCampaignService.getCampaign.and.resolveTo(createCampaign({ status: 'Active' }));
    adCampaignService.getMetrics.and.resolveTo({
      adCampaignId: 'campaign-id',
      sellerId: 'seller-id',
      status: 'Active',
      impressions: 100,
      clicks: 5,
      clickThroughRate: 0.05,
      spend: 25,
      ordersGenerated: 1,
      revenueGenerated: 499,
      returnOnAdSpend: 19.96,
      currency: 'ZAR'
    });
    adCampaignService.pauseCampaign.and.resolveTo(createCampaign({ status: 'Paused' }));
    productService.listProducts.and.resolveTo([{
      productId: 'product-id',
      categoryId: 'category-id',
      title: 'Published Dress',
      slug: 'published-dress',
      status: 'Published',
      updatedAtUtc: '2026-05-18T12:00:00Z'
    }]);

    await TestBed.configureTestingModule({
      imports: [SellerAdCampaignDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 'campaign-id' })
            }
          }
        },
        { provide: SellerAdCampaignService, useValue: adCampaignService },
        { provide: SellerProductService, useValue: productService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerAdCampaignDetailPageComponent);
  });

  it('loads campaign detail with metrics and product names', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Launch campaign');
    expect(compiled.textContent).toContain('Published Dress');
    expect(compiled.textContent).toContain('Impressions');
    expect(compiled.textContent).toContain('100');
  });

  it('pauses an active campaign', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const pauseButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Pause')) as HTMLButtonElement;
    pauseButton.click();

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adCampaignService.pauseCampaign).toHaveBeenCalledWith('campaign-id');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Campaign paused.');
  });
});

function createCampaign(overrides: Record<string, unknown> = {}) {
  return {
    adCampaignId: 'campaign-id',
    sellerId: 'seller-id',
    name: 'Launch campaign',
    campaignType: 'FeaturedProduct',
    status: 'Draft',
    startsAtUtc: '2026-05-20T00:00:00Z',
    endsAtUtc: '2026-06-03T00:00:00Z',
    submittedAtUtc: null,
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
      products: [{
        productId: 'product-id',
        isEligible: true,
        qualityScore: 90,
        reasons: []
      }]
    },
    moderationEvents: [],
    ...overrides
  };
}
