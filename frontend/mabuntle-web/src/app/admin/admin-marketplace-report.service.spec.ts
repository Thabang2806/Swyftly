import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminMarketplaceReportService } from './admin-marketplace-report.service';

describe('AdminMarketplaceReportService', () => {
  let service: AdminMarketplaceReportService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminMarketplaceReportService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads marketplace reports with date range params', async () => {
    const promise = service.getReport({
      fromUtc: '2026-05-01T00:00:00.000Z',
      toUtc: '2026-05-19T00:00:00.000Z'
    });

    const request = httpTestingController.expectOne(req =>
      req.url === `${environment.apiBaseUrl}/api/admin/reports/marketplace`
      && req.params.get('fromUtc') === '2026-05-01T00:00:00.000Z'
      && req.params.get('toUtc') === '2026-05-19T00:00:00.000Z');
    expect(request.request.method).toBe('GET');
    request.flush(createReport());

    const response = await promise;
    expect(response.finance.grossMerchandiseValue).toBe(120);
  });

  it('builds the csv export URL', () => {
    const url = service.getCsvExportUrl({
      fromUtc: '2026-05-01T00:00:00.000Z',
      toUtc: '2026-05-19T00:00:00.000Z'
    });

    expect(url).toContain(`${environment.apiBaseUrl}/api/admin/reports/marketplace/export.csv?`);
    expect(url).toContain('fromUtc=2026-05-01T00:00:00.000Z');
    expect(url).toContain('toUtc=2026-05-19T00:00:00.000Z');
  });

  it('loads buyer growth reports with date range and bucket params', async () => {
    const promise = service.getBuyerGrowthReport({
      fromUtc: '2026-05-01T00:00:00.000Z',
      toUtc: '2026-05-19T00:00:00.000Z',
      bucket: 'Week'
    });

    const request = httpTestingController.expectOne(req =>
      req.url === `${environment.apiBaseUrl}/api/admin/reports/buyer-growth`
      && req.params.get('fromUtc') === '2026-05-01T00:00:00.000Z'
      && req.params.get('toUtc') === '2026-05-19T00:00:00.000Z'
      && req.params.get('bucket') === 'Week');
    expect(request.request.method).toBe('GET');
    request.flush({
      fromUtc: '2026-05-01T00:00:00.000Z',
      toUtc: '2026-05-19T00:00:00.000Z',
      generatedAtUtc: '2026-05-19T10:00:00.000Z',
      bucket: 'Week',
      summary: {
        searchSubmittedCount: 1,
        shopHandoffCount: 0,
        productOpenedCount: 0,
        feedbackSubmittedCount: 0,
        assistantSearchCount: 1,
        visualSearchCount: 0
      },
      confidenceBreakdown: [],
      sourceToolBreakdown: [],
      topCategories: [],
      topColours: [],
      topMaterials: [],
      trend: []
    });

    const response = await promise;
    expect(response.summary.searchSubmittedCount).toBe(1);
    expect(response.bucket).toBe('Week');
  });
});

function createReport() {
  return {
    fromUtc: '2026-05-01T00:00:00.000Z',
    toUtc: '2026-05-19T00:00:00.000Z',
    generatedAtUtc: '2026-05-19T10:00:00.000Z',
    currency: 'ZAR',
    finance: {
      grossMerchandiseValue: 120,
      platformCommissionEarned: 12,
      paymentProcessingFees: 4,
      refunds: 30,
      sellerPendingBalances: 100,
      sellerAvailableBalances: 600,
      sellerHeldBalances: 25,
      payoutsProcessed: 80,
      failedPayouts: 45
    },
    operations: {
      orderCount: 1,
      refundCount: 1,
      payoutsProcessedCount: 1,
      failedPayoutCount: 1,
      disputeCount: 1,
      activeDisputeCount: 1
    },
    topSellers: [],
    topCategories: [],
    csvExportUrl: '/api/admin/reports/marketplace/export.csv'
  };
}
