import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerDeliveryMethodResponse } from '../seller/seller-delivery-method.models';
import { SellerDeliveryMethodService } from '../seller/seller-delivery-method.service';
import { SellerNotificationService } from '../seller/seller-notification.service';
import { SellerOnboardingResponse } from '../seller/seller-onboarding.models';
import { SellerOnboardingService } from '../seller/seller-onboarding.service';
import {
  SellerPayoutProfileChangeRequestResponse,
  SellerPayoutProfileChangeStateResponse
} from '../seller/seller-payout-profile-change.models';
import { SellerPayoutProfileChangeService } from '../seller/seller-payout-profile-change.service';
import { SellerStorePolicyService } from '../seller/seller-store-policy.service';
import { SellerPolicyResponse } from '../shared/seller-policy.models';
import { SellerStoreSettingsPageComponent } from './seller-store-settings-page.component';

describe('SellerStoreSettingsPageComponent', () => {
  let fixture: ComponentFixture<SellerStoreSettingsPageComponent>;
  let deliveryMethodService: jasmine.SpyObj<SellerDeliveryMethodService>;
  let notificationService: jasmine.SpyObj<SellerNotificationService>;
  let onboardingService: jasmine.SpyObj<SellerOnboardingService>;
  let payoutProfileChangeService: jasmine.SpyObj<SellerPayoutProfileChangeService>;
  let storePolicyService: jasmine.SpyObj<SellerStorePolicyService>;

  beforeEach(async () => {
    deliveryMethodService = jasmine.createSpyObj<SellerDeliveryMethodService>(
      'SellerDeliveryMethodService',
      ['list', 'create', 'update', 'activate', 'deactivate']);
    deliveryMethodService.list.and.resolveTo([createDeliveryMethod()]);
    deliveryMethodService.create.and.resolveTo(createDeliveryMethod({ deliveryMethodId: 'new-method-id', name: 'Express courier', methodType: 'Express' }));
    deliveryMethodService.update.and.resolveTo(createDeliveryMethod({ name: 'Updated courier' }));
    deliveryMethodService.activate.and.resolveTo(createDeliveryMethod({ isActive: true }));
    deliveryMethodService.deactivate.and.resolveTo(createDeliveryMethod({ isActive: false }));
    notificationService = jasmine.createSpyObj<SellerNotificationService>(
      'SellerNotificationService',
      ['getPreferences', 'updatePreferences'],
      { unreadCount: signal(0) });
    notificationService.getPreferences.and.resolveTo({
      preferences: [
        { category: 'Verification', isEnabled: true, emailEnabled: true },
        { category: 'Products', isEnabled: true, emailEnabled: true },
        { category: 'Revisions', isEnabled: true, emailEnabled: true },
        { category: 'Ads', isEnabled: true, emailEnabled: true }
      ]
    });
    notificationService.updatePreferences.and.resolveTo({
      preferences: [
        { category: 'Verification', isEnabled: true, emailEnabled: true },
        { category: 'Products', isEnabled: false, emailEnabled: true },
        { category: 'Revisions', isEnabled: true, emailEnabled: true },
        { category: 'Ads', isEnabled: true, emailEnabled: true }
      ]
    });
    onboardingService = jasmine.createSpyObj<SellerOnboardingService>(
      'SellerOnboardingService',
      ['getOnboarding', 'updateProfile', 'updateStorefront', 'updateAddress']);
    onboardingService.getOnboarding.and.resolveTo(createOnboardingResponse());
    onboardingService.updateProfile.and.resolveTo(createOnboardingResponse({ profile: {
      displayName: 'Updated Seller',
      contactEmail: 'seller@example.test',
      phoneNumber: '+27110000000',
      businessType: 'Individual',
      businessName: null
    } }));
    onboardingService.updateStorefront.and.resolveTo(createOnboardingResponse({ storefront: {
      storeName: 'Updated Store',
      slug: 'updated-store',
      description: 'Updated copy',
      logoUrl: null,
      bannerUrl: null,
      isPublished: true
    } }));
    onboardingService.updateAddress.and.resolveTo(createOnboardingResponse());
    payoutProfileChangeService = jasmine.createSpyObj<SellerPayoutProfileChangeService>(
      'SellerPayoutProfileChangeService',
      ['getState', 'upsertDraft', 'submitForReview', 'cancel']);
    payoutProfileChangeService.getState.and.resolveTo(createPayoutChangeState());
    payoutProfileChangeService.upsertDraft.and.resolveTo(createPayoutChangeState({
      activeRequest: createPayoutChangeRequest({ status: 'Draft', proposedPayoutProviderReference: 'provider-ref-next' })
    }));
    payoutProfileChangeService.submitForReview.and.resolveTo(createPayoutChangeState({
      activeRequest: createPayoutChangeRequest({ status: 'PendingReview' })
    }));
    payoutProfileChangeService.cancel.and.resolveTo(createPayoutChangeState({
      activeRequest: null,
      latestRequest: createPayoutChangeRequest({ status: 'Cancelled' })
    }));
    storePolicyService = jasmine.createSpyObj<SellerStorePolicyService>(
      'SellerStorePolicyService',
      ['getPolicy', 'updatePolicy']);
    storePolicyService.getPolicy.and.resolveTo(createStorePolicy());
    storePolicyService.updatePolicy.and.resolveTo(createStorePolicy({
      returnWindowDays: 21,
      returnPolicy: 'Updated return policy.'
    }));

    await TestBed.configureTestingModule({
      imports: [SellerStoreSettingsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerDeliveryMethodService, useValue: deliveryMethodService },
        { provide: SellerNotificationService, useValue: notificationService },
        { provide: SellerOnboardingService, useValue: onboardingService },
        { provide: SellerPayoutProfileChangeService, useValue: payoutProfileChangeService },
        { provide: SellerStorePolicyService, useValue: storePolicyService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerStoreSettingsPageComponent);
  });

  it('loads store settings with storefront preview and payout change messaging', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Store settings');
    expect(compiled.textContent).toContain('Verified');
    expect(compiled.textContent).toContain('Standard courier');
    expect(compiled.querySelector('a[href="/seller/luxe-seller"]')).not.toBeNull();
    expect(compiled.textContent).toContain('Payout profile changes');
    expect(compiled.textContent).toContain('provider-ref');
    expect(compiled.textContent).toContain('Seller notification preferences');
    expect(compiled.textContent).toContain('Store policies');
    expect(compiled.textContent).toContain('Policy completeness');
    expect(compiled.textContent).toContain('Complete');
  });

  it('saves storefront details through existing onboarding service', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      storefrontForm: { patchValue(value: Record<string, unknown>): void };
      saveStorefront(): Promise<void>;
    };

    component.storefrontForm.patchValue({
      storeName: 'Updated Store',
      slug: 'updated-store',
      description: 'Updated copy',
      logoUrl: '',
      bannerUrl: ''
    });

    await component.saveStorefront();

    expect(onboardingService.updateStorefront).toHaveBeenCalledWith({
      storeName: 'Updated Store',
      slug: 'updated-store',
      description: 'Updated copy',
      logoUrl: null,
      bannerUrl: null
    });
  });

  it('creates seller delivery methods through delivery method service', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      deliveryMethodForm: { patchValue(value: Record<string, unknown>): void };
      saveDeliveryMethod(): Promise<void>;
    };

    component.deliveryMethodForm.patchValue({
      name: 'Express courier',
      description: 'Faster delivery.',
      methodType: 'Express',
      countryCode: 'za',
      province: '',
      basePrice: 120,
      freeShippingThreshold: null,
      estimatedMinDays: 1,
      estimatedMaxDays: 2,
      displayOrder: 5,
      isActive: true
    });

    await component.saveDeliveryMethod();

    expect(deliveryMethodService.create).toHaveBeenCalledWith({
      name: 'Express courier',
      description: 'Faster delivery.',
      methodType: 'Express',
      countryCode: 'ZA',
      province: null,
      basePrice: 120,
      freeShippingThreshold: null,
      estimatedMinDays: 1,
      estimatedMaxDays: 2,
      displayOrder: 5,
      isActive: true
    });
  });

  it('saves a payout profile change draft through the change request service', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      payoutChangeForm: { patchValue(value: Record<string, unknown>): void };
      savePayoutChangeDraft(): Promise<void>;
    };

    component.payoutChangeForm.patchValue({
      payoutProviderReference: 'provider-ref-next',
      reason: 'Updated payout account token.'
    });

    await component.savePayoutChangeDraft();

    expect(payoutProfileChangeService.upsertDraft).toHaveBeenCalledWith({
      payoutProviderReference: 'provider-ref-next',
      reason: 'Updated payout account token.'
    });
  });

  it('saves seller notification preferences', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      notificationPreferenceForm: { patchValue(value: Record<string, unknown>): void };
      saveNotificationPreferences(): Promise<void>;
    };

    component.notificationPreferenceForm.patchValue({
      Products: {
        isEnabled: false,
        emailEnabled: true
      }
    });

    await component.saveNotificationPreferences();

    expect(notificationService.updatePreferences).toHaveBeenCalledWith({
      preferences: [
        { category: 'Verification', isEnabled: true, emailEnabled: true },
        { category: 'Products', isEnabled: false, emailEnabled: true },
        { category: 'Revisions', isEnabled: true, emailEnabled: true },
        { category: 'Ads', isEnabled: true, emailEnabled: true },
        { category: 'Reports', isEnabled: true, emailEnabled: true }
      ]
    });
  });

  it('saves buyer-facing store policy settings', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      policyForm: { patchValue(value: Record<string, unknown>): void };
      saveStorePolicy(): Promise<void>;
    };

    component.policyForm.patchValue({
      returnWindowDays: 21,
      returnPolicy: 'Updated return policy.',
      exchangePolicy: '',
      fulfilmentPolicy: 'Dispatched within two business days.',
      supportPolicy: 'Message support for order questions.',
      careInstructions: '',
      productDisclaimer: ''
    });

    await component.saveStorePolicy();

    expect(storePolicyService.updatePolicy).toHaveBeenCalledWith({
      returnWindowDays: 21,
      returnPolicy: 'Updated return policy.',
      exchangePolicy: null,
      fulfilmentPolicy: 'Dispatched within two business days.',
      supportPolicy: 'Message support for order questions.',
      careInstructions: null,
      productDisclaimer: null
    });
  });
});

