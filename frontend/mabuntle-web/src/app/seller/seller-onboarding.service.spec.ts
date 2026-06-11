import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerOnboardingService } from './seller-onboarding.service';

describe('SellerOnboardingService', () => {
  let service: SellerOnboardingService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerOnboardingService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads seller onboarding state', async () => {
    const promise = service.getOnboarding();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/onboarding`);
    expect(request.request.method).toBe('GET');
    request.flush(createOnboardingResponse());

    const response = await promise;
    expect(response.verificationStatus).toBe('PendingVerification');
    expect(response.latestVerificationReview).toBeNull();
  });

  it('updates profile details', async () => {
    const body = {
      displayName: 'Seller Store',
      contactEmail: 'seller@example.test',
      phoneNumber: '+27110000000',
      businessType: 'Individual' as const,
      businessName: null
    };

    const promise = service.updateProfile(body);

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/onboarding/profile`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(body);
    request.flush(createOnboardingResponse());

    await expectAsync(promise).toBeResolved();
  });

  it('submits verification', async () => {
    const promise = service.submitVerification();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/onboarding/submit-verification`);
    expect(request.request.method).toBe('POST');
    request.flush({ ...createOnboardingResponse(), verificationStatus: 'UnderReview' });

    const response = await promise;
    expect(response.verificationStatus).toBe('UnderReview');
  });
});

function createOnboardingResponse() {
  return {
    sellerId: '864c06ff-1b54-4931-a210-d458c014a19f',
    verificationStatus: 'PendingVerification',
    canSubmitForVerification: false,
    isProfileComplete: false,
    isStorefrontComplete: false,
    isAddressComplete: false,
    isPayoutPlaceholderComplete: false,
    profile: {
      displayName: null,
      contactEmail: null,
      phoneNumber: null,
      businessType: null,
      businessName: null
    },
    storefront: null,
    address: null,
    payout: null,
    latestVerificationReview: null
  };
}
