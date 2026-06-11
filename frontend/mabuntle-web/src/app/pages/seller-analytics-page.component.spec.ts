import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerAnalyticsSummaryResponse } from '../seller/seller-analytics.models';
import { SellerAnalyticsService } from '../seller/seller-analytics.service';
import { SellerAnalyticsPageComponent } from './seller-analytics-page.component';

describe('SellerAnalyticsPageComponent', () => {
  let fixture: ComponentFixture<SellerAnalyticsPageComponent>;
  let analyticsService: jasmine.SpyObj<SellerAnalyticsService>;

  beforeEach(async () => {
    analyticsService = jasmine.createSpyObj<SellerAnalyticsService>('SellerAnalyticsService', [
      'getSummary',
      'getPerformance',
      'getCsvExportUrl',
      'getReportSchedule',
      'updateReportSchedule',
      'sendTestReportDigest'
    ]);
    analyticsService.getSummary.and.resolveTo(createSummary());
    analyticsService.getPerformance.and.resolveTo(createPerformance());
    analyticsService.getCsvExportUrl.and.callFake(report => `/api/seller/analytics/export.csv?report=${report}`);
    analyticsService.getReportSchedule.and.resolveTo(createSchedule());
    analyticsService.updateReportSchedule.and.resolveTo(createSchedule());
    analyticsService.sendTestReportDigest.and.resolveTo({
      isSuccess: true,
      notificationId: 'notification-id',
      failureReason: null
    });

    await TestBed.configureTestingModule({
      imports: [SellerAnalyticsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerAnalyticsService, useValue: analyticsService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerAnalyticsPageComponent);
  });

  it('loads seller analytics cards and tables', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Total sales');
    expect(compiled.textContent).toContain('Product performance');
    expect(compiled.textContent).toContain('Conversion funnel');
    expect(compiled.textContent).toContain('Source breakdown');
    expect(compiled.textContent).toContain('Email');
    expect(compiled.textContent).toContain('Product funnel');
    expect(compiled.textContent).toContain('Sales trend');
    expect(compiled.textContent).toContain('Seller One Product');
    expect(compiled.textContent).toContain('Customer care');
    expect(compiled.textContent).toContain('AI usage');
    expect(compiled.textContent).toContain('Scheduled reports');
    expect(compiled.querySelector('a[href*="report=Products"]')).not.toBeNull();
    expect(compiled.querySelector('a[href*="report=Funnel"]')).not.toBeNull();
  });

  it('reloads performance data when filters are applied', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(analyticsService.getPerformance).toHaveBeenCalled();
  });

  it('saves scheduled report settings and sends a test digest', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const scheduleForm = compiled.querySelector('.seller-report-schedule-form') as HTMLFormElement;
    scheduleForm.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(analyticsService.updateReportSchedule).toHaveBeenCalled();

    const testButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Send test digest')) as HTMLButtonElement;
    testButton.click();
    await fixture.whenStable();

    expect(analyticsService.sendTestReportDigest).toHaveBeenCalled();
  });
});

function createSummary(): SellerAnalyticsSummaryResponse {
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
    topProducts: [{
      productId: 'product-id',
      productTitle: 'Seller One Product',
      quantitySold: 2,
      revenue: 998
    }],
    lowStockProducts: [{
      productId: 'product-id',
      title: 'Seller One Product',
      status: 'Published',
      availableQuantity: 3,
      lowStockVariantCount: 1
    }],
    adPerformance: {
      campaignCount: 1,
      impressions: 100,
      clicks: 5,
      clickThroughRate: 0.05,
      spend: 25,
      ordersGenerated: 1,
      revenueGenerated: 499,
      topCampaigns: [{
        adCampaignId: 'campaign-id',
        name: 'Launch campaign',
        status: 'Active',
        impressions: 100,
        clicks: 5,
        clickThroughRate: 0.05,
        spend: 25,
        ordersGenerated: 1,
        revenueGenerated: 499,
        returnOnAdSpend: 19.96
      }]
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
    bucket: 'Day' as const,
    salesTrend: [{
      periodStartUtc: '2026-05-01T00:00:00.000Z',
      periodEndUtc: '2026-05-02T00:00:00.000Z',
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
    inventoryPerformance: [{
      productId: 'product-id',
      productTitle: 'Seller One Product',
      productVariantId: 'variant-id',
      sku: 'SKU-1',
      barcode: 'BARCODE-1',
      size: 'M',
      colour: 'Black',
      status: 'Active',
      stockQuantity: 3,
      reservedQuantity: 0,
      availableQuantity: 3,
      isLowStock: true,
      isOutOfStock: false,
      lastMovementAtUtc: '2026-05-01T00:00:00.000Z'
    }],
    adPerformance: [{
      adCampaignId: 'campaign-id',
      name: 'Launch campaign',
      status: 'Active',
      impressions: 100,
      clicks: 5,
      clickThroughRate: 0.05,
      spend: 25,
      ordersGenerated: 1,
      revenueGenerated: 499,
      returnOnAdSpend: 19.96
    }],
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
      periodEndUtc: '2026-05-02T00:00:00.000Z',
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
      dominantSourceCategory: 'Email' as const,
      topUtmSource: 'newsletter',
      topReferrerHost: null
    }],
    sourceBreakdown: [{
      sourceCategory: 'Email' as const,
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
    frequency: 'Weekly' as const,
    reportRange: 'Last30Days' as const,
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
