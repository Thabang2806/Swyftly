import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminPickupPointRequest, AdminPickupPointResponse } from './admin-pickup-point.models';
import { AdminPickupPointService } from './admin-pickup-point.service';

describe('AdminPickupPointService', () => {
  let service: AdminPickupPointService;
  let httpTestingController: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/api/admin/pickup-points`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminPickupPointService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls pickup point endpoints', async () => {
    const request = createRequest();
    const response = createResponse();

    const listPromise = service.list();
    const listRequest = httpTestingController.expectOne(baseUrl);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([response]);
    await expectAsync(listPromise).toBeResolvedTo([response]);

    const createPromise = service.create(request);
    const createHttpRequest = httpTestingController.expectOne(baseUrl);
    expect(createHttpRequest.request.method).toBe('POST');
    expect(createHttpRequest.request.body).toEqual(request);
    createHttpRequest.flush(response);
    await expectAsync(createPromise).toBeResolvedTo(response);

    const updatePromise = service.update('pickup-id', request);
    const updateRequest = httpTestingController.expectOne(`${baseUrl}/pickup-id`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(request);
    updateRequest.flush(response);
    await expectAsync(updatePromise).toBeResolvedTo(response);

    const activatePromise = service.activate('pickup-id');
    const activateRequest = httpTestingController.expectOne(`${baseUrl}/pickup-id/activate`);
    expect(activateRequest.request.method).toBe('POST');
    activateRequest.flush({ ...response, isActive: true });
    await expectAsync(activatePromise).toBeResolved();

    const deactivatePromise = service.deactivate('pickup-id');
    const deactivateRequest = httpTestingController.expectOne(`${baseUrl}/pickup-id/deactivate`);
    expect(deactivateRequest.request.method).toBe('POST');
    deactivateRequest.flush({ ...response, isActive: false });
    await expectAsync(deactivatePromise).toBeResolved();
  });
});

function createRequest(): AdminPickupPointRequest {
  return {
    providerName: 'Manual',
    code: 'JHB-ROSEBANK-001',
    name: 'Rosebank Pickup Counter',
    addressLine1: '10 Market Street',
    addressLine2: null,
    suburb: 'Rosebank',
    city: 'Johannesburg',
    province: 'Gauteng',
    postalCode: '2196',
    countryCode: 'ZA',
    latitude: null,
    longitude: null,
    openingHours: 'Mon-Fri 09:00-17:00',
    isActive: true
  };
}

function createResponse(): AdminPickupPointResponse {
  return {
    ...createRequest(),
    pickupPointId: 'pickup-id',
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z'
  };
}
