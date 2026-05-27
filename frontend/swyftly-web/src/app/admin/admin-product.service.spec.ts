import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminProductService } from './admin-product.service';

describe('AdminProductService', () => {
  let service: AdminProductService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminProductService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads pending review products', async () => {
    const promise = service.getPendingReviewProducts();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/pending-review`);
    expect(request.request.method).toBe('GET');
    request.flush([createProductSummary()]);

    const response = await promise;
    expect(response[0].status).toBe('PendingReview');
  });

  it('loads product moderation items with query params', async () => {
    const promise = service.getModerationItems({
      view: 'All',
      status: 'Published',
      search: 'dress',
      sellerId: 'seller-id',
      page: 3,
      pageSize: 25,
      sort: 'SubmittedAsc'
    });

    const request = httpTestingController.expectOne(req => req.url === `${environment.apiBaseUrl}/api/admin/products/moderation-items`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('view')).toBe('All');
    expect(request.request.params.get('status')).toBe('Published');
    expect(request.request.params.get('search')).toBe('dress');
    expect(request.request.params.get('sellerId')).toBe('seller-id');
    expect(request.request.params.get('page')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('sort')).toBe('SubmittedAsc');
    request.flush({
      items: [{
        id: 'product-id',
        itemType: 'Product',
        productId: 'product-id',
        revisionId: null,
        sellerId: 'seller-id',
        sellerDisplayName: 'Seller Store',
        sellerVerificationStatus: 'Verified',
        title: 'Summer Dress',
        categoryPath: 'Women > Dresses',
        status: 'Published',
        submittedAtUtc: null,
        updatedAtUtc: '2026-05-18T12:00:00Z',
        riskFlagCount: 0,
        itemCount: 0,
        detailRoute: '/admin/products/product-id'
      }],
      totalCount: 1,
      page: 3,
      pageSize: 25,
      statusCounts: [{ status: 'Published', count: 1 }]
    });

    const response = await promise;
    expect(response.items[0].detailRoute).toBe('/admin/products/product-id');
  });

  it('approves with an optional override reason', async () => {
    const promise = service.approveProduct('product-id', { overrideReason: 'Manual review complete.' });

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/product-id/approve`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ overrideReason: 'Manual review complete.' });
    request.flush(createProductDetail({ status: 'Published' }));

    const response = await promise;
    expect(response.status).toBe('Published');
  });

  it('rejects and requests changes with reasons', async () => {
    const rejectPromise = service.rejectProduct('product-id', { reason: 'Policy issue.' });
    const rejectRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/product-id/reject`);
    expect(rejectRequest.request.method).toBe('POST');
    expect(rejectRequest.request.body).toEqual({ reason: 'Policy issue.' });
    rejectRequest.flush(createProductDetail({ status: 'Rejected' }));

    const changesPromise = service.requestChanges('product-id', { reason: 'Add measurements.' });
    const changesRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/product-id/request-changes`);
    expect(changesRequest.request.method).toBe('POST');
    expect(changesRequest.request.body).toEqual({ reason: 'Add measurements.' });
    changesRequest.flush(createProductDetail({ status: 'ChangesRequested' }));

    await expectAsync(rejectPromise).toBeResolved();
    await expectAsync(changesPromise).toBeResolved();
  });

  it('loads and reviews variant revisions', async () => {
    const listPromise = service.getPendingVariantRevisions();
    const listRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/pending-variant-revisions`);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([createVariantRevisionSummary()]);
    await expectAsync(listPromise).toBeResolved();

    const detailPromise = service.getVariantRevision('revision-id');
    const detailRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/variant-revisions/revision-id`);
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush(createVariantRevisionDetail());
    await expectAsync(detailPromise).toBeResolved();

    const approvePromise = service.approveVariantRevision('revision-id');
    const approveRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/variant-revisions/revision-id/approve`);
    expect(approveRequest.request.method).toBe('POST');
    approveRequest.flush(createVariantRevisionDetail({ status: 'Approved' }));
    await expectAsync(approvePromise).toBeResolved();

    const rejectPromise = service.rejectVariantRevision('revision-id', { reason: 'Price evidence missing.' });
    const rejectRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/products/variant-revisions/revision-id/reject`);
    expect(rejectRequest.request.method).toBe('POST');
    expect(rejectRequest.request.body).toEqual({ reason: 'Price evidence missing.' });
    rejectRequest.flush(createVariantRevisionDetail({ status: 'Rejected' }));
    await expectAsync(rejectPromise).toBeResolved();
  });
});

function createProductSummary() {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    sellerVerificationStatus: 'Verified',
    title: 'Summer Dress',
    categoryPath: 'Women > Clothing > Dresses',
    status: 'PendingReview',
    highRiskFlagCount: 0,
    updatedAtUtc: '2026-05-18T12:00:00Z'
  };
}

function createProductDetail(overrides: Record<string, unknown> = {}) {
  return {
    ...createProductSummary(),
    seller: {
      displayName: 'Seller Store',
      contactEmail: 'seller@example.test',
      verificationStatus: 'Verified'
    },
    categoryId: 'category-id',
    brandId: null,
    slug: 'summer-dress',
    shortDescription: 'Short description',
    fullDescription: 'Full description',
    tags: [],
    rejectionReason: null,
    createdAtUtc: '2026-05-18T11:00:00Z',
    publishedAtUtc: null,
    attributes: {},
    variants: [],
    images: [],
    moderationResults: [],
    auditTrail: [],
    ...overrides
  };
}

function createVariantRevisionSummary(overrides: Record<string, unknown> = {}) {
  return {
    revisionId: 'revision-id',
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    sellerVerificationStatus: 'Verified',
    productTitle: 'Summer Dress',
    status: 'PendingReview',
    itemCount: 1,
    submittedAtUtc: '2026-05-18T12:00:00Z',
    updatedAtUtc: '2026-05-18T12:00:00Z',
    ...overrides
  };
}

function createVariantRevisionDetail(overrides: Record<string, unknown> = {}) {
  return {
    revisionId: 'revision-id',
    productId: 'product-id',
    sellerId: 'seller-id',
    seller: {
      displayName: 'Seller Store',
      contactEmail: 'seller@example.test',
      verificationStatus: 'Verified'
    },
    productTitle: 'Summer Dress',
    productSlug: 'summer-dress',
    status: 'PendingReview',
    sellerReason: 'Seasonal price update.',
    rejectionReason: null,
    submittedAtUtc: '2026-05-18T12:00:00Z',
    reviewedAtUtc: null,
    currentVariants: [],
    items: [],
    proposedFinalVariants: [],
    validationErrors: {},
    auditTrail: [],
    ...overrides
  };
}
