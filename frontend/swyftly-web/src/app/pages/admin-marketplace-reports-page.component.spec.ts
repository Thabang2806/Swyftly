import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminMarketplaceReportResponse } from '../admin/admin-marketplace-report.models';
import { AdminMarketplaceReportService } from '../admin/admin-marketplace-report.service';
import { AdminMarketplaceReportsPageComponent } from './admin-marketplace-reports-page.component';

describe('AdminMarketplaceReportsPageComponent', () => {
  let fixture: ComponentFixture<AdminMarketplaceReportsPageComponent>;
  let reportService: jasmine.SpyObj<AdminMarketplaceReportService>;

  beforeEach(async () => {
    reportService = jasmine.createSpyObj<AdminMarketplaceReportService>('AdminMarketplaceReportService', [
      'getReport',
      'getCsvExportUrl'
    ]);
    reportService.getReport.and.resolveTo(createReport());
    reportService.getCsvExportUrl.and.returnValue('https://localhost:7268/api/admin/reports/marketplace/export.csv');

    await TestBed.configureTestingModule({
      imports: [AdminMarketplaceReportsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminMarketplaceReportService, useValue: reportService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminMarketplaceReportsPageComponent);
  });

  it('loads and displays marketplace report metrics', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('Gross merchandise value');
    expect(compiled.textContent).toContain('Platform commission earned');
    expect(compiled.textContent).toContain('Report Seller');
    expect(compiled.textContent).toContain('Report Dresses');
    expect(compiled.querySelector('a[href*="export.csv"]')).not.toBeNull();
  });

  it('submits date range filters to the report service', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const fromInput = (fixture.nativeElement as HTMLElement).querySelector('input[formControlName="from"]') as HTMLInputElement;
    const toInput = (fixture.nativeElement as HTMLElement).querySelector('input[formControlName="to"]') as HTMLInputElement;
    fromInput.value = '2026-05-01T08:00';
    toInput.value = '2026-05-19T17:00';
    fromInput.dispatchEvent(new Event('input'));
    toInput.dispatchEvent(new Event('input'));

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(reportService.getReport).toHaveBeenCalledWith(jasmine.objectContaining({
      fromUtc: new Date('2026-05-01T08:00').toISOString(),
      toUtc: new Date('2026-05-19T17:00').toISOString()
    }));
  });
});

function createReport(): AdminMarketplaceReportResponse {
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
    topSellers: [{
      sellerId: 'seller-id',
      sellerDisplayName: 'Report Seller',
      orderCount: 1,
      grossMerchandiseValue: 120,
      itemsSold: 2
    }],
    topCategories: [{
      categoryId: 'category-id',
      categoryName: 'Report Dresses',
      quantitySold: 2,
      revenue: 120
    }],
    csvExportUrl: '/api/admin/reports/marketplace/export.csv'
  };
}
