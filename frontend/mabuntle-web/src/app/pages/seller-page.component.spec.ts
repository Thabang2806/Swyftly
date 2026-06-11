import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { SellerDashboardSummaryResponse } from '../seller/seller-dashboard.models';
import { SellerDashboardService } from '../seller/seller-dashboard.service';
import { SellerOnboardingResponse } from '../seller/seller-onboarding.models';
import { SellerOnboardingService } from '../seller/seller-onboarding.service';
import { SellerVerificationEvidenceService } from '../seller/seller-verification-evidence.service';
import { SellerPageComponent } from './seller-page.component';

describe('SellerPageComponent', () => {
  let fixture: ComponentFixture<SellerPageComponent>;
  let onboardingService: jasmine.SpyObj<SellerOnboardingService>;
  let dashboardService: jasmine.SpyObj<SellerDashboardService>;
  let evidenceService: jasmine.SpyObj<SellerVerificationEvidenceService>;

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
    dashboardService = jasmine.createSpyObj<SellerDashboardService>(
      'SellerDashboardService',
      ['getSummary']);
    dashboardService.getSummary.and.resolveTo(createDashboardSummary());
    evidenceService = jasmine.createSpyObj<SellerVerificationEvidenceService>(
      'SellerVerificationEvidenceService',
      ['list', 'upload', 'remove', 'download']);
    evidenceService.list.and.resolveTo([]);
    evidenceService.upload.and.resolveTo({
      evidenceId: 'evidence-id',
      evidenceType: 'BusinessRegistration',
      originalFileName: 'registration.pdf',
      contentType: 'application/pdf',
      byteSize: 1234,
      sha256Hash: 'hash',
      note: 'Registration document',
      uploadedAtUtc: '2026-05-26T10:00:00Z',
      removedAtUtc: null
    });
    evidenceService.remove.and.resolveTo();
    evidenceService.download.and.resolveTo(new Blob(['evidence'], { type: 'application/pdf' }));

    await TestBed.configureTestingModule({
      imports: [SellerPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerOnboardingService, useValue: onboardingService },
        { provide: SellerDashboardService, useValue: dashboardService },
        { provide: SellerVerificationEvidenceService, useValue: evidenceService }
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
    expect(compiled.textContent).toContain('Complete onboarding to apply');
    expect(compiled.textContent).toContain('Supporting documents');
    expect(compiled.textContent).toContain('Prepare product drafts');
    expect(compiled.querySelector('a[href="/orders"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/inventory"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/returns"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/payouts"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/support"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/ads"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/analytics"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/settings/store"]')).not.toBeNull();
    expect(dashboardService.getSummary).not.toHaveBeenCalled();
  });

  it('shows under-review seller guidance without loading the verified dashboard', async () => {
    onboardingService.getOnboarding.and.resolveTo(createOnboardingResponse({
      verificationStatus: 'UnderReview',
      canSubmitForVerification: true,
      isProfileComplete: true,
      isStorefrontComplete: true,
      isAddressComplete: true,
      isPayoutPlaceholderComplete: true,
      latestVerificationReview: {
        submittedAtUtc: '2026-05-26T10:00:00Z',
        reviewedAtUtc: null,
        rejectionReason: null,
        suspensionReason: null
      }
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Your seller profile is under review');
    expect(compiled.textContent).toContain('prepare product drafts');
    expect(compiled.textContent).toContain('Submitted for review');
    expect(compiled.textContent).not.toContain('Basic seller details');
    expect(dashboardService.getSummary).not.toHaveBeenCalled();
  });

  it('shows rejected seller review reason and allows onboarding edits', async () => {
    onboardingService.getOnboarding.and.resolveTo(createOnboardingResponse({
      verificationStatus: 'Rejected',
      latestVerificationReview: {
        submittedAtUtc: '2026-05-26T10:00:00Z',
        reviewedAtUtc: '2026-05-26T12:00:00Z',
        rejectionReason: 'Storefront details need more evidence.',
        suspensionReason: null
      }
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Update your application and resubmit');
    expect(compiled.textContent).toContain('Review reason: Storefront details need more evidence.');
    expect(compiled.textContent).toContain('Basic seller details');
  });

  it('shows suspended seller restriction and support guidance', async () => {
    onboardingService.getOnboarding.and.resolveTo(createOnboardingResponse({
      verificationStatus: 'Suspended',
      latestVerificationReview: {
        submittedAtUtc: null,
        reviewedAtUtc: null,
        rejectionReason: null,
        suspensionReason: 'Policy review required.'
      }
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Seller account suspended');
    expect(compiled.textContent).toContain('Suspension reason: Policy review required.');
    expect(compiled.textContent).toContain('Contact support before continuing');
    expect(compiled.textContent).not.toContain('Basic seller details');
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
    expect(compiled.textContent).toContain('Sales 30d');
    expect(compiled.textContent).toContain('18 orders');
    expect(compiled.textContent).toContain('Delivery exceptions need review');
    expect(compiled.textContent).toContain('Latest seller events');
    expect(compiled.textContent).toContain('5 unread seller updates');
    expect(compiled.querySelector('a[href="/notifications"]')).not.toBeNull();
    expect(compiled.querySelector('.hf-seller-dashboard-hero')).not.toBeNull();
    expect(compiled.querySelector('.hf-seller-opportunity-card')).not.toBeNull();
    expect(compiled.textContent).toContain('Orders');
    expect(compiled.textContent).not.toContain('Basic seller details');
    expect(dashboardService.getSummary).toHaveBeenCalled();
  });

  it('keeps verified workspace links usable when dashboard summary fails', async () => {
    onboardingService.getOnboarding.and.resolveTo(createOnboardingResponse({
      verificationStatus: 'Verified',
      isProfileComplete: true,
      isStorefrontComplete: true,
      isAddressComplete: true,
      isPayoutPlaceholderComplete: true
    }));
    dashboardService.getSummary.and.rejectWith({ error: { detail: 'Summary temporarily unavailable.' } });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Workspace links remain available below.');
    expect(compiled.querySelector('a[href="/products"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/orders"]')).not.toBeNull();
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

  it('uploads optional verification evidence without changing submit eligibility', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const fileInput = (fixture.nativeElement as HTMLElement).querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['%PDF-1.4'], 'registration.pdf', { type: 'application/pdf' });
    Object.defineProperty(fileInput, 'files', { value: [file] });
    fileInput.dispatchEvent(new Event('change'));

    const evidenceForm = fileInput.closest('form');
    evidenceForm?.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(evidenceService.upload).toHaveBeenCalledWith(file, 'BusinessRegistration', null);
    expect(onboardingService.submitVerification).not.toHaveBeenCalled();
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
    latestVerificationReview: null,
    ...overrides
  };
}

function createDashboardSummary(
  overrides: Partial<SellerDashboardSummaryResponse> = {}
): SellerDashboardSummaryResponse {
  return {
    sellerId: '864c06ff-1b54-4931-a210-d458c014a19f',
    generatedAtUtc: '2026-05-26T14:30:00Z',
    fromUtc: '2026-04-26T14:30:00Z',
    salesLast30Days: 12450,
    ordersLast30Days: 18,
    paidOrderCount: 2,
    processingOrderCount: 1,
    readyToShipOrderCount: 1,
    shippedOrderCount: 3,
    pendingFulfilmentOrders: 4,
    deliveryExceptionOrderCount: 1,
    draftProductCount: 3,
    pendingReviewProductCount: 2,
    publishedProductCount: 8,
    changesRequestedProductCount: 1,
    pendingListingRevisionCount: 1,
    pendingVariantRevisionCount: 1,
    lowStockProductCount: 3,
    outOfStockVariantCount: 2,
    reservedStockCount: 5,
    openReturnCount: 1,
    returnsAwaitingSellerResponseCount: 1,
    openSupportTicketCount: 2,
    activeDisputeCount: 1,
    pendingPayoutAmount: 3400,
    availablePayoutAmount: 1200,
    heldPayoutAmount: 0,
    pendingPayoutCount: 1,
    processingPayoutCount: 0,
    hasPendingPayoutProfileChange: true,
    activeAdCampaignCount: 2,
    pendingAdReviewCount: 1,
    adSpendLast30Days: 480,
    adRevenueLast30Days: 1600,
    unreadNotificationCount: 5,
    alerts: [
      {
        severity: 'danger',
        title: 'Delivery exceptions need review',
        message: 'Failed or returned shipments need seller follow-up.',
        route: '/orders',
        count: 1
      }
    ],
    recentActivity: [
      {
        type: 'Order',
        title: 'Order abc12345',
        status: 'ReadyToShip',
        occurredAtUtc: '2026-05-26T13:00:00Z',
        route: '/orders/00000000-0000-0000-0000-000000000001'
      }
    ],
    ...overrides
  };
}
