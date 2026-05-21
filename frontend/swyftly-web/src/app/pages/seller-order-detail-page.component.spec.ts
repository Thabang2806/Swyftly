import { ComponentFixture, TestBed } from '@angular/core/testing';
import { convertToParamMap, ActivatedRoute } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerOrderService } from '../seller/seller-order.service';
import { SellerOrderDetailPageComponent } from './seller-order-detail-page.component';
import { createOrder } from './seller-orders-page.component.spec';

describe('SellerOrderDetailPageComponent', () => {
  let fixture: ComponentFixture<SellerOrderDetailPageComponent>;
  let orderService: jasmine.SpyObj<SellerOrderService>;

  beforeEach(async () => {
    orderService = jasmine.createSpyObj<SellerOrderService>(
      'SellerOrderService',
      ['getOrder', 'markProcessing', 'addTracking', 'markReadyToShip', 'markShipped', 'markDelivered', 'markDeliveryFailed', 'markReturnedToSender']);
    orderService.getOrder.and.resolveTo(createOrder());
    orderService.markProcessing.and.resolveTo(createOrder({ status: 'Processing' }));
    orderService.markReadyToShip.and.resolveTo(createOrder({ status: 'ReadyToShip' }));
    orderService.addTracking.and.resolveTo(createOrder({
      shipments: [{
        shipmentId: 'shipment-id',
        status: 'AwaitingFulfilment',
        carrierName: 'Courier',
        trackingNumber: 'TRACK-1',
        trackingUrl: null,
        shippedAtUtc: null,
        deliveredAtUtc: null,
        events: []
      }]
    }));
    orderService.markShipped.and.resolveTo(createOrder({ status: 'Shipped' }));
    orderService.markDelivered.and.resolveTo(createOrder({ status: 'Delivered' }));
    orderService.markDeliveryFailed.and.resolveTo(createOrder({ status: 'Shipped', shipments: [{
      shipmentId: 'shipment-id',
      status: 'DeliveryFailed',
      carrierName: 'Courier',
      trackingNumber: 'TRACK-1',
      trackingUrl: null,
      shippedAtUtc: '2026-05-19T10:00:00Z',
      deliveredAtUtc: null,
      events: []
    }] }));
    orderService.markReturnedToSender.and.resolveTo(createOrder({ status: 'Shipped', shipments: [{
      shipmentId: 'shipment-id',
      status: 'ReturnedToSender',
      carrierName: 'Courier',
      trackingNumber: 'TRACK-1',
      trackingUrl: null,
      shippedAtUtc: '2026-05-19T10:00:00Z',
      deliveredAtUtc: null,
      events: []
    }] }));

    await TestBed.configureTestingModule({
      imports: [SellerOrderDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerOrderService, useValue: orderService },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ orderId: 'order-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerOrderDetailPageComponent);
  });

  it('loads order details and calls fulfilment actions', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');

    const processingButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Mark processing'));
    processingButton?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(orderService.markProcessing).toHaveBeenCalledWith('order-id');
  });

  it('calls mark delivered for shipped in-transit orders', async () => {
    orderService.getOrder.and.resolveTo(createOrder({
      status: 'Shipped',
      shipments: [{
        shipmentId: 'shipment-id',
        status: 'InTransit',
        carrierName: 'Courier',
        trackingNumber: 'TRACK-1',
        trackingUrl: null,
        shippedAtUtc: '2026-05-19T10:00:00Z',
        deliveredAtUtc: null,
        events: []
      }]
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const deliveredButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Mark delivered'));
    expect(deliveredButton?.hasAttribute('disabled')).toBeFalse();
    deliveredButton?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(orderService.markDelivered).toHaveBeenCalledWith('order-id');
  });

  it('records delivery exceptions with reason payloads', async () => {
    orderService.getOrder.and.resolveTo(createOrder({
      status: 'Shipped',
      shipments: [{
        shipmentId: 'shipment-id',
        status: 'InTransit',
        carrierName: 'Courier',
        trackingNumber: 'TRACK-1',
        trackingUrl: null,
        shippedAtUtc: '2026-05-19T10:00:00Z',
        deliveredAtUtc: null,
        events: []
      }]
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const reason = compiled.querySelector('textarea[formControlName="reason"]') as HTMLTextAreaElement;
    reason.value = 'Courier could not reach the recipient.';
    reason.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const failedButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Mark delivery failed'));
    failedButton?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(orderService.markDeliveryFailed).toHaveBeenCalledWith('order-id', {
      reason: 'Courier could not reach the recipient.'
    });
  });
});
