import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerDashboardService } from './seller-dashboard.service';

describe('SellerDashboardService', () => {
  let service: SellerDashboardService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerDashboardService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads the seller dashboard summary', async () => {
    const promise = service.getSummary();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/dashboard/summary`);
    expect(request.request.method).toBe('GET');
    request.flush({
      sellerId: 'seller-id',
      generatedAtUtc: '2026-05-26T14:30:00Z',
      fromUtc: '2026-04-26T14:30:00Z',
      salesLast30Days: 12450,
      ordersLast30Days: 18,
      paidOrderCount: 2,
      processingOrderCount: 1,
      readyToShipOrderCount: 1,
      shippedOrderCount: 3,
      pendingFulfilmentOrders: 4,
      deliveryExceptionOrderCount: 1,
      draftProductCount: 3,
      pendingReviewProductCount: 2,
      publishedProductCount: 8,
      changesRequestedProductCount: 1,
      pendingListingRevisionCount: 1,
      pendingVariantRevisionCount: 1,
      lowStockProductCount: 3,
      outOfStockVariantCount: 2,
      reservedStockCount: 5,
      openReturnCount: 1,
      returnsAwaitingSellerResponseCount: 1,
      openSupportTicketCount: 2,
      activeDisputeCount: 1,
      pendingPayoutAmount: 3400,
      availablePayoutAmount: 1200,
      heldPayoutAmount: 0,
      pendingPayoutCount: 1,
      processingPayoutCount: 0,
      hasPendingPayoutProfileChange: true,
      activeAdCampaignCount: 2,
      pendingAdReviewCount: 1,
      adSpendLast30Days: 480,
      adRevenueLast30Days: 1600,
      unreadNotificationCount: 5,
      alerts: [],
      recentActivity: []
    });

    const response = await promise;
    expect(response.salesLast30Days).toBe(12450);
    expect(response.pendingFulfilmentOrders).toBe(4);
  });
});