function createOnboardingResponse(
  overrides: Partial<SellerOnboardingResponse> = {}
): SellerOnboardingResponse {
  return {
    sellerId: 'seller-id',
    verificationStatus: 'Verified',
    canSubmitForVerification: false,
    isProfileComplete: true,
    isStorefrontComplete: true,
    isAddressComplete: true,
    isPayoutPlaceholderComplete: true,
    profile: {
      displayName: 'Luxe Seller',
      contactEmail: 'seller@example.test',
      phoneNumber: '+27110000000',
      businessType: 'Individual',
      businessName: null
    },
    storefront: {
      storeName: 'Luxe Seller',
      slug: 'luxe-seller',
      description: 'Curated fashion edits',
      logoUrl: null,
      bannerUrl: null,
      isPublished: true
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
      payoutProviderReference: 'provider-ref',
      hasSubmittedPlaceholder: true,
      isAdminApproved: true
    },
    latestVerificationReview: null,
    ...overrides
  };
}

function createDeliveryMethod(
  overrides: Partial<SellerDeliveryMethodResponse> = {}
): SellerDeliveryMethodResponse {
  return {
    deliveryMethodId: 'delivery-method-id',
    sellerId: 'seller-id',
    name: 'Standard courier',
    description: 'Door-to-door delivery.',
    methodType: 'Standard',
    countryCode: 'ZA',
    province: 'Gauteng',
    basePrice: 75,
    freeShippingThreshold: 1000,
    estimatedMinDays: 2,
    estimatedMaxDays: 5,
    displayOrder: 10,
    isActive: true,
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z',
    ...overrides
  };
}

