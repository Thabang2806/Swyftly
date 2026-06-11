import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminDashboardService } from './admin-dashboard.service';

describe('AdminDashboardService', () => {
  let service: AdminDashboardService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminDashboardService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads the admin dashboard summary', async () => {
    const promise = service.getSummary();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/dashboard/summary`);
    expect(request.request.method).toBe('GET');
    request.flush({
      pendingSellerApprovals: 2,
      pendingProductReviews: 3,
      newOrdersToday: 4,
      openDisputes: 1,
      pendingRefunds: 5,
      pendingPayouts: 6,
      totalGrossSalesPlaceholder: 0,
      platformCommissionPlaceholder: 0
    });

    const response = await promise;
    expect(response.pendingSellerApprovals).toBe(2);
    expect(response.pendingPayouts).toBe(6);
  });
});
