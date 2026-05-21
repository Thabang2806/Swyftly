import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminOrderDetailResponse } from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AdminOrderDetailPageComponent } from './admin-order-detail-page.component';
import { createAdminOrder } from './admin-orders-page.component.spec';
import { createAdminPayment } from './admin-payments-page.component.spec';

describe('AdminOrderDetailPageComponent', () => {
  let fixture: ComponentFixture<AdminOrderDetailPageComponent>;
  let service: jasmine.SpyObj<AdminOrderPaymentService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<AdminOrderPaymentService>('AdminOrderPaymentService', ['getOrder']);
    service.getOrder.and.resolveTo(createAdminOrderDetail());

    await TestBed.configureTestingModule({
      imports: [AdminOrderDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminOrderPaymentService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ orderId: 'order-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminOrderDetailPageComponent);
  });

  it('loads an order detail with items, status history, shipments, and payments', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(service.getOrder).toHaveBeenCalledWith('order-id');
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('PaymentCaptured');
    expect(compiled.textContent).toContain('ShipmentInTransit');
    expect(compiled.textContent).toContain('Leave at reception.');
    expect(compiled.querySelector('a[href="/admin/payments/payment-id"]')).not.toBeNull();
  });
});

export function createAdminOrderDetail(overrides: Partial<AdminOrderDetailResponse> = {}): AdminOrderDetailResponse {
  return {
    ...createAdminOrder(),
    cartId: 'cart-id',
    deliveryAddress: {
      recipientName: 'Buyer One',
      phoneNumber: '+27110000000',
      addressLine1: '10 Market Street',
      addressLine2: null,
      suburb: 'Rosebank',
      city: 'Johannesburg',
      province: 'Gauteng',
      postalCode: '2196',
      countryCode: 'ZA',
      deliveryInstructions: 'Leave at reception.'
    },
    items: [{
      orderItemId: 'item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      productTitle: 'Summer Dress',
      sku: 'SKU-1',
      size: 'M',
      colour: 'Red',
      unitPrice: 120,
      quantity: 1,
      lineTotal: 120
    }],
    statusHistory: [{
      statusHistoryId: 'history-id',
      previousStatus: 'PendingPayment',
      newStatus: 'Paid',
      changedAtUtc: '2026-05-19T10:05:00Z',
      reason: 'PaymentCaptured'
    }],
    shipments: [{
      shipmentId: 'shipment-id',
      status: 'InTransit',
      carrierName: 'Courier',
      trackingNumber: 'TRACK-1',
      trackingUrl: null,
      shippedAtUtc: '2026-05-19T11:00:00Z',
      deliveredAtUtc: null,
      events: [{
        shipmentEventId: 'shipment-event-id',
        status: 'InTransit',
        eventType: 'ShipmentInTransit',
        message: 'Shipment was marked as shipped.',
        carrierName: 'Courier',
        trackingNumber: 'TRACK-1',
        occurredAtUtc: '2026-05-19T11:00:00Z'
      }]
    }],
    payments: [createAdminPayment()],
    ...overrides
  };
}
