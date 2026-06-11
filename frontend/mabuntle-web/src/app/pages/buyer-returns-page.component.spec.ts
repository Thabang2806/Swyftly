import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerReturnRequestResult } from '../buyer/buyer-return.models';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerReturnsPageComponent } from './buyer-returns-page.component';

describe('BuyerReturnsPageComponent', () => {
  let fixture: ComponentFixture<BuyerReturnsPageComponent>;
  let returnService: jasmine.SpyObj<BuyerReturnService>;

  beforeEach(async () => {
    returnService = jasmine.createSpyObj<BuyerReturnService>('BuyerReturnService', ['listReturns']);
    returnService.listReturns.and.resolveTo([createReturn()]);

    await TestBed.configureTestingModule({
      imports: [BuyerReturnsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerReturnService, useValue: returnService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerReturnsPageComponent);
  });

  it('renders populated return rows', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const openLink = (fixture.nativeElement as HTMLElement).querySelector('a[href="/account/returns/return-id"]');

    expect(text).toContain('Size did not work');
    expect(text).toContain('2 items');
    expect(text).toContain('Requested');
    expect(openLink).not.toBeNull();
  });

  it('renders the empty state', async () => {
    returnService.listReturns.and.resolveTo([]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('No return requests');
    expect(text).toContain('Return requests can be started from delivered order details.');
    expect(text).toContain('View orders');
  });

  it('renders an error state', async () => {
    returnService.listReturns.and.rejectWith(new Error('Unable to load returns.'));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Something went wrong. Please try again.');
  });

  it('shows the loading state before returns resolve', () => {
    returnService.listReturns.and.returnValue(new Promise(() => undefined));

    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading returns...');
  });
});

function createReturn(overrides: Partial<BuyerReturnRequestResult> = {}): BuyerReturnRequestResult {
  return {
    returnRequestId: 'return-id',
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    status: 'Requested',
    reason: 'Size did not work',
    details: 'Need a different size.',
    requestedAtUtc: '2026-05-18T12:00:00Z',
    sellerRespondedAtUtc: null,
    sellerResponseReason: null,
    disputedAtUtc: null,
    disputeReason: null,
    items: [{
      returnItemId: 'return-item-id',
      orderItemId: 'order-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      quantity: 2,
      reason: 'Size did not work',
      isOpenedOrUnsealed: false,
      note: null
    }],
    messages: [],
    sellerPolicySnapshot: null,
    ...overrides
  };
}
