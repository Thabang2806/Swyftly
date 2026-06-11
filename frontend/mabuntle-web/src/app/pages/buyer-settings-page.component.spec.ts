import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BuyerAiDiscoveryService } from '../buyer/buyer-ai-discovery.service';
import { BuyerSettingsService } from '../buyer/buyer-settings.service';
import { BuyerSettingsPageComponent } from './buyer-settings-page.component';

describe('BuyerSettingsPageComponent', () => {
  let fixture: ComponentFixture<BuyerSettingsPageComponent>;
  let settingsService: jasmine.SpyObj<BuyerSettingsService>;
  let aiDiscoveryService: jasmine.SpyObj<BuyerAiDiscoveryService>;

  beforeEach(async () => {
    settingsService = jasmine.createSpyObj<BuyerSettingsService>('BuyerSettingsService', [
      'getProfile',
      'updateProfile',
      'getNotificationPreferences',
      'updateNotificationPreferences',
      'listDeliveryAddresses',
      'createDeliveryAddress',
      'updateDeliveryAddress',
      'deleteDeliveryAddress',
      'makeDefaultDeliveryAddress'
    ]);
    aiDiscoveryService = jasmine.createSpyObj<BuyerAiDiscoveryService>('BuyerAiDiscoveryService', [
      'getPreferences',
      'updatePreferences',
      'clearHistory'
    ]);
    settingsService.getProfile.and.resolveTo(createProfile());
    settingsService.updateProfile.and.resolveTo({ ...createProfile(), displayName: 'Thabo', phoneNumber: '+27110000000' });
    settingsService.getNotificationPreferences.and.resolveTo(createPreferences());
    settingsService.listDeliveryAddresses.and.resolveTo([createAddress()]);
    settingsService.createDeliveryAddress.and.resolveTo(createAddress({ deliveryAddressId: 'new-address-id', label: 'Work' }));
    settingsService.updateDeliveryAddress.and.resolveTo(createAddress({ label: 'Updated home' }));
    settingsService.deleteDeliveryAddress.and.resolveTo();
    settingsService.makeDefaultDeliveryAddress.and.resolveTo([createAddress()]);
    settingsService.updateNotificationPreferences.and.resolveTo({
      preferences: createPreferences().preferences.map(preference =>
        preference.category === 'Reviews' ? { ...preference, isEnabled: false, emailEnabled: false } : preference)
    });
    aiDiscoveryService.getPreferences.and.resolveTo({ historyEnabled: false, personalizationEnabled: false, updatedAtUtc: null });
    aiDiscoveryService.updatePreferences.and.resolveTo({ historyEnabled: true, personalizationEnabled: true, updatedAtUtc: '2026-05-29T12:00:00Z' });
    aiDiscoveryService.clearHistory.and.resolveTo();

    await TestBed.configureTestingModule({
      imports: [BuyerSettingsPageComponent],
      providers: [
        provideRouter([]),
        { provide: BuyerSettingsService, useValue: settingsService },
        { provide: BuyerAiDiscoveryService, useValue: aiDiscoveryService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerSettingsPageComponent);
  });

  it('loads profile and notification preferences', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('buyer@example.test');
    expect(compiled.textContent).toContain('Order updates');
    expect(compiled.textContent).toContain('Review moderation');
    expect(compiled.textContent).toContain('Saved delivery addresses');
    expect(compiled.textContent).toContain('AI discovery history');
    expect(compiled.textContent).toContain('Personalized AI discovery');
    expect(compiled.textContent).toContain('Prompts, uploaded images, image previews, base64 content, provider payloads, and full AI responses are not stored.');
    expect(compiled.textContent).toContain('Home');
    expect(compiled.textContent).toContain('Account channel note');
    expect(compiled.textContent).not.toContain('LocalRules');
    expect(compiled.textContent).not.toContain('Not included yet');
  });

  it('saves profile settings', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance as unknown as {
      profileForm: { patchValue(value: { displayName: string; phoneNumber: string }): void };
      saveProfile(): Promise<void>;
    };

    component.profileForm.patchValue({ displayName: 'Thabo', phoneNumber: '+27110000000' });
    await component.saveProfile();

    expect(settingsService.updateProfile).toHaveBeenCalledWith({
      displayName: 'Thabo',
      phoneNumber: '+27110000000'
    });
  });

  it('saves notification preferences', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance as unknown as {
      notificationForm: { controls: { Reviews: { setValue(value: boolean): void } } };
      emailNotificationForm: { controls: { Reviews: { setValue(value: boolean): void } } };
      savePreferences(): Promise<void>;
    };

    component.notificationForm.controls.Reviews.setValue(false);
    component.emailNotificationForm.controls.Reviews.setValue(false);
    await component.savePreferences();

    expect(settingsService.updateNotificationPreferences).toHaveBeenCalledWith({
      preferences: [
        { category: 'Orders', isEnabled: true, emailEnabled: true },
        { category: 'Returns', isEnabled: true, emailEnabled: true },
        { category: 'Reviews', isEnabled: false, emailEnabled: false },
        { category: 'Support', isEnabled: true, emailEnabled: true }
      ]
    });
  });

  it('saves and clears AI discovery history controls', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance as unknown as {
      aiHistoryForm: {
        controls: {
          historyEnabled: { setValue(value: boolean): void };
          personalizationEnabled: { setValue(value: boolean): void };
        }
      };
      saveAiHistoryPreference(): Promise<void>;
      clearAiHistory(): Promise<void>;
    };

    component.aiHistoryForm.controls.historyEnabled.setValue(true);
    component.aiHistoryForm.controls.personalizationEnabled.setValue(true);
    await component.saveAiHistoryPreference();
    await component.clearAiHistory();

    expect(aiDiscoveryService.updatePreferences).toHaveBeenCalledWith({ historyEnabled: true, personalizationEnabled: true });
    expect(aiDiscoveryService.clearHistory).toHaveBeenCalled();
  });

  it('creates a delivery address', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance as unknown as {
      addressForm: { setValue(value: Record<string, string | boolean>): void };
      saveDeliveryAddress(): Promise<void>;
    };

    component.addressForm.setValue({
      label: 'Work',
      recipientName: 'Buyer One',
      phoneNumber: '+27110000000',
      addressLine1: '10 Market Street',
      addressLine2: '',
      suburb: 'Rosebank',
      city: 'Johannesburg',
      province: 'Gauteng',
      postalCode: '2196',
      countryCode: 'ZA',
      deliveryInstructions: 'Leave at reception.',
      isDefault: false
    });
    await component.saveDeliveryAddress();

    expect(settingsService.createDeliveryAddress).toHaveBeenCalledWith({
      label: 'Work',
      recipientName: 'Buyer One',
      phoneNumber: '+27110000000',
      addressLine1: '10 Market Street',
      addressLine2: null,
      suburb: 'Rosebank',
      city: 'Johannesburg',
      province: 'Gauteng',
      postalCode: '2196',
      countryCode: 'ZA',
      deliveryInstructions: 'Leave at reception.',
      isDefault: false
    });
  });
});

function createProfile() {
  return {
    buyerId: 'buyer-id',
    userId: 'user-id',
    email: 'buyer@example.test',
    displayName: null,
    phoneNumber: null,
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z'
  };
}

function createPreferences() {
  return {
    preferences: [
      { category: 'Orders' as const, isEnabled: true, emailEnabled: true },
      { category: 'Returns' as const, isEnabled: true, emailEnabled: true },
      { category: 'Reviews' as const, isEnabled: true, emailEnabled: true },
      { category: 'Support' as const, isEnabled: true, emailEnabled: true }
    ]
  };
}

function createAddress(overrides: Partial<ReturnType<typeof createAddressBase>> = {}) {
  return {
    ...createAddressBase(),
    ...overrides
  };
}

function createAddressBase() {
  return {
    deliveryAddressId: 'address-id',
    label: 'Home',
    recipientName: 'Buyer One',
    phoneNumber: '+27110000000',
    addressLine1: '10 Market Street',
    addressLine2: 'Apartment 4',
    suburb: 'Rosebank',
    city: 'Johannesburg',
    province: 'Gauteng',
    postalCode: '2196',
    countryCode: 'ZA',
    deliveryInstructions: 'Leave at reception.',
    isDefault: true,
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z'
  };
}
