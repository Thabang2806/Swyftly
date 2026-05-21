import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerDeliveryMethodResponse } from '../seller/seller-delivery-method.models';
import { SellerDeliveryMethodService } from '../seller/seller-delivery-method.service';
import { SellerOnboardingResponse } from '../seller/seller-onboarding.models';
import { SellerOnboardingService } from '../seller/seller-onboarding.service';
import { SellerStoreSettingsPageComponent } from './seller-store-settings-page.component';

describe('SellerStoreSettingsPageComponent', () => {
  let fixture: ComponentFixture<SellerStoreSettingsPageComponent>;
  let deliveryMethodService: jasmine.SpyObj<SellerDeliveryMethodService>;
  let onboardingService: jasmine.SpyObj<SellerOnboardingService>;

  beforeEach(async () => {
    deliveryMethodService = jasmine.createSpyObj<SellerDeliveryMethodService>(
      'SellerDeliveryMethodService',
      ['list', 'create', 'update', 'activate', 'deactivate']);
    deliveryMethodService.list.and.resolveTo([createDeliveryMethod()]);
    deliveryMethodService.create.and.resolveTo(createDeliveryMethod({ deliveryMethodId: 'new-method-id', name: 'Express courier', methodType: 'Express' }));
    deliveryMethodService.update.and.resolveTo(createDeliveryMethod({ name: 'Updated courier' }));
    deliveryMethodService.activate.and.resolveTo(createDeliveryMethod({ isActive: true }));
    deliveryMethodService.deactivate.and.resolveTo(createDeliveryMethod({ isActive: false }));
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

    await TestBed.configureTestingModule({
      imports: [SellerStoreSettingsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerDeliveryMethodService, useValue: deliveryMethodService },
        { provide: SellerOnboardingService, useValue: onboardingService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerStoreSettingsPageComponent);
  });

  it('loads store settings with storefront preview and read-only payout messaging', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Store settings');
    expect(compiled.textContent).toContain('Verified');
    expect(compiled.textContent).toContain('Standard courier');
    expect(compiled.querySelector('a[href="/seller/luxe-seller"]')).not.toBeNull();
    expect(compiled.textContent).toContain('Payout details are read-only here');
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
