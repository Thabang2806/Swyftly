import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { environment } from '../../environments/environment';
import { BuyerPaymentService } from './buyer-payment.service';

describe('BuyerPaymentService', () => {
  let service: BuyerPaymentService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BuyerPaymentService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('initiates payment for an order', async () => {
    const promise = service.initiatePayment('order-id');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/payments/initiate`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ orderId: 'order-id' });
    request.flush({
      paymentId: 'payment-id',
      orderId: 'order-id',
      provider: 'Fake',
      providerReference: 'fake-reference',
      amount: 500,
      currency: 'ZAR',
      status: 'Pending',
      checkoutUrl: 'https://checkout.example.test/session'
    });

    const response = await promise;
    expect(response.paymentId).toBe('payment-id');
    expect(response.checkoutUrl).toBe('https://checkout.example.test/session');
  });
});

