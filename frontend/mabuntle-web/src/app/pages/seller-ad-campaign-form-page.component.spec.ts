import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerAdCampaignService } from '../seller/seller-ad-campaign.service';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerAdCampaignFormPageComponent } from './seller-ad-campaign-form-page.component';

describe('SellerAdCampaignFormPageComponent', () => {
  let fixture: ComponentFixture<SellerAdCampaignFormPageComponent>;
  let adCampaignService: jasmine.SpyObj<SellerAdCampaignService>;
  let productService: jasmine.SpyObj<SellerProductService>;

  beforeEach(async () => {
    adCampaignService = jasmine.createSpyObj<SellerAdCampaignService>('SellerAdCampaignService', [
      'createCampaign',
      'updateCampaign',
      'submitForReview'
    ]);
    productService = jasmine.createSpyObj<SellerProductService>('SellerProductService', ['listProducts']);
    productService.listProducts.and.resolveTo([
      {
        productId: 'published-product',
        categoryId: 'category-id',
        title: 'Published Dress',
        slug: 'published-dress',
        status: 'Published',
        updatedAtUtc: '2026-05-18T12:00:00Z'
      },
      {
        productId: 'draft-product',
        categoryId: 'category-id',
        title: 'Draft Dress',
        slug: 'draft-dress',
        status: 'Draft',
        updatedAtUtc: '2026-05-18T12:00:00Z'
      }
    ]);
    adCampaignService.createCampaign.and.resolveTo(createCampaign());
    adCampaignService.submitForReview.and.resolveTo(createCampaign({ status: 'PendingReview' }));

    await TestBed.configureTestingModule({
      imports: [SellerAdCampaignFormPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerAdCampaignService, useValue: adCampaignService },
        { provide: SellerProductService, useValue: productService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerAdCampaignFormPageComponent);
  });

  it('loads products and shows selected product eligibility warnings', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance as unknown as {
      campaignForm: { patchValue: (value: object) => void };
    };
    component.campaignForm.patchValue({ productIds: ['draft-product'] });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Draft Dress is Draft');
  });

  it('creates a campaign draft from the form', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance as unknown as {
      campaignForm: { patchValue: (value: object) => void };
    };
    component.campaignForm.patchValue({
      name: 'Launch campaign',
      productIds: ['published-product'],
      dailyBudget: 100,
      totalBudget: 1000,
      maxCostPerClick: 5
    });
    fixture.detectChanges();

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adCampaignService.createCampaign).toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Campaign draft created.');
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
    productIds: ['published-product'],
    budget: {
      currency: 'ZAR',
      dailyBudget: 100,
      totalBudget: 1000,
      maxCostPerClick: 5,
      spentAmount: 0
    },
    eligibility: {
      isEligible: true,
      sellerReasons: [],
      products: [{
        productId: 'published-product',
        isEligible: true,
        qualityScore: 90,
        reasons: []
      }]
    },
    moderationEvents: [],
    ...overrides
  };
}
