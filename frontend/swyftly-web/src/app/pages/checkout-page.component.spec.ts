import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { StorefrontAnalyticsService } from '../analytics/storefront-analytics.service';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { BuyerSettingsService } from '../buyer/buyer-settings.service';
import { CartService } from '../cart/cart.service';
import { createCart } from '../cart/cart.service.spec';
import { CheckoutPageComponent } from './checkout-page.component';

describe('CheckoutPageComponent', () => {
  let fixture: ComponentFixture<CheckoutPageComponent>;
  let paymentRedirectService: jasmine.SpyObj<BuyerPaymentRedirectService>;
  let paymentService: jasmine.SpyObj<BuyerPaymentService>;
  let settingsService: jasmine.SpyObj<BuyerSettingsService>;
  let cartService: jasmine.SpyObj<CartService>;
  let storefrontAnalytics: jasmine.SpyObj<StorefrontAnalyticsService>;
  let router: Router;

  beforeEach(async () => {
    cartService = jasmine.createSpyObj<CartService>('CartService', ['getCart', 'createOrderFromCart', 'getShippingOptions']);
    paymentRedirectService = jasmine.createSpyObj<BuyerPaymentRedirectService>('BuyerPaymentRedirectService', ['redirect']);
    paymentService = jasmine.createSpyObj<BuyerPaymentService>('BuyerPaymentService', ['initiatePayment']);
    settingsService = jasmine.createSpyObj<BuyerSettingsService>('BuyerSettingsService', ['listDeliveryAddresses']);
    storefrontAnalytics = jasmine.createSpyObj<StorefrontAnalyticsService>('StorefrontAnalyticsService', ['trackCheckoutStarted']);
    cartService.getCart.and.resolveTo(createCart());
    cartService.getShippingOptions.and.resolveTo(createShippingOptions());
    settingsService.listDeliveryAddresses.and.resolveTo([]);
    cartService.createOrderFromCart.and.resolveTo({
      orderId: 'order-id',
      buyerId: 'buyer-id',
      sellerId: 'seller-id',
      cartId: 'cart-id',
      status: 'PendingPayment',
      items: [],
      itemsSubtotal: 998,
      shippingAmount: 75,
      platformFeeAmount: 0,
      discountAmount: 0,
      totalAmount: 1073,
      statusHistory: [],
      deliveryMethodId: 'delivery-method-id',
      deliveryMethodName: 'Standard courier',
      deliveryMethodType: 'Standard',
      deliveryEstimatedMinDays: 2,
      deliveryEstimatedMaxDays: 5
    });
    paymentService.initiatePayment.and.resolveTo({
      paymentId: 'payment-id',
      orderId: 'order-id',
      provider: 'Fake',
      providerReference: 'fake-reference',
      amount: 1073,
      currency: 'ZAR',
      status: 'Pending',
      checkoutUrl: 'https://checkout.example.test/session'
    });

    await TestBed.configureTestingModule({
      imports: [CheckoutPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerPaymentRedirectService, useValue: paymentRedirectService },
        { provide: BuyerPaymentService, useValue: paymentService },
        { provide: BuyerSettingsService, useValue: settingsService },
        { provide: CartService, useValue: cartService },
        { provide: StorefrontAnalyticsService, useValue: storefrontAnalytics }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CheckoutPageComponent);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  it('loads checkout summary', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.hf-checkout-hero')).not.toBeNull();
    expect(compiled.querySelector('.hf-order-summary')).not.toBeNull();
    expect(compiled.textContent).toContain('Shipping address');
    expect(compiled.textContent).toContain('Delivery');
    expect(compiled.textContent).toContain('Delivery options');
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Payment');
    expect(compiled.textContent).toContain('Review and start checkout');
    expect(compiled.textContent).toContain('Stock is reserved when checkout starts');
    expect(compiled.textContent).toContain('Provider-confirmed payment');
  });

  it('starts checkout, initiates payment, and redirects to checkout url', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    setInput(compiled, 'input[formControlName="fullName"]', 'Buyer One');
    setInput(compiled, 'input[formControlName="phone"]', '+27110000000');
    setInput(compiled, 'input[formControlName="addressLine1"]', '1 Market Street');
    setInput(compiled, 'input[formControlName="city"]', 'Johannesburg');
    setInput(compiled, 'input[formControlName="province"]', 'Gauteng');
    setInput(compiled, 'input[formControlName="postalCode"]', '2000');
    await chooseShippingOption(compiled, fixture);

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(cartService.createOrderFromCart).toHaveBeenCalledWith({
      cartId: 'cart-id',
      reservationMinutes: null,
      deliveryAddressId: null,
      deliveryAddress: {
        recipientName: 'Buyer One',
        phoneNumber: '+27110000000',
        addressLine1: '1 Market Street',
        addressLine2: null,
        suburb: null,
        city: 'Johannesburg',
        province: 'Gauteng',
        postalCode: '2000',
        countryCode: 'ZA',
        deliveryInstructions: null
      },
      deliveryMethodId: 'delivery-method-id',
      pickupPointId: null
    });
    expect(storefrontAnalytics.trackCheckoutStarted).toHaveBeenCalledWith('cart-id', jasmine.any(String));
    expect(paymentService.initiatePayment).toHaveBeenCalledWith('order-id');
    expect(paymentRedirectService.redirect).toHaveBeenCalledWith('https://checkout.example.test/session');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('navigates to success when payment initiation has no checkout url', async () => {
    paymentService.initiatePayment.and.resolveTo({
      paymentId: 'payment-id',
      orderId: 'order-id',
      provider: 'Fake',
      providerReference: 'fake-reference',
      amount: 998,
      currency: 'ZAR',
      status: 'Pending',
      checkoutUrl: null
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    setInput(compiled, 'input[formControlName="fullName"]', 'Buyer One');
    setInput(compiled, 'input[formControlName="phone"]', '+27110000000');
    setInput(compiled, 'input[formControlName="addressLine1"]', '1 Market Street');
    setInput(compiled, 'input[formControlName="city"]', 'Johannesburg');
    setInput(compiled, 'input[formControlName="province"]', 'Gauteng');
    setInput(compiled, 'input[formControlName="postalCode"]', '2000');
    await chooseShippingOption(compiled, fixture);

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(router.navigate).toHaveBeenCalledWith(['/checkout/success'], {
      queryParams: { orderId: 'order-id' }
    });
  });

  it('navigates to failed with order id when payment initiation fails', async () => {
    paymentService.initiatePayment.and.rejectWith({ error: { detail: 'Payment failed.' } });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    setInput(compiled, 'input[formControlName="fullName"]', 'Buyer One');
    setInput(compiled, 'input[formControlName="phone"]', '+27110000000');
    setInput(compiled, 'input[formControlName="addressLine1"]', '1 Market Street');
    setInput(compiled, 'input[formControlName="city"]', 'Johannesburg');
    setInput(compiled, 'input[formControlName="province"]', 'Gauteng');
    setInput(compiled, 'input[formControlName="postalCode"]', '2000');
    await chooseShippingOption(compiled, fixture);

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(router.navigate).toHaveBeenCalledWith(['/checkout/failed'], {
      queryParams: { orderId: 'order-id' }
    });
  });

  it('preselects the default saved delivery address', async () => {
    settingsService.listDeliveryAddresses.and.resolveTo([createAddress()]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Home');

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(cartService.createOrderFromCart).toHaveBeenCalledWith({
      cartId: 'cart-id',
      reservationMinutes: null,
      deliveryAddressId: 'address-id',
      deliveryAddress: null,
      deliveryMethodId: 'delivery-method-id',
      pickupPointId: null
    });
  });

  it('requires pickup point selection for pickup delivery methods', async () => {
    cartService.getShippingOptions.and.resolveTo({
      ...createShippingOptions(),
      options: [{
        ...createShippingOptions().options[0],
        methodType: 'PickupPoint' as const,
        requiresPickupPoint: true,
        pickupPoints: [{
          pickupPointId: 'pickup-id',
          providerName: 'Manual',
          code: 'JHB-ROSEBANK-001',
          name: 'Rosebank Pickup Counter',
          addressLine1: '10 Market Street',
          addressLine2: null,
          suburb: 'Rosebank',
          city: 'Johannesburg',
          province: 'Gauteng',
          postalCode: '2196',
          countryCode: 'ZA',
          latitude: null,
          longitude: null,
          openingHours: 'Mon-Fri 09:00-17:00'
        }]
      }]
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    setInput(compiled, 'input[formControlName="fullName"]', 'Buyer One');
    setInput(compiled, 'input[formControlName="phone"]', '+27110000000');
    setInput(compiled, 'input[formControlName="addressLine1"]', '1 Market Street');
    setInput(compiled, 'input[formControlName="city"]', 'Johannesburg');
    setInput(compiled, 'input[formControlName="province"]', 'Gauteng');
    setInput(compiled, 'input[formControlName="postalCode"]', '2000');
    await chooseShippingOption(compiled, fixture);

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(cartService.createOrderFromCart).not.toHaveBeenCalled();
    expect(compiled.textContent).toContain('Choose a pickup point');
  });
});

async function chooseShippingOption(compiled: HTMLElement, fixture: ComponentFixture<CheckoutPageComponent>): Promise<void> {
  const buttons = Array.from(compiled.querySelectorAll('button')) as HTMLButtonElement[];
  const checkDelivery = buttons.find(button => button.textContent?.includes('Check delivery options'));
  checkDelivery?.click();
  await fixture.whenStable();
  fixture.detectChanges();
}

function createShippingOptions() {
  return {
    cartId: 'cart-id',
    sellerId: 'seller-id',
    cartSubtotal: 998,
    addressVerification: {
      verificationStatus: 'Verified',
      verificationProvider: 'LocalRules',
      verificationWarnings: [],
      verifiedAtUtc: '2026-05-21T10:00:00Z'
    },
    options: [{
      deliveryMethodId: 'delivery-method-id',
      name: 'Standard courier',
      description: 'Door-to-door delivery within South Africa.',
      methodType: 'Standard' as const,
      countryCode: 'ZA',
      province: 'Gauteng',
      basePrice: 75,
      freeShippingThreshold: null,
      shippingAmount: 75,
      freeShippingApplied: false,
      estimatedMinDays: 2,
      estimatedMaxDays: 5,
      displayOrder: 10,
      requiresPickupPoint: false,
      pickupPoints: []
    }]
  };
}

function setInput(compiled: HTMLElement, selector: string, value: string): void {
  const input = compiled.querySelector(selector) as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function createAddress() {
  return {
    deliveryAddressId: 'address-id',
    label: 'Home',
    recipientName: 'Buyer One',
    phoneNumber: '+27110000000',
    addressLine1: '10 Market Street',
    addressLine2: null,
    suburb: 'Rosebank',
    city: 'Johannesburg',
    province: 'Gauteng',
    postalCode: '2196',
    countryCode: 'ZA',
    deliveryInstructions: 'Leave at reception.',
    isDefault: true,
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z'
  };
}
