import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminSellerService } from './admin-seller.service';

describe('AdminSellerService', () => {
  let service: AdminSellerService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminSellerService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads pending sellers', async () => {
    const promise = service.getPendingSellers();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/sellers/pending`);
    expect(request.request.method).toBe('GET');
    request.flush([createSellerSummary()]);

    const response = await promise;
    expect(response[0].verificationStatus).toBe('UnderReview');
  });

  it('loads all-state seller operational lists with query params', async () => {
    const promise = service.getSellers({
      view: 'All',
      status: 'Verified',
      search: 'store',
      sellerId: 'seller-id',
      page: 2,
      pageSize: 50,
      sort: 'UpdatedAsc'
    });

    const request = httpTestingController.expectOne(req => req.url === `${environment.apiBaseUrl}/api/admin/sellers`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('view')).toBe('All');
    expect(request.request.params.get('status')).toBe('Verified');
    expect(request.request.params.get('search')).toBe('store');
    expect(request.request.params.get('sellerId')).toBe('seller-id');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.get('sort')).toBe('UpdatedAsc');
    request.flush({
      items: [{ ...createSellerSummary(), updatedAtUtc: '2026-05-18T13:00:00Z', detailRoute: '/admin/sellers/seller-id' }],
      totalCount: 1,
      page: 2,
      pageSize: 50,
      statusCounts: [{ status: 'Verified', count: 1 }]
    });

    const response = await promise;
    expect(response.items[0].detailRoute).toBe('/admin/sellers/seller-id');
  });

  it('approves a seller', async () => {
    const promise = service.approveSeller('seller-id');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/sellers/seller-id/approve`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush(createSellerDetail({ verificationStatus: 'Verified' }));

    const response = await promise;
    expect(response.verificationStatus).toBe('Verified');
  });

  it('rejects and suspends sellers with a reason', async () => {
    const rejectPromise = service.rejectSeller('seller-id', { reason: 'Missing documents.' });
    const rejectRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/sellers/seller-id/reject`);
    expect(rejectRequest.request.method).toBe('POST');
    expect(rejectRequest.request.body).toEqual({ reason: 'Missing documents.' });
    rejectRequest.flush(createSellerDetail({ verificationStatus: 'Rejected' }));

    const suspendPromise = service.suspendSeller('seller-id', { reason: 'Policy review.' });
    const suspendRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/sellers/seller-id/suspend`);
    expect(suspendRequest.request.method).toBe('POST');
    expect(suspendRequest.request.body).toEqual({ reason: 'Policy review.' });
    suspendRequest.flush(createSellerDetail({ verificationStatus: 'Suspended' }));

    await expectAsync(rejectPromise).toBeResolved();
    await expectAsync(suspendPromise).toBeResolved();
  });

  it('downloads seller verification evidence as a blob', async () => {
    const promise = service.downloadVerificationEvidence('seller-id', 'evidence-id');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/sellers/seller-id/verification-evidence/evidence-id/download`);
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['evidence'], { type: 'application/pdf' }));

    await expectAsync(promise).toBeResolved();
  });
});

function createSellerSummary() {
  return {
    sellerId: 'seller-id',
    displayName: 'Seller Store',
    contactEmail: 'seller@example.test',
    storeName: 'Seller Store',
    storeSlug: 'seller-store',
    verificationStatus: 'UnderReview',
    submittedAtUtc: '2026-05-18T12:00:00Z'
  };
}

function createSellerDetail(overrides: Record<string, unknown> = {}) {
  return {
    sellerId: 'seller-id',
    userId: 'user-id',
    verificationStatus: 'UnderReview',
    displayName: 'Seller Store',
    contactEmail: 'seller@example.test',
    phoneNumber: '+27110000000',
    businessType: 'RegisteredBusiness',
    businessName: 'Seller Trading',
    storefront: {
      storeName: 'Seller Store',
      slug: 'seller-store',
      description: 'Seller storefront',
      logoUrl: null,
      bannerUrl: null,
      isPublished: false
    },
    address: {
      addressLine1: '1 Market Street',
      addressLine2: null,
      city: 'Johannesburg',
      province: 'Gauteng',
      postalCode: '2000',
      countryCode: 'ZA'
    },
    payout: {
      payoutProviderReference: 'provider-ref-123',
      hasSubmittedPlaceholder: true,
      isAdminApproved: false
    },
    storePolicy: {
      returnWindowDays: null,
      returnPolicy: null,
      exchangePolicy: null,
      fulfilmentPolicy: null,
      supportPolicy: null,
      careInstructions: null,
      productDisclaimer: null,
      isComplete: false,
      missingFields: ['returnPolicy']
    },
    verificationEvidence: [],
    auditTrail: [],
    ...overrides
  };
}
