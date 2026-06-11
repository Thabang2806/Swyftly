import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerPayoutProfileChangeService } from './seller-payout-profile-change.service';

describe('SellerPayoutProfileChangeService', () => {
  let service: SellerPayoutProfileChangeService;
  let httpTestingController: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/api/seller/payout-profile/change-request`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(SellerPayoutProfileChangeService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls seller payout profile change endpoints', async () => {
    const statePromise = service.getState();
    const stateRequest = httpTestingController.expectOne(baseUrl);
    expect(stateRequest.request.method).toBe('GET');
    stateRequest.flush(createState());
    await expectAsync(statePromise).toBeResolved();

    const payload = {
      payoutProviderReference: 'provider-ref-next',
      reason: 'Updated reference.'
    };
    const draftPromise = service.upsertDraft(payload);
    const draftRequest = httpTestingController.expectOne(baseUrl);
    expect(draftRequest.request.method).toBe('PUT');
    expect(draftRequest.request.body).toEqual(payload);
    draftRequest.flush(createState());
    await expectAsync(draftPromise).toBeResolved();

    const submitPromise = service.submitForReview();
    const submitRequest = httpTestingController.expectOne(`${baseUrl}/submit-review`);
    expect(submitRequest.request.method).toBe('POST');
    submitRequest.flush(createState());
    await expectAsync(submitPromise).toBeResolved();

    const cancelPromise = service.cancel();
    const cancelRequest = httpTestingController.expectOne(`${baseUrl}/cancel`);
    expect(cancelRequest.request.method).toBe('POST');
    cancelRequest.flush(createState());
    await expectAsync(cancelPromise).toBeResolved();
  });
});

function createState() {
  return {
    currentPayoutProfile: {
      payoutProviderReference: 'provider-ref-current',
      isAdminApproved: true,
      approvedAtUtc: null,
      approvedByUserId: null
    },
    activeRequest: null,
    latestRequest: null
  };
}
