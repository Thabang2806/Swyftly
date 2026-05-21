import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { CheckoutFailedPageComponent } from './checkout-failed-page.component';
import { createOrder } from './checkout-success-page.component.spec';

describe('CheckoutFailedPageComponent', () => {
  let fixture: ComponentFixture<CheckoutFailedPageComponent>;
  let orderService: jasmine.SpyObj<BuyerOrderService>;
  let paymentRedirectService: jasmine.SpyObj<BuyerPaymentRedirectService>;
  let paymentService: jasmine.SpyObj<BuyerPaymentService>;

  beforeEach(async () => {
    orderService = jasmine.createSpyObj<BuyerOrderService>('BuyerOrderService', ['getOrder']);
    paymentRedirectService = jasmine.createSpyObj<BuyerPaymentRedirectService>('BuyerPaymentRedirectService', ['redirect']);
    paymentService = jasmine.createSpyObj<BuyerPaymentService>('BuyerPaymentService', ['initiatePayment']);
    orderService.getOrder.and.resolveTo(createOrder());
    paymentService.initiatePayment.and.resolveTo({
      paymentId: 'payment-id',
      orderId: 'order-id',
      provider: 'Fake',
      providerReference: 'fake-reference',
      amount: 500,
      currency: 'ZAR',
      status: 'Pending',
      checkoutUrl: 'https://checkout.example.test/session'
    });

    await TestBed.configureTestingModule({
      imports: [CheckoutFailedPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerOrderService, useValue: orderService },
        { provide: BuyerPaymentRedirectService, useValue: paymentRedirectService },
        { provide: BuyerPaymentService, useValue: paymentService },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ orderId: 'order-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CheckoutFailedPageComponent);
  });

  it('shows retry for pending-payment orders', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.hf-checkout-result-card--warning')).not.toBeNull();
    expect(compiled.textContent).toContain('Retry payment');

    const retryButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Retry payment'));
    retryButton?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(paymentService.initiatePayment).toHaveBeenCalledWith('order-id');
    expect(paymentRedirectService.redirect).toHaveBeenCalledWith('https://checkout.example.test/session');
  });

  it('shows start-checkout-again copy for cancelled orders', async () => {
    orderService.getOrder.and.resolveTo(createOrder({ status: 'Cancelled' }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Start checkout again');
    expect(text).not.toContain('Retry payment');
  });
});
