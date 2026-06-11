import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerAdCampaignService } from './seller-ad-campaign.service';

describe('SellerAdCampaignService', () => {
  let service: SellerAdCampaignService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerAdCampaignService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('lists and creates campaigns', async () => {
    const listPromise = service.listCampaigns();
    const listRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/ad-campaigns`);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([createCampaign()]);

    const createPromise = service.createCampaign(createRequest());
    const createHttpRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/ad-campaigns`);
    expect(createHttpRequest.request.method).toBe('POST');
    expect(createHttpRequest.request.body.name).toBe('Launch campaign');
    createHttpRequest.flush(createCampaign());

    await expectAsync(listPromise).toBeResolved();
    await expectAsync(createPromise).toBeResolved();
  });

  it('loads detail, metrics, and state actions', async () => {
    const detailPromise = service.getCampaign('campaign-id');
    const detailRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/ad-campaigns/campaign-id`);
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush(createCampaign());

    const metricsPromise = service.getMetrics('campaign-id');
    const metricsRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/ad-campaigns/campaign-id/metrics`);
    expect(metricsRequest.request.method).toBe('GET');
    metricsRequest.flush({
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

    const submitPromise = service.submitForReview('campaign-id');
    const submitRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/ad-campaigns/campaign-id/submit-review`);
    expect(submitRequest.request.method).toBe('POST');
    submitRequest.flush(createCampaign({ status: 'PendingReview' }));

    const pausePromise = service.pauseCampaign('campaign-id');
    const pauseRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/ad-campaigns/campaign-id/pause`);
    expect(pauseRequest.request.method).toBe('POST');
    pauseRequest.flush(createCampaign({ status: 'Paused' }));

    await expectAsync(detailPromise).toBeResolved();
    await expectAsync(metricsPromise).toBeResolved();
    await expectAsync(submitPromise).toBeResolved();
    await expectAsync(pausePromise).toBeResolved();
  });
});

function createRequest() {
  return {
    name: 'Launch campaign',
    campaignType: 'FeaturedProduct',
    startsAtUtc: '2026-05-20T00:00:00Z',
    endsAtUtc: '2026-06-03T00:00:00Z',
    productIds: ['product-id'],
    budget: {
      currency: 'ZAR',
      dailyBudget: 100,
      totalBudget: 1000,
      maxCostPerClick: 5
    }
  };
}

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
      spentAmount: 0
    },
    eligibility: {
      isEligible: true,
      sellerReasons: [],
      products: []
    },
    moderationEvents: [],
    ...overrides
  };
}
