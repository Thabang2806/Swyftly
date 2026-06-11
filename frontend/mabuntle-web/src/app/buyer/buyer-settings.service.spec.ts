import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { BuyerSettingsService } from './buyer-settings.service';

describe('BuyerSettingsService', () => {
  let service: BuyerSettingsService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BuyerSettingsService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls buyer profile settings endpoints', async () => {
    const profilePromise = service.getProfile();
    const profileRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/profile`);
    expect(profileRequest.request.method).toBe('GET');
    profileRequest.flush(createProfile());
    await expectAsync(profilePromise).toBeResolvedTo(createProfile());

    const updatePromise = service.updateProfile({ displayName: 'Thabo', phoneNumber: '+27110000000' });
    const updateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/profile`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual({ displayName: 'Thabo', phoneNumber: '+27110000000' });
    updateRequest.flush({ ...createProfile(), displayName: 'Thabo', phoneNumber: '+27110000000' });
    await expectAsync(updatePromise).toBeResolved();
  });

  it('calls notification preference endpoints', async () => {
    const preferences = {
      preferences: [
        { category: 'Orders' as const, isEnabled: true, emailEnabled: true },
        { category: 'Reviews' as const, isEnabled: false, emailEnabled: true }
      ]
    };

    const getPromise = service.getNotificationPreferences();
    const getRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/notification-preferences`);
    expect(getRequest.request.method).toBe('GET');
    getRequest.flush(preferences);
    await expectAsync(getPromise).toBeResolvedTo(preferences);

    const updatePromise = service.updateNotificationPreferences(preferences);
    const updateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/notification-preferences`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(preferences);
    updateRequest.flush(preferences);
    await expectAsync(updatePromise).toBeResolvedTo(preferences);
  });

  it('calls delivery address endpoints', async () => {
    const address = createAddress();
    const request = {
      label: 'Home',
      recipientName: 'Thabo',
      phoneNumber: '+27110000000',
      addressLine1: '10 Market Street',
      addressLine2: null,
      suburb: 'Rosebank',
      city: 'Johannesburg',
      province: 'Gauteng',
      postalCode: '2196',
      countryCode: 'ZA',
      deliveryInstructions: 'Leave at reception.',
      isDefault: true
    };

    const listPromise = service.listDeliveryAddresses();
    const listRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/delivery-addresses`);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([address]);
    await expectAsync(listPromise).toBeResolvedTo([address]);

    const createPromise = service.createDeliveryAddress(request);
    const createRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/delivery-addresses`);
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual(request);
    createRequest.flush(address);
    await expectAsync(createPromise).toBeResolvedTo(address);

    const verifyRequestBody = {
      recipientName: request.recipientName,
      phoneNumber: request.phoneNumber,
      addressLine1: request.addressLine1,
      addressLine2: request.addressLine2,
      suburb: request.suburb,
      city: request.city,
      province: request.province,
      postalCode: request.postalCode,
      countryCode: request.countryCode,
      deliveryInstructions: request.deliveryInstructions
    };
    const verifyPromise = service.verifyDeliveryAddress(verifyRequestBody);
    const verifyRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/delivery-addresses/verify`);
    expect(verifyRequest.request.method).toBe('POST');
    expect(verifyRequest.request.body).toEqual(verifyRequestBody);
    verifyRequest.flush({
      ...verifyRequestBody,
      verificationStatus: 'Verified',
      verificationProvider: 'LocalRules',
      verificationWarnings: [],
      verifiedAtUtc: '2026-05-21T10:00:00Z'
    });
    await expectAsync(verifyPromise).toBeResolved();

    const updatePromise = service.updateDeliveryAddress('address-id', request);
    const updateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/delivery-addresses/address-id`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(request);
    updateRequest.flush(address);
    await expectAsync(updatePromise).toBeResolvedTo(address);

    const defaultPromise = service.makeDefaultDeliveryAddress('address-id');
    const defaultRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/delivery-addresses/address-id/make-default`);
    expect(defaultRequest.request.method).toBe('POST');
    defaultRequest.flush([address]);
    await expectAsync(defaultPromise).toBeResolvedTo([address]);

    const deletePromise = service.deleteDeliveryAddress('address-id');
    const deleteRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/delivery-addresses/address-id`);
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);
    await expectAsync(deletePromise).toBeResolved();
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

function createAddress() {
  return {
    deliveryAddressId: 'address-id',
    label: 'Home',
    recipientName: 'Thabo',
    phoneNumber: '+27110000000',
    addressLine1: '10 Market Street',
    addressLine2: null,
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
