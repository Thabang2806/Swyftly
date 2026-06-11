import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { BuyerAiDiscoveryService } from './buyer-ai-discovery.service';

describe('BuyerAiDiscoveryService', () => {
  let service: BuyerAiDiscoveryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BuyerAiDiscoveryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads and saves AI discovery preferences', async () => {
    const getPromise = service.getPreferences();
    const getRequest = httpMock.expectOne(`${environment.apiBaseUrl}/api/buyer/ai-discovery/preferences`);
    expect(getRequest.request.method).toBe('GET');
    getRequest.flush({ historyEnabled: false, personalizationEnabled: false, updatedAtUtc: null });
    expect((await getPromise).historyEnabled).toBeFalse();

    const putPromise = service.updatePreferences({ historyEnabled: true, personalizationEnabled: true });
    const putRequest = httpMock.expectOne(`${environment.apiBaseUrl}/api/buyer/ai-discovery/preferences`);
    expect(putRequest.request.method).toBe('PUT');
    expect(putRequest.request.body).toEqual({ historyEnabled: true, personalizationEnabled: true });
    putRequest.flush({ historyEnabled: true, personalizationEnabled: true, updatedAtUtc: '2026-05-29T12:00:00Z' });
    const preference = await putPromise;
    expect(preference.historyEnabled).toBeTrue();
    expect(preference.personalizationEnabled).toBeTrue();
  });

  it('loads filtered history and deletes history rows', async () => {
    const listPromise = service.getHistory({ page: 2, pageSize: 10, tool: 'Assistant' });
    const listRequest = httpMock.expectOne(request =>
      request.url === `${environment.apiBaseUrl}/api/buyer/ai-discovery/history`
      && request.params.get('page') === '2'
      && request.params.get('pageSize') === '10'
      && request.params.get('tool') === 'Assistant');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], totalCount: 0, page: 2, pageSize: 10 });
    expect((await listPromise).page).toBe(2);

    const deletePromise = service.deleteHistoryItem('history-id');
    const deleteRequest = httpMock.expectOne(`${environment.apiBaseUrl}/api/buyer/ai-discovery/history/history-id`);
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);
    await deletePromise;

    const clearPromise = service.clearHistory();
    const clearRequest = httpMock.expectOne(`${environment.apiBaseUrl}/api/buyer/ai-discovery/history`);
    expect(clearRequest.request.method).toBe('DELETE');
    clearRequest.flush(null);
    await clearPromise;
  });
});
