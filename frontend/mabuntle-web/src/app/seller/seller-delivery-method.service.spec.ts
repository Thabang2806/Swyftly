import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerDeliveryMethodRequest } from './seller-delivery-method.models';
import { SellerDeliveryMethodService } from './seller-delivery-method.service';

describe('SellerDeliveryMethodService', () => {
  let service: SellerDeliveryMethodService;
  let httpTestingController: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/api/seller/delivery-methods`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerDeliveryMethodService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('lists and writes seller delivery methods', async () => {
    const listPromise = service.list();
    const listRequest = httpTestingController.expectOne(baseUrl);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([createResponse()]);
    expect((await listPromise)[0].deliveryMethodId).toBe('delivery-method-id');

    const createPromise = service.create(createRequest());
    const createRequestMessage = httpTestingController.expectOne(baseUrl);
    expect(createRequestMessage.request.method).toBe('POST');
    expect(createRequestMessage.request.body).toEqual(createRequest());
    createRequestMessage.flush(createResponse());
    await expectAsync(createPromise).toBeResolved();

    const updatePromise = service.update('delivery-method-id', createRequest({ name: 'Express courier' }));
    const updateRequest = httpTestingController.expectOne(`${baseUrl}/delivery-method-id`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body.name).toBe('Express courier');
    updateRequest.flush(createResponse({ name: 'Express courier' }));
    await expectAsync(updatePromise).toBeResolved();
  });

  it('activates and deactivates methods', async () => {
    const deactivatePromise = service.deactivate('delivery-method-id');
    const deactivateRequest = httpTestingController.expectOne(`${baseUrl}/delivery-method-id/deactivate`);
    expect(deactivateRequest.request.method).toBe('POST');
    deactivateRequest.flush(createResponse({ isActive: false }));
    expect((await deactivatePromise).isActive).toBeFalse();

    const activatePromise = service.activate('delivery-method-id');
    const activateRequest = httpTestingController.expectOne(`${baseUrl}/delivery-method-id/activate`);
    expect(activateRequest.request.method).toBe('POST');
    activateRequest.flush(createResponse({ isActive: true }));
    expect((await activatePromise).isActive).toBeTrue();
  });
});

function createRequest(overrides: Partial<SellerDeliveryMethodRequest> = {}): SellerDeliveryMethodRequest {
  return {
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
    ...overrides
  };
}

function createResponse(overrides: Record<string, unknown> = {}) {
  return {
    deliveryMethodId: 'delivery-method-id',
    sellerId: 'seller-id',
    ...createRequest(),
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z',
    ...overrides
  };
}
