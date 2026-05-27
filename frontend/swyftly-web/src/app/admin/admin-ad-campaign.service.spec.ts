import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminAdCampaignService } from './admin-ad-campaign.service';

describe('AdminAdCampaignService', () => {
  let service: AdminAdCampaignService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminAdCampaignService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads pending ad campaigns', async () => {
    const promise = service.getPendingCampaigns();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/ad-campaigns/pending`);
    expect(request.request.method).toBe('GET');
    request.flush([createCampaignSummary()]);

    const response = await promise;
    expect(response[0].status).toBe('PendingReview');
  });

  it('loads all-state ad campaign operational lists with query params', async () => {
    const promise = service.getCampaigns({
      view: 'All',
      status: 'Active',
      search: 'launch',
      sellerId: 'seller-id',
      page: 2,
      pageSize: 20,
      sort: 'StartDateAsc'
    });

    const request = httpTestingController.expectOne(req => req.url === `${environment.apiBaseUrl}/api/admin/ad-campaigns`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('view')).toBe('All');
    expect(request.request.params.get('status')).toBe('Active');
    expect(request.request.params.get('search')).toBe('launch');
    expect(request.request.params.get('sellerId')).toBe('seller-id');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('sort')).toBe('StartDateAsc');
    request.flush({
      items: [{
        ...createCampaignSummary(),
        sellerVerificationStatus: 'Verified',
        status: 'Active',
        updatedAtUtc: '2026-05-19T12:30:00Z',
        detailRoute: '/admin/ads/campaign-id'
      }],
      totalCount: 1,
      page: 2,
      pageSize: 20,
      statusCounts: [{ status: 'Active', count: 1 }]
    });

    const response = await promise;
    expect(response.items[0].detailRoute).toBe('/admin/ads/campaign-id');
  });

  it('loads campaign detail', async () => {
    const promise = service.getCampaign('campaign-id');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/ad-campaigns/campaign-id`);
    expect(request.request.method).toBe('GET');
    request.flush(createCampaignDetail());

    const response = await promise;
    expect(response.adCampaignId).toBe('campaign-id');
  });

  it('approves and rejects campaigns', async () => {
    const approvePromise = service.approveCampaign('campaign-id');
    const approveRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/ad-campaigns/campaign-id/approve`);
    expect(approveRequest.request.method).toBe('POST');
    expect(approveRequest.request.body).toEqual({});
    approveRequest.flush(createCampaignDetail({ status: 'Active' }));

    const rejectPromise = service.rejectCampaign('campaign-id', { reason: 'Ad copy mismatch.' });
    const rejectRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/ad-campaigns/campaign-id/reject`);
    expect(rejectRequest.request.method).toBe('POST');
    expect(rejectRequest.request.body).toEqual({ reason: 'Ad copy mismatch.' });
    rejectRequest.flush(createCampaignDetail({ status: 'Rejected' }));

    await expectAsync(approvePromise).toBeResolved();
    await expectAsync(rejectPromise).toBeResolved();
  });
});

function createCampaignSummary() {
  return {
    adCampaignId: 'campaign-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    name: 'Launch campaign',
    campaignType: 'FeaturedProduct',
    status: 'PendingReview',
    startsAtUtc: '2026-05-20T12:00:00Z',
    endsAtUtc: '2026-06-03T12:00:00Z',
    submittedAtUtc: '2026-05-19T12:00:00Z',
    productCount: 1,
    totalBudget: 1000,
    currency: 'ZAR'
  };
}

function createCampaignDetail(overrides: Record<string, unknown> = {}) {
  return {
    ...createCampaignSummary(),
    seller: {
      displayName: 'Seller Store',
      contactEmail: 'seller@example.test',
      verificationStatus: 'Verified'
    },
    approvedAtUtc: null,
    pausedAtUtc: null,
    completedAtUtc: null,
    cancelledAtUtc: null,
    rejectionReason: null,
    products: [],
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
    auditTrail: [],
    ...overrides
  };
}
