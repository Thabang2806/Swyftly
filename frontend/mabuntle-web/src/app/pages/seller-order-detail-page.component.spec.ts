import { ComponentFixture, TestBed } from '@angular/core/testing';
import { convertToParamMap, ActivatedRoute } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerInventoryService } from '../seller/seller-inventory.service';
import { SellerOrderService } from '../seller/seller-order.service';
import { SellerOrderDetailPageComponent } from './seller-order-detail-page.component';
import { createOrder } from './seller-orders-page.component.spec';

describe('SellerOrderDetailPageComponent', () => {
  let fixture: ComponentFixture<SellerOrderDetailPageComponent>;
  let inventoryService: jasmine.SpyObj<SellerInventoryService>;
  let orderService: jasmine.SpyObj<SellerOrderService>;

  beforeEach(async () => {
    inventoryService = jasmine.createSpyObj<SellerInventoryService>('SellerInventoryService', ['listHistory']);
    inventoryService.listHistory.and.resolveTo([]);
    orderService = jasmine.createSpyObj<SellerOrderService>(
      'SellerOrderService',
      ['getOrder', 'markProcessing', 'addTracking', 'markReadyToShip', 'markShipped', 'markDelivered', 'markDeliveryFailed', 'markReturnedToSender', 'bookCarrier', 'syncCarrierTracking']);
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
    orderService.bookCarrier.and.resolveTo(createOrder({ status: 'ReadyToShip', shipments: [{
      shipmentId: 'shipment-id',
      status: 'ReadyForCourier',
      carrierName: 'Fake Courier',
      trackingNumber: 'FAKE-ORDER',
      trackingUrl: 'http://localhost:4200/fake-tracking/FAKE-ORDER',
      shippedAtUtc: null,
      deliveredAtUtc: null,
      carrierProviderName: 'Fake',
      carrierServiceCode: 'STANDARD',
      providerShipmentReference: 'fake-shp-1',
      carrierBookingStatus: 'Booked',
      providerStatus: 'Booked',
      providerLabelUrl: 'http://localhost:4200/fake-label/fake-shp-1',
      providerLastSyncedAtUtc: '2026-05-21T10:00:00Z',
      providerError: null,
      events: []
    }] }));
    orderService.syncCarrierTracking.and.resolveTo(createOrder({ status: 'Shipped' }));

    await TestBed.configureTestingModule({
      imports: [SellerOrderDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerInventoryService, useValue: inventoryService },
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
    expect(inventoryService.listHistory).toHaveBeenCalledWith({ orderId: 'order-id' });
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

  it('books carrier with package payload for ready-to-ship orders', async () => {
    orderService.getOrder.and.resolveTo(createOrder({
      status: 'ReadyToShip',
      shipments: [{
        shipmentId: 'shipment-id',
        status: 'ReadyForCourier',
        carrierName: null,
        trackingNumber: null,
        trackingUrl: null,
        shippedAtUtc: null,
        deliveredAtUtc: null,
        events: []
      }]
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const button = Array.from(compiled.querySelectorAll('button'))
      .find(candidate => candidate.textContent?.includes('Book carrier'));
    expect(button?.hasAttribute('disabled')).toBeFalse();
    button?.closest('form')?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(orderService.bookCarrier).toHaveBeenCalledWith('order-id', jasmine.objectContaining({
      packageWeightKg: 1,
      packageLengthCm: 30,
      packageWidthCm: 20,
      packageHeightCm: 10,
      serviceCode: 'STANDARD',
      collectionNote: null
    }));
  });
});
