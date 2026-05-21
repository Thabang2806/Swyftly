import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { SellerOnboardingResponse } from '../seller/seller-onboarding.models';
import { SellerOnboardingService } from '../seller/seller-onboarding.service';
import { SellerPageComponent } from './seller-page.component';

describe('SellerPageComponent', () => {
  let fixture: ComponentFixture<SellerPageComponent>;
  let onboardingService: jasmine.SpyObj<SellerOnboardingService>;

  beforeEach(async () => {
    onboardingService = jasmine.createSpyObj<SellerOnboardingService>(
      'SellerOnboardingService',
      [
        'getOnboarding',
        'updateProfile',
        'updateStorefront',
        'updateAddress',
        'updatePayout',
        'submitVerification'
      ]);
    onboardingService.getOnboarding.and.resolveTo(createOnboardingResponse());

    await TestBed.configureTestingModule({
      imports: [SellerPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerOnboardingService, useValue: onboardingService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerPageComponent);
  });

  it('loads and displays seller verification status', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('PendingVerification');
    expect(compiled.textContent).toContain('0 of 4 setup sections complete');
    expect(compiled.querySelector('a[href="/seller/orders"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/inventory"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/returns"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/payouts"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/support"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/ads"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/analytics"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/settings/store"]')).not.toBeNull();
  });

  it('shows the verified seller dashboard instead of onboarding steps', async () => {
    onboardingService.getOnboarding.and.resolveTo(createOnboardingResponse({
      verificationStatus: 'Verified',
      isProfileComplete: true,
      isStorefrontComplete: true,
      isAddressComplete: true,
      isPayoutPlaceholderComplete: true,
      storefront: {
        storeName: 'Verified Store',
        slug: 'verified-store',
        description: null,
        logoUrl: null,
        bannerUrl: null,
        isPublished: true
      }
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Seller dashboard');
    expect(compiled.textContent).toContain('Verified Store');
    expect(compiled.querySelector('.hf-seller-dashboard-hero')).not.toBeNull();
    expect(compiled.querySelector('.hf-seller-opportunity-card')).not.toBeNull();
    expect(compiled.textContent).toContain('Orders');
    expect(compiled.textContent).not.toContain('Basic seller details');
  });

  it('does not save profile when required fields are missing', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const form = (fixture.nativeElement as HTMLElement).querySelector('form');
    form?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(onboardingService.updateProfile).not.toHaveBeenCalled();
  });

  it('submits verification when onboarding is complete', async () => {
    const completeOnboarding = createOnboardingResponse({
      canSubmitForVerification: true,
      isProfileComplete: true,
      isStorefrontComplete: true,
      isAddressComplete: true,
      isPayoutPlaceholderComplete: true
    });
    onboardingService.getOnboarding.and.resolveTo(completeOnboarding);
    onboardingService.submitVerification.and.resolveTo({
      ...completeOnboarding,
      verificationStatus: 'UnderReview'
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const reviewStep = Array.from(compiled.querySelectorAll('.wizard-steps button'))
      .find(button => button.textContent?.includes('Review'));
    reviewStep?.dispatchEvent(new Event('click'));
    fixture.detectChanges();

    const submitButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Submit for verification'));
    submitButton?.dispatchEvent(new Event('click'));

    await fixture.whenStable();

    expect(onboardingService.submitVerification).toHaveBeenCalled();
  });
});

function createOnboardingResponse(
  overrides: Partial<SellerOnboardingResponse> = {}
): SellerOnboardingResponse {
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
    ...overrides
  };
}
