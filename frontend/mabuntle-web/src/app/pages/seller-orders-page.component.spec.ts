import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerOrderResult } from '../seller/seller-order.models';
import { SellerOrderService } from '../seller/seller-order.service';
import { SellerOrdersPageComponent } from './seller-orders-page.component';

describe('SellerOrdersPageComponent', () => {
  let fixture: ComponentFixture<SellerOrdersPageComponent>;
  let orderService: jasmine.SpyObj<SellerOrderService>;

  beforeEach(async () => {
    orderService = jasmine.createSpyObj<SellerOrderService>('SellerOrderService', ['listOrders']);
    orderService.listOrders.and.resolveTo([createOrder()]);

    await TestBed.configureTestingModule({
      imports: [SellerOrdersPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([
          { path: 'orders/:orderId', component: StubRouteComponent },
          { path: 'payouts', component: StubRouteComponent }
        ]),
        { provide: SellerOrderService, useValue: orderService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerOrdersPageComponent);
  });

  it('loads seller orders with workspace navigation', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Orders');
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Paid');
    expect(compiled.querySelector('a[href="/payouts"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/orders/order-id"]')).not.toBeNull();
  });
});

export function createOrder(overrides: Partial<SellerOrderResult> = {}): SellerOrderResult {
  return {
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    cartId: 'cart-id',
    status: 'Paid',
    items: [{
      orderItemId: 'item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      productTitle: 'Summer Dress',
      sku: 'SKU-1',
      size: 'M',
      colour: 'Red',
      unitPrice: 120,
      quantity: 2,
      lineTotal: 240
    }],
    itemsSubtotal: 240,
    shippingAmount: 0,
    platformFeeAmount: 0,
    discountAmount: 0,
    totalAmount: 240,
    statusHistory: [{
      statusHistoryId: 'history-id',
      previousStatus: null,
      newStatus: 'Paid',
      changedAtUtc: '2026-05-19T10:00:00Z',
      reason: null
    }],
    shipments: [],
    ...overrides
  };
}

@Component({ template: '' })
class StubRouteComponent {}
