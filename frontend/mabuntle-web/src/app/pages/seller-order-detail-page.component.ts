import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerInventoryMovementResponse, SellerInventoryMovementType } from '../seller/seller-inventory.models';
import { SellerInventoryService } from '../seller/seller-inventory.service';
import { SellerOrderResult } from '../seller/seller-order.models';
import { SellerOrderService } from '../seller/seller-order.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-order-detail-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page">
      <app-seller-workspace-nav />

      <a class="admin-back-link" routerLink="/orders">Back to orders</a>

      <app-page-header
        eyebrow="Seller operations"
        [heading]="order() ? 'Order ' + order()!.orderId : 'Order'"
        description="Manage manual fulfilment actions for this seller order."
      >
        <div pageHeaderActions>
          @if (order()) {
            <app-status-badge [label]="order()!.status" [tone]="statusTone(order()!.status)" />
          }
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading order...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (order()) {
          <div class="seller-detail-grid">
            <section class="seller-panel">
              <h2>Order summary</h2>
              <dl class="seller-facts">
                <div><dt>Items subtotal</dt><dd>{{ order()!.itemsSubtotal | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Shipping</dt><dd>{{ order()!.shippingAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                @if (order()!.deliveryMethodName) {
                  <div><dt>Delivery method</dt><dd>{{ order()!.deliveryMethodName }} - {{ deliveryEstimate(order()!) }}</dd></div>
                }
                @if (order()!.pickupPoint) {
                  <div><dt>Pickup point</dt><dd>{{ order()!.pickupPoint!.name }}</dd></div>
                }
                <div><dt>Platform fee</dt><dd>{{ order()!.platformFeeAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Discount</dt><dd>{{ order()!.discountAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Total</dt><dd>{{ order()!.totalAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
              </dl>
            </section>

            <section class="seller-panel">
              <h2>Delivery address</h2>
              @if (order()!.deliveryAddress) {
                <dl class="seller-facts">
                  <div><dt>Recipient</dt><dd>{{ order()!.deliveryAddress!.recipientName }}</dd></div>
                  <div><dt>Phone</dt><dd>{{ order()!.deliveryAddress!.phoneNumber }}</dd></div>
                  <div><dt>Address</dt><dd>{{ renderDeliveryAddress(order()!.deliveryAddress!) }}</dd></div>
                  @if (order()!.deliveryAddress!.deliveryInstructions) {
                    <div><dt>Instructions</dt><dd>{{ order()!.deliveryAddress!.deliveryInstructions }}</dd></div>
                  }
                  @if (order()!.deliveryAddress!.verificationStatus) {
                    <div><dt>Address check</dt><dd>{{ order()!.deliveryAddress!.verificationStatus }}</dd></div>
                  }
                </dl>
              } @else {
                <app-ui-alert tone="info">This older order does not have a delivery-address snapshot.</app-ui-alert>
              }
            </section>

            @if (order()!.pickupPoint) {
              <section class="seller-panel">
                <h2>Pickup point</h2>
                <dl class="seller-facts">
                  <div><dt>Name</dt><dd>{{ order()!.pickupPoint!.name }}</dd></div>
                  <div><dt>Code</dt><dd>{{ order()!.pickupPoint!.code }}</dd></div>
                  <div><dt>Address</dt><dd>{{ order()!.pickupPoint!.addressLine1 }}, {{ order()!.pickupPoint!.city }}, {{ order()!.pickupPoint!.province }}</dd></div>
                  @if (order()!.pickupPoint!.openingHours) {
                    <div><dt>Opening hours</dt><dd>{{ order()!.pickupPoint!.openingHours }}</dd></div>
                  }
                </dl>
              </section>
            }

            <section class="seller-panel">
              <h2>Fulfilment actions</h2>
              <p>Use these controls for manual marketplace fulfilment. Carrier booking is available when the API is configured with a carrier provider.</p>
              <div class="seller-action-row">
                <button data-ui-button="primary" type="button" [disabled]="isActing()" (click)="markProcessing()">Mark processing</button>
                <button data-ui-button="secondary" type="button" [disabled]="isActing() || !canMarkReadyToShip()" (click)="markReadyToShip()">Ready to ship</button>
                <button data-ui-button="secondary" type="button" [disabled]="isActing()" (click)="markShipped()">Mark shipped</button>
                <button data-ui-button="secondary" type="button" [disabled]="isActing() || !canMarkDelivered()" (click)="markDelivered()">Mark delivered</button>
              </div>

              <form [formGroup]="trackingForm" (ngSubmit)="addTracking()" class="seller-form-grid" novalidate>
                <label class="ui-field">
                  <span>Carrier name</span>
                  <input formControlName="carrierName" />
                </label>

                <label class="ui-field">
                  <span>Tracking number</span>
                  <input formControlName="trackingNumber" />
                </label>

                <label class="ui-field">
                  <span>Tracking URL</span>
                  <input formControlName="trackingUrl" />
                </label>

                <label class="ui-field">
                  <span>Note</span>
                  <textarea rows="3" formControlName="note"></textarea>
                </label>

                <button data-ui-button="primary" type="submit" [disabled]="isActing()">Add tracking</button>
              </form>

              <form [formGroup]="exceptionForm" class="seller-form-grid" novalidate>
                <label class="ui-field">
                  <span>Exception reason</span>
                  <textarea rows="3" formControlName="reason" maxlength="500"></textarea>
                  @if (exceptionForm.controls.reason.hasError('required')) {
                    <span class="ui-field-error">Reason is required.</span>
                  } @else if (exceptionForm.controls.reason.hasError('maxlength')) {
                    <span class="ui-field-error">Reason must be 500 characters or fewer.</span>
                  }
                </label>

                <div class="seller-action-row">
                  <button data-ui-button="secondary" type="button" [disabled]="isActing() || exceptionForm.invalid || !canMarkDeliveryFailed()" (click)="markDeliveryFailed()">Mark delivery failed</button>
                  <button data-ui-button="secondary" type="button" [disabled]="isActing() || exceptionForm.invalid || !canMarkReturnedToSender()" (click)="markReturnedToSender()">Returned to sender</button>
                </div>
              </form>
            </section>

            <section class="seller-panel">
              <h2>Carrier booking</h2>
              <p>Book a configured carrier for a ready-to-ship order, or keep using manual tracking when carrier booking is disabled.</p>

              @if (latestShipment()) {
                <dl class="seller-facts">
                  <div><dt>Provider</dt><dd>{{ latestShipment()!.carrierProviderName ?? 'Manual fulfilment' }}</dd></div>
                  <div><dt>Booking</dt><dd>{{ latestShipment()!.carrierBookingStatus ?? 'Not booked' }}</dd></div>
                  <div><dt>Provider status</dt><dd>{{ latestShipment()!.providerStatus ?? 'No carrier status yet' }}</dd></div>
                  @if (latestShipment()!.providerShipmentReference) {
                    <div><dt>Provider reference</dt><dd>{{ latestShipment()!.providerShipmentReference }}</dd></div>
                  }
                  @if (latestShipment()!.providerLastSyncedAtUtc) {
                    <div><dt>Last synced</dt><dd>{{ latestShipment()!.providerLastSyncedAtUtc | date:'medium' }}</dd></div>
                  }
                  @if (latestShipment()!.providerError) {
                    <div><dt>Provider error</dt><dd>{{ latestShipment()!.providerError }}</dd></div>
                  }
                </dl>

                <div class="seller-action-row">
                  @if (latestShipment()!.providerLabelUrl) {
                    <a data-ui-button="secondary" [href]="latestShipment()!.providerLabelUrl" target="_blank" rel="noreferrer">Open label</a>
                  }
                  @if (latestShipment()!.trackingUrl) {
                    <a data-ui-button="secondary" [href]="latestShipment()!.trackingUrl" target="_blank" rel="noreferrer">Track shipment</a>
                  }
                  <button data-ui-button="secondary" type="button" [disabled]="isActing() || !latestShipment()!.providerShipmentReference" (click)="syncCarrierTracking()">Sync carrier tracking</button>
                </div>
              }

              <form [formGroup]="carrierBookingForm" (ngSubmit)="bookCarrier()" class="seller-form-grid" novalidate>
                <label class="ui-field">
                  <span>Service code</span>
                  <input formControlName="serviceCode" />
                </label>

                <label class="ui-field">
                  <span>Weight kg</span>
                  <input type="number" formControlName="packageWeightKg" />
                </label>

                <label class="ui-field">
                  <span>Length cm</span>
                  <input type="number" formControlName="packageLengthCm" />
                </label>

                <label class="ui-field">
                  <span>Width cm</span>
                  <input type="number" formControlName="packageWidthCm" />
                </label>

                <label class="ui-field">
                  <span>Height cm</span>
                  <input type="number" formControlName="packageHeightCm" />
                </label>

                <label class="ui-field">
                  <span>Collection note</span>
                  <textarea rows="3" formControlName="collectionNote" maxlength="500"></textarea>
                </label>

                <button data-ui-button="primary" type="submit" [disabled]="isActing() || carrierBookingForm.invalid || !canBookCarrier()">Book carrier</button>
              </form>
            </section>
          </div>

          <section class="seller-panel">
            <h2>Items</h2>
            <div class="seller-item-list">
              @for (item of order()!.items; track item.orderItemId) {
                <div class="seller-item-row">
                  <span>
                    <strong>{{ item.productTitle ?? 'Untitled product' }}</strong>
                    <small>SKU {{ item.sku }} - {{ item.size }} / {{ item.colour }}</small>
                  </span>
                  <span>{{ item.quantity }} x {{ item.unitPrice | currency:'ZAR':'symbol-narrow' }}</span>
                  <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>
                </div>
              }
            </div>
          </section>

          <div class="seller-detail-grid">
            <section class="seller-panel">
              <h2>Status history</h2>
              <div class="seller-timeline">
                @for (history of order()!.statusHistory; track history.statusHistoryId) {
                  <div>
                    <app-status-badge [label]="history.newStatus" [tone]="statusTone(history.newStatus)" />
                    <span>{{ history.changedAtUtc | date:'medium' }}</span>
                    @if (history.reason) {
                      <small>{{ history.reason }}</small>
                    }
                  </div>
                }
              </div>
            </section>

            <section class="seller-panel">
              <h2>Shipments</h2>
              @if (order()!.shipments.length === 0) {
                <p>No shipment has been created for this order yet.</p>
              } @else {
                <div class="seller-timeline">
                  @for (shipment of order()!.shipments; track shipment.shipmentId) {
                    <div>
                      <app-status-badge [label]="shipment.status" [tone]="statusTone(shipment.status)" />
                      <span>{{ shipment.carrierName ?? 'Carrier not set' }}</span>
                      @if (shipment.trackingNumber) {
                        <small>{{ shipment.trackingNumber }}</small>
                      }
                      @if (shipment.providerStatus || shipment.providerShipmentReference) {
                        <small>Carrier {{ shipment.carrierProviderName ?? 'provider' }}: {{ shipment.providerStatus ?? shipment.carrierBookingStatus ?? 'Booked' }} {{ shipment.providerShipmentReference ? '- ' + shipment.providerShipmentReference : '' }}</small>
                      }
                      @if (shipment.providerLabelUrl) {
                        <a [href]="shipment.providerLabelUrl" target="_blank" rel="noreferrer">Open carrier label</a>
                      }
                      @for (event of shipment.events; track event.shipmentEventId) {
                        <small>{{ event.eventType }} - {{ event.occurredAtUtc | date:'medium' }}{{ event.message ? ': ' + event.message : '' }}</small>
                      }
                    </div>
                  }
                </div>
              }
            </section>

            <section class="seller-panel">
              <h2>Stock ledger</h2>
              <p>Reservation and payment stock events for this order. Paid settlement confirms reservations but does not deduct physical stock automatically.</p>
              @if (isStockLedgerLoading()) {
                <p>Loading stock ledger...</p>
              } @else if (stockLedgerError()) {
                <app-ui-alert tone="warning">{{ stockLedgerError() }}</app-ui-alert>
              } @else if (stockLedger().length === 0) {
                <p>No reservation or stock ledger events have been recorded for this order yet.</p>
              } @else {
                <div class="seller-timeline">
                  @for (movement of stockLedger(); track movement.movementId) {
                    <article class="seller-timeline-item">
                      <app-status-badge [label]="movementLabel(movement)" [tone]="movementTone(movement)" />
                      <strong>{{ movement.productTitle }}</strong>
                      <small>{{ movement.sku }} - {{ movement.occurredAtUtc | date:'medium' }}</small>
                      <small>Stock {{ movement.stockQuantityBefore }} -> {{ movement.stockQuantityAfter }} ({{ movement.quantityDelta > 0 ? '+' : '' }}{{ movement.quantityDelta }})</small>
                      <small>Reserved {{ movement.reservedQuantityBefore }} -> {{ movement.reservedQuantityAfter }} ({{ movement.reservedQuantityDelta > 0 ? '+' : '' }}{{ movement.reservedQuantityDelta }})</small>
                      <p>{{ movement.reason }}</p>
                    </article>
                  }
                </div>
              }
            </section>
          </div>
        }
      }
    </section>
  `
})
export class SellerOrderDetailPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly inventoryService = inject(SellerInventoryService);
  private readonly orderService = inject(SellerOrderService);
  private readonly route = inject(ActivatedRoute);

  protected readonly order = signal<SellerOrderResult | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly stockLedger = signal<SellerInventoryMovementResponse[]>([]);
  protected readonly isStockLedgerLoading = signal(false);
  protected readonly stockLedgerError = signal<string | null>(null);

  protected readonly trackingForm = this.formBuilder.group({
    carrierName: ['', [Validators.required]],
    trackingNumber: ['', [Validators.required]],
    trackingUrl: [''],
    note: ['']
  });
  protected readonly exceptionForm = this.formBuilder.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]]
  });
  protected readonly carrierBookingForm = this.formBuilder.group({
    packageWeightKg: [1, [Validators.required, Validators.min(0.01)]],
    packageLengthCm: [30, [Validators.required, Validators.min(0.01)]],
    packageWidthCm: [20, [Validators.required, Validators.min(0.01)]],
    packageHeightCm: [10, [Validators.required, Validators.min(0.01)]],
    serviceCode: ['STANDARD', [Validators.required, Validators.maxLength(80)]],
    collectionNote: ['', [Validators.maxLength(500)]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadOrder();
  }

  protected async markProcessing(): Promise<void> {
    await this.runAction(
      () => this.orderService.markProcessing(this.orderId()),
      'Order marked as processing.');
  }

  protected async markReadyToShip(): Promise<void> {
    await this.runAction(
      () => this.orderService.markReadyToShip(this.orderId()),
      'Order marked ready to ship.');
  }

  protected async markShipped(): Promise<void> {
    await this.runAction(
      () => this.orderService.markShipped(this.orderId()),
      'Order marked as shipped.');
  }

  protected async markDelivered(): Promise<void> {
    await this.runAction(
      () => this.orderService.markDelivered(this.orderId()),
      'Order marked as delivered.');
  }

  protected async markDeliveryFailed(): Promise<void> {
    await this.runExceptionAction(
      () => this.orderService.markDeliveryFailed(this.orderId(), { reason: this.exceptionReason() }),
      'Delivery failure recorded.');
  }

  protected async markReturnedToSender(): Promise<void> {
    await this.runExceptionAction(
      () => this.orderService.markReturnedToSender(this.orderId(), { reason: this.exceptionReason() }),
      'Return to sender recorded.');
  }

  protected async bookCarrier(): Promise<void> {
    if (this.carrierBookingForm.invalid || this.isActing()) {
      this.carrierBookingForm.markAllAsTouched();
      return;
    }

    const value = this.carrierBookingForm.getRawValue();
    await this.runAction(
      () => this.orderService.bookCarrier(this.orderId(), {
        packageWeightKg: Number(value.packageWeightKg),
        packageLengthCm: Number(value.packageLengthCm),
        packageWidthCm: Number(value.packageWidthCm),
        packageHeightCm: Number(value.packageHeightCm),
        serviceCode: value.serviceCode,
        collectionNote: emptyToNull(value.collectionNote)
      }),
      'Carrier booking updated.');
  }

  protected async syncCarrierTracking(): Promise<void> {
    await this.runAction(
      () => this.orderService.syncCarrierTracking(this.orderId()),
      'Carrier tracking synchronized.');
  }

  protected canMarkReadyToShip(): boolean {
    return ['Paid', 'Processing', 'ReadyToShip'].includes(this.order()?.status ?? '');
  }

  protected canMarkDelivered(): boolean {
    const order = this.order();
    if (!order || order.status !== 'Shipped') {
      return false;
    }

    const latestShipment = this.latestShipment();
    return latestShipment?.status === 'InTransit';
  }

  protected canMarkDeliveryFailed(): boolean {
    return this.order()?.status === 'Shipped' && this.latestShipment()?.status === 'InTransit';
  }

  protected canMarkReturnedToSender(): boolean {
    const status = this.latestShipment()?.status;
    return this.order()?.status === 'Shipped' && (status === 'InTransit' || status === 'DeliveryFailed');
  }

  protected canBookCarrier(): boolean {
    const latestShipment = this.latestShipment();
    return this.order()?.status === 'ReadyToShip'
      && latestShipment?.status === 'ReadyForCourier'
      && latestShipment.carrierBookingStatus !== 'Booked';
  }

  protected async addTracking(): Promise<void> {
    if (this.trackingForm.invalid || this.isActing()) {
      this.trackingForm.markAllAsTouched();
      return;
    }

    const value = this.trackingForm.getRawValue();
    await this.runAction(
      () => this.orderService.addTracking(this.orderId(), {
        carrierName: value.carrierName,
        trackingNumber: value.trackingNumber,
        trackingUrl: emptyToNull(value.trackingUrl),
        note: emptyToNull(value.note)
      }),
      'Tracking added.');

    if (!this.errorMessage()) {
      this.trackingForm.reset();
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Processing', 'ReadyToShip', 'AwaitingFulfilment', 'ReadyForCourier'].includes(status)) {
      return 'accent';
    }

    if (['Shipped', 'Delivered', 'Completed', 'InTransit'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Disputed', 'Failed', 'DeliveryFailed', 'ReturnedToSender'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  protected renderDeliveryAddress(address: NonNullable<SellerOrderResult['deliveryAddress']>): string {
    return [
      address.addressLine1,
      address.addressLine2,
      address.suburb,
      address.city,
      address.province,
      address.postalCode,
      address.countryCode
    ].filter(Boolean).join(', ');
  }

  protected deliveryEstimate(order: SellerOrderResult): string {
    if (order.deliveryEstimatedMinDays === null
      || order.deliveryEstimatedMinDays === undefined
      || order.deliveryEstimatedMaxDays === null
      || order.deliveryEstimatedMaxDays === undefined) {
      return 'estimate unavailable';
    }

    return order.deliveryEstimatedMinDays === order.deliveryEstimatedMaxDays
      ? `${order.deliveryEstimatedMinDays} day${order.deliveryEstimatedMinDays === 1 ? '' : 's'}`
      : `${order.deliveryEstimatedMinDays}-${order.deliveryEstimatedMaxDays} days`;
  }

  protected movementTone(movement: SellerInventoryMovementResponse): StatusBadgeTone {
    if (movement.movementType === 'PaymentFailedReservationReleased' || movement.movementType === 'ReturnRequested') {
      return 'warning';
    }

    if (movement.movementType === 'RefundCompleted') {
      return 'accent';
    }

    if (movement.movementType === 'ReturnRestocked') {
      return 'success';
    }

    if (movement.reservedQuantityDelta > 0) {
      return 'success';
    }

    if (movement.reservedQuantityDelta < 0) {
      return 'accent';
    }

    return 'neutral';
  }

  protected movementLabel(movement: SellerInventoryMovementResponse): string {
    const labels: Record<SellerInventoryMovementType, string> = {
      SellerAdjustment: 'Adjustment',
      BulkImportAdjustment: 'Bulk import',
      ReservationCreated: 'Reserved',
      ReservationReleased: 'Released',
      ReservationExpired: 'Expired',
      ReservationConfirmed: 'Confirmed',
      PaymentFailedReservationReleased: 'Payment release',
      ReturnRequested: 'Return requested',
      RefundCompleted: 'Refund completed',
      ReturnRestocked: 'Restocked'
    };

    return labels[movement.movementType] ?? movement.movementType;
  }

  private async loadOrder(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const order = await this.orderService.getOrder(this.orderId());
      this.order.set(order);
      await this.loadStockLedger(order.orderId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async loadStockLedger(orderId: string): Promise<void> {
    this.isStockLedgerLoading.set(true);
    this.stockLedgerError.set(null);

    try {
      this.stockLedger.set(await this.inventoryService.listHistory({ orderId }));
    } catch (error) {
      this.stockLedgerError.set(getApiErrorMessage(error));
    } finally {
      this.isStockLedgerLoading.set(false);
    }
  }

  private async runAction(action: () => Promise<SellerOrderResult>, successMessage: string): Promise<void> {
    if (this.isActing()) {
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const order = await action();
      this.order.set(order);
      await this.loadStockLedger(order.orderId);
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  private async runExceptionAction(action: () => Promise<SellerOrderResult>, successMessage: string): Promise<void> {
    if (this.exceptionForm.invalid) {
      this.exceptionForm.markAllAsTouched();
      return;
    }

    await this.runAction(action, successMessage);
    if (!this.errorMessage()) {
      this.exceptionForm.reset();
    }
  }

  private orderId(): string {
    return this.route.snapshot.paramMap.get('orderId') ?? '';
  }

  protected latestShipment() {
    const order = this.order();
    return order
      ? [...order.shipments].sort((left, right) => (left.shippedAtUtc ?? left.shipmentId).localeCompare(right.shippedAtUtc ?? right.shipmentId)).at(-1)
      : null;
  }

  private exceptionReason(): string {
    return this.exceptionForm.controls.reason.value.trim();
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
