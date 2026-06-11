import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../environments/environment';
import { BuyerGrowthTelemetryService } from './buyer-growth-telemetry.service';

describe('BuyerGrowthTelemetryService', () => {
  let service: BuyerGrowthTelemetryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BuyerGrowthTelemetryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('posts buyer growth events to the buyer telemetry endpoint', () => {
    service.recordEvent({
      eventType: 'AssistantShopHandoff',
      sourceTool: 'Assistant',
      resultCount: 4,
      confidenceBand: 'Medium',
      category: 'Dresses',
      colour: 'Black',
      sourceRoute: '/assistant'
    });

    const request = httpMock.expectOne(`${environment.apiBaseUrl}/api/buyer/growth-events`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(jasmine.objectContaining({
      eventType: 'AssistantShopHandoff',
      sourceTool: 'Assistant',
      resultCount: 4
    }));
    request.flush({ eventId: 'event-id', status: 'Recorded' });
  });

  it('swallows telemetry failures so buyer flows remain non-blocking', () => {
    service.recordEvent({
      eventType: 'VisualSearchSubmitted',
      sourceTool: 'VisualSearch',
      resultCount: 0,
      sourceRoute: '/visual-search'
    });

    const request = httpMock.expectOne(`${environment.apiBaseUrl}/api/buyer/growth-events`);
    request.flush({ title: 'Unavailable' }, { status: 503, statusText: 'Service Unavailable' });

    expect().nothing();
  });
});
