import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { CheckoutSuccessPageComponent } from './checkout-success-page.component';

describe('CheckoutSuccessPageComponent', () => {
  let fixture: ComponentFixture<CheckoutSuccessPageComponent>;
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
      imports: [CheckoutSuccessPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerOrderService, useValue: orderService },
        { provide: BuyerPaymentRedirectService, useValue: paymentRedirectService },
        { provide: BuyerPaymentService, useValue: paymentService },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ orderId: 'order-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CheckoutSuccessPageComponent);
  });

  it('loads order context and retries pending payment', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.hf-checkout-result-card')).not.toBeNull();
    expect(compiled.textContent).toContain('PendingPayment');
    expect(compiled.textContent).toContain('Order reference');
    expect(compiled.textContent).toContain('Total');
    expect(compiled.textContent).toContain('Retry payment');

    const retryButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Retry payment'));
    retryButton?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(orderService.getOrder).toHaveBeenCalledWith('order-id');
    expect(paymentService.initiatePayment).toHaveBeenCalledWith('order-id');
    expect(paymentRedirectService.redirect).toHaveBeenCalledWith('https://checkout.example.test/session');
  });
});

export function createOrder(overrides: Partial<BuyerOrderResult> = {}): BuyerOrderResult {
  return {
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    cartId: 'cart-id',
    status: 'PendingPayment',
    items: [{
      orderItemId: 'order-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      productTitle: 'Summer Dress',
      sku: 'SKU-1',
      size: 'M',
      colour: 'Black',
      unitPrice: 500,
      quantity: 1,
      lineTotal: 500
    }],
    itemsSubtotal: 500,
    shippingAmount: 0,
    platformFeeAmount: 0,
    discountAmount: 0,
    totalAmount: 500,
    statusHistory: [],
    shipments: [],
    ...overrides
  };
}
