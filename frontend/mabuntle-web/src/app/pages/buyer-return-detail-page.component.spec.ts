import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BuyerRefundService } from '../buyer/buyer-refund.service';
import { BuyerReturnRequestResult } from '../buyer/buyer-return.models';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerReturnDetailPageComponent } from './buyer-return-detail-page.component';
import { createSellerPolicySnapshot } from './shop-page.component.spec';

describe('BuyerReturnDetailPageComponent', () => {
  let fixture: ComponentFixture<BuyerReturnDetailPageComponent>;
  let refundService: jasmine.SpyObj<BuyerRefundService>;
  let returnService: jasmine.SpyObj<BuyerReturnService>;

  beforeEach(async () => {
    refundService = jasmine.createSpyObj<BuyerRefundService>('BuyerRefundService', ['listReturnRefunds']);
    returnService = jasmine.createSpyObj<BuyerReturnService>('BuyerReturnService', ['getReturn', 'disputeReturn']);
    refundService.listReturnRefunds.and.resolveTo([createRefund()]);
    returnService.getReturn.and.resolveTo(createReturn());
    returnService.disputeReturn.and.resolveTo(createReturn({ status: 'Disputed', disputeReason: 'Please review.' }));

    await TestBed.configureTestingModule({
      imports: [BuyerReturnDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerRefundService, useValue: refundService },
        { provide: BuyerReturnService, useValue: returnService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ returnRequestId: 'return-id' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerReturnDetailPageComponent);
  });

  it('opens a dispute for a rejected return', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      disputeForm: { patchValue: (value: unknown) => void };
    };
    component.disputeForm.patchValue({ reason: 'Please review.' });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(returnService.disputeReturn).toHaveBeenCalledWith('return-id', { reason: 'Please review.' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Return dispute opened.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Store policy snapshot');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Refund outcome');
  });

  it('renders linked refund context', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(refundService.listReturnRefunds).toHaveBeenCalledWith('return-id');
    expect(text).toContain('Your refund is being processed.');
    expect(text).toContain('View refund history');
  });

  it('links support handoff with order and seller context when dispute is unavailable', async () => {
    returnService.getReturn.and.resolveTo(createReturn({ status: 'Requested', sellerResponseReason: null }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const link = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
      .find(anchor => anchor.textContent?.trim() === 'Contact support') as HTMLAnchorElement | undefined;

    expect(link).toBeTruthy();
    expect(link?.getAttribute('href')).toContain('/account/support');
    expect(link?.getAttribute('href')).toContain('orderId=order-id');
    expect(link?.getAttribute('href')).toContain('sellerId=seller-id');
  });
});

function createReturn(overrides: Partial<BuyerReturnRequestResult> = {}): BuyerReturnRequestResult {
  return {
    returnRequestId: 'return-id',
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    status: 'Rejected',
    reason: 'Damaged',
    details: 'Box torn',
    requestedAtUtc: '2026-05-18T12:00:00Z',
    sellerRespondedAtUtc: '2026-05-18T13:00:00Z',
    sellerResponseReason: 'Rejected by seller.',
    disputedAtUtc: null,
    disputeReason: null,
    items: [{
      returnItemId: 'return-item-id',
      orderItemId: 'order-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      quantity: 1,
      reason: 'Damaged',
      isOpenedOrUnsealed: true,
      note: 'Photo available'
    }],
    messages: [],
    sellerPolicySnapshot: createSellerPolicySnapshot(),
    ...overrides
  };
}

function createRefund() {
  return {
    refundId: 'refund-id',
    orderId: 'order-id',
    returnRequestId: 'return-id',
    amount: 250,
    currency: 'ZAR',
    status: 'Processing',
    statusMessage: 'Your refund is being processed.',
    requestedAtUtc: '2026-05-18T13:30:00Z',
    approvedAtUtc: null,
    refundedAtUtc: null,
    timeline: []
  };
}
