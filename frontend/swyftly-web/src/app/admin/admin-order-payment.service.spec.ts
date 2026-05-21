import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminOrderPaymentService } from './admin-order-payment.service';

describe('AdminOrderPaymentService', () => {
  let service: AdminOrderPaymentService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminOrderPaymentService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads admin orders with status filtering', async () => {
    const promise = service.getOrders('Paid');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/orders?status=Paid`);
    expect(request.request.method).toBe('GET');
    request.flush([{ orderId: 'order-id', status: 'Paid' }]);

    const response = await promise;
    expect(response[0].orderId).toBe('order-id');
  });

  it('loads an admin order detail', async () => {
    const promise = service.getOrder('order-id');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/orders/order-id`);
    expect(request.request.method).toBe('GET');
    request.flush({ orderId: 'order-id', payments: [] });

    const response = await promise;
    expect(response.orderId).toBe('order-id');
  });

  it('loads admin payments with status and order filters', async () => {
    const promise = service.getPayments('Paid', 'order-id');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/payments?status=Paid&orderId=order-id`);
    expect(request.request.method).toBe('GET');
    request.flush([{ paymentId: 'payment-id', status: 'Paid' }]);

    const response = await promise;
    expect(response[0].paymentId).toBe('payment-id');
  });

  it('loads an admin payment detail', async () => {
    const promise = service.getPayment('payment-id');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/payments/payment-id`);
    expect(request.request.method).toBe('GET');
    request.flush({ paymentId: 'payment-id', events: [] });

    const response = await promise;
    expect(response.paymentId).toBe('payment-id');
  });

  it('loads admin payment reconciliation candidates', async () => {
    const promise = service.getPaymentReconciliationCandidates(45);

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/payments/reconciliation-candidates?olderThanMinutes=45&includeSnoozed=false`);
    expect(request.request.method).toBe('GET');
    request.flush([{ paymentId: 'payment-id', reasonCode: 'StalePendingPayment' }]);

    const response = await promise;
    expect(response[0].reasonCode).toBe('StalePendingPayment');
  });

  it('records a payment reconciliation review', async () => {
    const requestBody = {
      observedProviderStatus: 'COMPLETE',
      observedAmount: 140,
      observedCurrency: 'ZAR',
      outcome: 'ProviderPaidMissingWebhook' as const,
      reason: 'Provider dashboard shows complete.',
      nextReviewAfterUtc: null
    };
    const promise = service.createPaymentReconciliationReview('payment-id', requestBody);

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/payments/payment-id/reconciliation-reviews`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(requestBody);
    request.flush({ reviewId: 'review-id', paymentId: 'payment-id', outcome: 'ProviderPaidMissingWebhook' });

    const response = await promise;
    expect(response.reviewId).toBe('review-id');
  });
});
