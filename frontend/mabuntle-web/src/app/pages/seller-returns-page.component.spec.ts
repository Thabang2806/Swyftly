import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerReturnRequestResult } from '../seller/seller-return.models';
import { SellerReturnService } from '../seller/seller-return.service';
import { SellerReturnsPageComponent } from './seller-returns-page.component';
import { createSellerPolicySnapshot } from './shop-page.component.spec';

describe('SellerReturnsPageComponent', () => {
  let fixture: ComponentFixture<SellerReturnsPageComponent>;
  let returnService: jasmine.SpyObj<SellerReturnService>;

  beforeEach(async () => {
    returnService = jasmine.createSpyObj<SellerReturnService>('SellerReturnService', ['listReturns']);
    returnService.listReturns.and.resolveTo([createReturnRequest()]);

    await TestBed.configureTestingModule({
      imports: [SellerReturnsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerReturnService, useValue: returnService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerReturnsPageComponent);
  });

  it('loads seller returns', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Changed mind');
    expect(compiled.textContent).toContain('AwaitingSellerResponse');
    expect(compiled.querySelector('a[href="/returns/return-id"]')).not.toBeNull();
  });
});

export function createReturnRequest(
  overrides: Partial<SellerReturnRequestResult> = {}
): SellerReturnRequestResult {
  return {
    returnRequestId: 'return-id',
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    status: 'AwaitingSellerResponse',
    reason: 'Changed mind',
    details: 'Buyer requested a return.',
    requestedAtUtc: '2026-05-19T10:00:00Z',
    sellerRespondedAtUtc: null,
    sellerResponseReason: null,
    disputedAtUtc: null,
    disputeReason: null,
    items: [{
      returnItemId: 'return-item-id',
      orderItemId: 'order-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      quantity: 1,
      reason: 'Changed mind',
      isOpenedOrUnsealed: false,
      note: null
    }],
    messages: [],
    sellerPolicySnapshot: createSellerPolicySnapshot(),
    ...overrides
  };
}
