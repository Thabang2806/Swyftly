import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminPayoutProfileChangeService } from './admin-payout-profile-change.service';

describe('AdminPayoutProfileChangeService', () => {
  let service: AdminPayoutProfileChangeService;
  let httpTestingController: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/api/admin/sellers/payout-profile-change-requests`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(AdminPayoutProfileChangeService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls admin payout profile change review endpoints', async () => {
    const listPromise = service.list();
    const listRequest = httpTestingController.expectOne(baseUrl);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([createResponse()]);
    await expectAsync(listPromise).toBeResolved();

    const detailPromise = service.get('request-id');
    const detailRequest = httpTestingController.expectOne(`${baseUrl}/request-id`);
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush(createResponse());
    await expectAsync(detailPromise).toBeResolved();

    const approvePromise = service.approve('request-id', { reason: 'Verified.' });
    const approveRequest = httpTestingController.expectOne(`${baseUrl}/request-id/approve`);
    expect(approveRequest.request.method).toBe('POST');
    expect(approveRequest.request.body).toEqual({ reason: 'Verified.' });
    approveRequest.flush(createResponse({ status: 'Approved' }));
    await expectAsync(approvePromise).toBeResolved();

    const rejectPromise = service.reject('request-id', { reason: 'Could not verify.' });
    const rejectRequest = httpTestingController.expectOne(`${baseUrl}/request-id/reject`);
    expect(rejectRequest.request.method).toBe('POST');
    expect(rejectRequest.request.body).toEqual({ reason: 'Could not verify.' });
    rejectRequest.flush(createResponse({ status: 'Rejected' }));
    await expectAsync(rejectPromise).toBeResolved();
  });
});

function createResponse(overrides: Record<string, unknown> = {}) {
  return {
    requestId: 'request-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Luxe Seller',
    sellerContactEmail: 'seller@example.test',
    sellerVerificationStatus: 'Verified',
    currentPayoutProviderReference: 'provider-ref-current',
    currentPayoutIsAdminApproved: true,
    proposedPayoutProviderReference: 'provider-ref-next',
    reason: 'Updated reference.',
    status: 'PendingReview',
    requestedByUserId: 'requester-user-id',
    submittedAtUtc: '2026-05-21T10:00:00Z',
    cancelledAtUtc: null,
    reviewedByUserId: null,
    reviewedAtUtc: null,
    reviewReason: null,
    createdAtUtc: '2026-05-21T09:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z',
    ...overrides
  };
}
