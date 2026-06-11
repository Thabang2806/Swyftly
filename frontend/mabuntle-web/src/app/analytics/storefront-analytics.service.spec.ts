import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { StorefrontAnalyticsService } from './storefront-analytics.service';

describe('StorefrontAnalyticsService', () => {
  let service: StorefrontAnalyticsService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    localStorage.removeItem('mabuntleVisitorId');
    history.replaceState({}, '', '/');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(StorefrontAnalyticsService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
    localStorage.removeItem('mabuntleVisitorId');
    history.replaceState({}, '', '/');
  });

  it('records product views with a stable first-party visitor id', () => {
    service.trackProductView('product-id', '/product/sample');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/analytics/storefront-events`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(jasmine.objectContaining({
      eventType: 'ProductViewed',
      productId: 'product-id',
      sourceRoute: '/product/sample',
      sourceCategory: 'Direct'
    }));
    const visitorId = request.request.body.anonymousVisitorId as string;
    expect(visitorId).toMatch(/^[A-Za-z0-9_-]{8,128}$/);
    request.flush({ recorded: true, eventId: 'event-id', status: 'Recorded' });

    service.trackProductAddedToCart('product-id', '/product/sample');
    const secondRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/analytics/storefront-events`);
    expect(secondRequest.request.body.anonymousVisitorId).toBe(visitorId);
    expect(secondRequest.request.body.eventType).toBe('ProductAddedToCart');
    secondRequest.flush({ recorded: true, eventId: 'event-id-2', status: 'Recorded' });
  });

  it('sends UTM attribution without blocking storefront events', () => {
    history.replaceState({}, '', '/product/sample?utm_source=newsletter&utm_medium=email&utm_campaign=launch');

    service.trackProductView('product-id', '/product/sample?utm_source=newsletter');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/analytics/storefront-events`);
    expect(request.request.body).toEqual(jasmine.objectContaining({
      eventType: 'ProductViewed',
      productId: 'product-id',
      utmSource: 'newsletter',
      utmMedium: 'email',
      utmCampaign: 'launch',
      sourceCategory: 'Email'
    }));
    request.flush({ recorded: true, eventId: 'event-id', status: 'Recorded' });
  });
});
