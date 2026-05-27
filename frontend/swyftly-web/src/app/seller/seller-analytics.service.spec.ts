import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerAnalyticsService } from './seller-analytics.service';

describe('SellerAnalyticsService', () => {
  let service: SellerAnalyticsService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerAnalyticsService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads the seller analytics summary', async () => {
    const promise = service.getSummary();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/analytics/summary`);
    expect(request.request.method).toBe('GET');
    request.flush(createSummary());

    const response = await promise;
    expect(response.totalSales).toBe(998);
    expect(response.adPerformance.clicks).toBe(5);
  });

  it('loads seller analytics performance with filters', async () => {
    const promise = service.getPerformance({
      fromUtc: '2026-05-01T00:00:00.000Z',
      toUtc: '2026-05-31T00:00:00.000Z',
      bucket: 'Week',
      sourceCategory: 'Email'
    });

    const request = httpTestingController.expectOne(req =>
      req.url === `${environment.apiBaseUrl}/api/seller/analytics/performance`
      && req.params.get('fromUtc') === '2026-05-01T00:00:00.000Z'
      && req.params.get('toUtc') === '2026-05-31T00:00:00.000Z'
      && req.params.get('bucket') === 'Week'
      && req.params.get('sourceCategory') === 'Email');
    expect(request.request.method).toBe('GET');
    request.flush(createPerformance());

    const response = await promise;
    expect(response.salesTrend[0].grossSales).toBe(998);
    expect(response.productPerformance[0].productTitle).toBe('Seller One Product');
    expect(response.funnelSummary.productViews).toBe(10);
    expect(response.sourceBreakdown[0].sourceCategory).toBe('Email');
  });

  it('builds seller analytics csv export urls', () => {
    const url = service.getCsvExportUrl('Products', {
      fromUtc: '2026-05-01T00:00:00.000Z',
      toUtc: '2026-05-31T00:00:00.000Z',
      bucket: 'Day',
      sourceCategory: 'Referral'
    });

    expect(url).toContain(`${environment.apiBaseUrl}/api/seller/analytics/export.csv?`);
    expect(url).toContain('report=Products');
    expect(url).toContain('fromUtc=2026-05-01T00:00:00.000Z');
    expect(url).toContain('toUtc=2026-05-31T00:00:00.000Z');
    expect(url).toContain('bucket=Day');
    expect(url).toContain('sourceCategory=Referral');

    const funnelUrl = service.getCsvExportUrl('Funnel');
    expect(funnelUrl).toContain('report=Funnel');
  });

  it('loads and updates the scheduled report settings', async () => {
    const getPromise = service.getReportSchedule();
    const getRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/analytics/report-schedule`);
    expect(getRequest.request.method).toBe('GET');
    getRequest.flush(createSchedule());
    expect((await getPromise).frequency).toBe('Weekly');

    const updatePromise = service.updateReportSchedule({
      isEnabled: true,
      frequency: 'Weekly',
      reportRange: 'Last30Days',
      sendDayOfWeek: 'Monday',
      sendDayOfMonth: null,
      sendTimeLocal: '08:00',
      timeZoneId: 'Africa/Johannesburg'
    });
    const updateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/analytics/report-schedule`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body.isEnabled).toBeTrue();
    updateRequest.flush(createSchedule());
    expect((await updatePromise).isEnabled).toBeTrue();
  });

  it('sends a test seller analytics digest', async () => {
    const promise = service.sendTestReportDigest();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/analytics/report-schedule/send-test`);
    expect(request.request.method).toBe('POST');
    request.flush({ isSuccess: true, notificationId: 'notification-id', failureReason: null });

    expect((await promise).notificationId).toBe('notification-id');
  });
});