function createPayoutChangeState(
  overrides: Partial<SellerPayoutProfileChangeStateResponse> = {}
): SellerPayoutProfileChangeStateResponse {
  return {
    currentPayoutProfile: {
      payoutProviderReference: 'provider-ref',
      isAdminApproved: true,
      approvedAtUtc: '2026-05-21T10:00:00Z',
      approvedByUserId: 'admin-user-id'
    },
    activeRequest: null,
    latestRequest: null,
    ...overrides
  };
}

function createPayoutChangeRequest(
  overrides: Partial<SellerPayoutProfileChangeRequestResponse> = {}
): SellerPayoutProfileChangeRequestResponse {
  return {
    requestId: 'change-request-id',
    status: 'Draft',
    proposedPayoutProviderReference: 'provider-ref-next',
    reason: 'Updated payout account token.',
    reviewReason: null,
    submittedAtUtc: null,
    cancelledAtUtc: null,
    reviewedAtUtc: null,
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z',
    ...overrides
  };
}

function createStorePolicy(overrides: Partial<SellerPolicyResponse> = {}): SellerPolicyResponse {
  return {
    returnWindowDays: 14,
    returnPolicy: 'Returns are reviewed for delivered items in original condition.',
    exchangePolicy: 'Exchanges depend on stock availability.',
    fulfilmentPolicy: 'Orders are usually dispatched within 2-3 business days.',
    supportPolicy: 'Message support with order issues and product questions.',
    careInstructions: 'Follow product care notes on each item.',
    productDisclaimer: 'Colour and fit may vary slightly by screen and size.',
    isComplete: true,
    missingFields: [],
    updatedAtUtc: '2026-05-21T10:00:00Z',
    ...overrides
  };
}
