import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerOrdersPageComponent } from './buyer-orders-page.component';

describe('BuyerOrdersPageComponent', () => {
  let fixture: ComponentFixture<BuyerOrdersPageComponent>;
  let orderService: jasmine.SpyObj<BuyerOrderService>;

  beforeEach(async () => {
    orderService = jasmine.createSpyObj<BuyerOrderService>('BuyerOrderService', ['listOrders']);
    orderService.listOrders.and.resolveTo([
      createOrder(),
      createOrder({ orderId: 'second-order-id', status: 'Paid', items: [{ ...createOrder().items[0], productTitle: 'Canvas Sneakers' }] })
    ]);

    await TestBed.configureTestingModule({
      imports: [BuyerOrdersPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerOrderService, useValue: orderService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerOrdersPageComponent);
  });

  it('filters buyer orders by status and search text', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const searchInput = compiled.querySelector('input[formControlName="search"]') as HTMLInputElement;
    const statusInput = compiled.querySelector('input[formControlName="status"]') as HTMLInputElement;
    searchInput.value = 'Sneakers';
    searchInput.dispatchEvent(new Event('input'));
    statusInput.value = 'Paid';
    statusInput.dispatchEvent(new Event('input'));
    compiled.querySelector('form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Canvas Sneakers');
    expect(compiled.textContent).not.toContain('Summer Dress');
    const reviewLink = Array.from(compiled.querySelectorAll('a'))
      .find(link => link.getAttribute('href') === '/account/orders/second-order-id');
    expect(reviewLink).toBeTruthy();
  });
});

function createOrder(overrides: Partial<BuyerOrderResult> = {}): BuyerOrderResult {
  return {
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    cartId: 'cart-id',
    status: 'Delivered',
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
    statusHistory: [{ statusHistoryId: 'history-id', previousStatus: null, newStatus: 'Delivered', changedAtUtc: '2026-05-18T12:00:00Z', reason: null }],
    shipments: [],
    ...overrides
  };
}