function createSummary() {
  return {
    sellerId: 'seller-id',
    totalSales: 998,
    orderCount: 1,
    averageOrderValue: 998,
    conversionRatePlaceholder: 0,
    productsSold: 2,
    totalRefunded: 100,
    refundRate: 1,
    returnRate: 1,
    topProducts: [],
    lowStockProducts: [],
    adPerformance: {
      campaignCount: 1,
      impressions: 100,
      clicks: 5,
      clickThroughRate: 0.05,
      spend: 25,
      ordersGenerated: 1,
      revenueGenerated: 499,
      topCampaigns: []
    },
    aiUsage: {
      requests: 3,
      successfulRequests: 2,
      failedRequests: 1,
      estimatedCost: 0.02,
      averageLatencyMs: 100,
      suggestionsGenerated: 2,
      suggestionsAccepted: 1,
      suggestionAcceptanceRate: 0.5,
      productsImprovedWithAi: 1,
      averageListingQualityScore: 70,
      averageQualityScoreImprovement: null,
      qualityScoreImprovementNote: 'Pre-AI baseline quality scores are not captured yet.',
      fieldValuesAccepted: 1,
      fieldValuesEdited: 1
    }
  };
}

function createPerformance() {
  return {
    sellerId: 'seller-id',
    fromUtc: '2026-05-01T00:00:00.000Z',
    toUtc: '2026-05-31T00:00:00.000Z',
    bucket: 'Week',
    salesTrend: [{
      periodStartUtc: '2026-05-01T00:00:00.000Z',
      periodEndUtc: '2026-05-08T00:00:00.000Z',
      orderCount: 1,
      grossSales: 998,
      refundedAmount: 100,
      netSales: 898,
      unitsSold: 2
    }],
    productPerformance: [{
      productId: 'product-id',
      productTitle: 'Seller One Product',
      productSlug: 'seller-one-product',
      status: 'Published',
      unitsSold: 2,
      grossSales: 998,
      refundedAmount: 100,
      returnCount: 1,
      returnRate: 0.5,
      stockQuantity: 3,
      reservedQuantity: 0,
      availableQuantity: 3
    }],
    inventoryPerformance: [],
    adPerformance: [],
    customerCareSummary: {
      returnCount: 1,
      openReturnCount: 1,
      refundCount: 1,
      refundedAmount: 100,
      supportTicketCount: 1,
      openSupportTicketCount: 1,
      disputeCount: 1,
      activeDisputeCount: 1
    },
    funnelSummary: {
      storefrontViews: 3,
      productViews: 10,
      addToCartCount: 2,
      checkoutStartCount: 1,
      orderCreatedCount: 1,
      paidOrderCount: 1,
      productViewToCartRate: 0.2,
      checkoutToPaidRate: 1
    },
    funnelTrend: [{
      periodStartUtc: '2026-05-01T00:00:00.000Z',
      periodEndUtc: '2026-05-08T00:00:00.000Z',
      storefrontViews: 3,
      productViews: 10,
      addToCartCount: 2,
      checkoutStartCount: 1,
      orderCreatedCount: 1,
      paidOrderCount: 1,
      productViewToCartRate: 0.2,
      checkoutToPaidRate: 1
    }],
    productFunnel: [{
      productId: 'product-id',
      productTitle: 'Seller One Product',
      productSlug: 'seller-one-product',
      productViews: 10,
      addToCartCount: 2,
      paidOrderCount: 1,
      revenue: 998,
      productViewToCartRate: 0.2,
      productViewToPaidRate: 0.1,
      dominantSourceCategory: 'Email',
      topUtmSource: 'newsletter',
      topReferrerHost: null
    }],
    sourceBreakdown: [{
      sourceCategory: 'Email',
      storefrontViews: 3,
      productViews: 10,
      addToCartCount: 2,
      checkoutStartCount: 1,
      orderCreatedCount: 1,
      paidOrderCount: 1,
      productViewToCartRate: 0.2,
      checkoutToPaidRate: 1,
      topUtmSource: 'newsletter',
      topReferrerHost: null
    }]
  };
}

function createSchedule() {
  return {
    scheduleId: 'schedule-id',
    isEnabled: true,
    frequency: 'Weekly',
    reportRange: 'Last30Days',
    sendDayOfWeek: 'Monday',
    sendDayOfMonth: null,
    sendTimeLocal: '08:00',
    timeZoneId: 'Africa/Johannesburg',
    nextRunAtUtc: '2026-05-04T06:00:00.000Z',
    lastSentAtUtc: null,
    lastReportPeriodStartUtc: null,
    lastReportPeriodEndUtc: null,
    lastFailureReason: null,
    lastFailedAtUtc: null
  };
}
