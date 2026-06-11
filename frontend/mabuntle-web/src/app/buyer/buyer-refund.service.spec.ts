import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { BuyerRefundService } from './buyer-refund.service';

describe('BuyerRefundService', () => {
  let service: BuyerRefundService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BuyerRefundService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls buyer refund read endpoints', async () => {
    const refund = {
      refundId: 'refund-id',
      orderId: 'order-id',
      returnRequestId: 'return-id',
      amount: 499,
      currency: 'ZAR',
      status: 'Processing',
      statusMessage: 'Your refund is being processed.',
      requestedAtUtc: '2026-05-29T10:00:00Z',
      approvedAtUtc: null,
      refundedAtUtc: null,
      timeline: []
    };

    const listPromise = service.listRefunds();
    httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/refunds`).flush([refund]);
    await expectAsync(listPromise).toBeResolvedTo([refund]);

    const detailPromise = service.getRefund('refund-id');
    httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/refunds/refund-id`).flush(refund);
    await expectAsync(detailPromise).toBeResolvedTo(refund);

    const orderPromise = service.listOrderRefunds('order-id');
    httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/orders/order-id/refunds`).flush([refund]);
    await expectAsync(orderPromise).toBeResolvedTo([refund]);

    const returnPromise = service.listReturnRefunds('return-id');
    httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/returns/return-id/refunds`).flush([refund]);
    await expectAsync(returnPromise).toBeResolvedTo([refund]);
  });
});
