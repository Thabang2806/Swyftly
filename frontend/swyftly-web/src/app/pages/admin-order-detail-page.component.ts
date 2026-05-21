import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AdminOrderDetailResponse } from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-order-detail-page',
  imports: [
    AdminWorkspaceNavComponent,
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    PageHeaderComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-finance-page">
      <app-admin-workspace-nav />
      <a class="admin-back-link" routerLink="/admin/orders">Back to orders</a>

      @if (isLoading()) {
        <div class="route-card">Loading order...</div>
      } @else if (errorMessage()) {
        <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
      } @else if (order()) {
        <app-page-header
          eyebrow="Admin order"
          [heading]="order()!.orderId"
          [description]="'Seller ' + (order()!.sellerDisplayName ?? order()!.sellerId)"
        >
          <div pageHeaderActions>
            <app-status-badge [label]="order()!.status" [tone]="statusTone(order()!.status)" />
            <a mat-stroked-button [routerLink]="['/admin/payments']" [queryParams]="{ orderId: order()!.orderId }">Related payments</a>
          </div>
        </app-page-header>

        <div class="admin-finance-policy">
          <app-status-badge label="Read only" tone="accent" />
          <span>Order state changes remain with seller fulfilment, payment webhooks, refunds, and dispute workflows.</span>
        </div>

        <div class="buyer-detail-grid">
          <section class="buyer-panel">
            <h2>Totals</h2>
            <dl class="seller-facts">
              <div><dt>Items subtotal</dt><dd>{{ order()!.itemsSubtotal | currency:'ZAR':'symbol-narrow' }}</dd></div>
              <div><dt>Shipping</dt><dd>{{ order()!.shippingAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
              @if (order()!.deliveryMethodName) {
                <div><dt>Delivery method</dt><dd>{{ order()!.deliveryMethodName }} - {{ deliveryEstimate(order()!) }}</dd></div>
              }
              <div><dt>Platform fee</dt><dd>{{ order()!.platformFeeAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
              <div><dt>Discount</dt><dd>{{ order()!.discountAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
              <div><dt>Total</dt><dd>{{ order()!.totalAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
            </dl>
          </section>

          <section class="buyer-panel">
            <h2>Parties</h2>
            <dl class="seller-facts">
              <div><dt>Buyer</dt><dd>{{ order()!.buyerId }}</dd></div>
              <div><dt>Seller</dt><dd>{{ order()!.sellerId }}</dd></div>
              <div><dt>Cart</dt><dd>{{ order()!.cartId }}</dd></div>
              <div><dt>Created</dt><dd>{{ order()!.createdAtUtc | date:'medium' }}</dd></div>
            </dl>
          </section>

          <section class="buyer-panel">
            <h2>Delivery address</h2>
            @if (order()!.deliveryAddress) {
              <dl class="seller-facts">
                <div><dt>Recipient</dt><dd>{{ order()!.deliveryAddress!.recipientName }}</dd></div>
                <div><dt>Phone</dt><dd>{{ order()!.deliveryAddress!.phoneNumber }}</dd></div>
                <div><dt>Address</dt><dd>{{ formatDeliveryAddress(order()!.deliveryAddress!) }}</dd></div>
                @if (order()!.deliveryAddress!.deliveryInstructions) {
                  <div><dt>Instructions</dt><dd>{{ order()!.deliveryAddress!.deliveryInstructions }}</dd></div>
                }
              </dl>
            } @else {
              <app-ui-alert tone="info">This order does not have a delivery-address snapshot.</app-ui-alert>
            }
          </section>
        </div>

        <section class="buyer-panel">
          <h2>Items</h2>
          <div class="seller-item-list">
            @for (item of order()!.items; track item.orderItemId) {
              <div class="seller-item-row">
                <span>
                  <strong>{{ item.productTitle ?? item.sku }}</strong>
                  <small>{{ item.sku }} - {{ item.size }} - {{ item.colour }}</small>
                </span>
                <span>{{ item.quantity }} x {{ item.unitPrice | currency:'ZAR':'symbol-narrow' }}</span>
                <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
            }
          </div>
        </section>

        <section class="buyer-panel">
          <h2>Payments</h2>
          <div class="admin-table" role="table" aria-label="Order payments">
            @for (payment of order()!.payments; track payment.paymentId) {
              <div class="admin-table-row admin-finance-table-row" role="row">
                <span><strong>{{ payment.provider }}</strong><small>{{ payment.providerReference ?? payment.paymentId }}</small></span>
                <span>{{ payment.amount | currency:payment.currency:'symbol-narrow' }}</span>
                <span><app-status-badge [label]="payment.status" [tone]="statusTone(payment.status)" /></span>
                <span><a mat-stroked-button [routerLink]="['/admin/payments', payment.paymentId]">Open</a></span>
              </div>
            } @empty {
              <p>No payment records are attached to this order yet.</p>
            }
          </div>
        </section>

        <div class="buyer-detail-grid">
          <section class="buyer-panel">
            <h2>Status history</h2>
            <div class="seller-timeline">
              @for (history of order()!.statusHistory; track history.statusHistoryId) {
                <div>
                  <strong>{{ history.newStatus }}</strong>
                  <small>{{ history.changedAtUtc | date:'medium' }}</small>
                  @if (history.reason) {
                    <p>{{ history.reason }}</p>
                  }
                </div>
              }
            </div>
          </section>

          <section class="buyer-panel">
            <h2>Shipments</h2>
            <div class="seller-timeline">
              @for (shipment of order()!.shipments; track shipment.shipmentId) {
                <div>
                  <strong>{{ shipment.status }}</strong>
                  <small>{{ shipment.carrierName ?? 'Manual fulfilment' }} {{ shipment.trackingNumber ?? '' }}</small>
                  @for (event of shipment.events; track event.shipmentEventId) {
                    <p>{{ event.eventType }} - {{ event.occurredAtUtc | date:'medium' }}</p>
                  }
                </div>
              } @empty {
                <p>No shipment records are attached yet.</p>
              }
            </div>
          </section>
        </div>
      }
    </section>
  `
})
export class AdminOrderDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly adminOrderPaymentService = inject(AdminOrderPaymentService);

  protected readonly order = signal<AdminOrderDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    try {
      this.order.set(await this.adminOrderPaymentService.getOrder(this.route.snapshot.paramMap.get('orderId') ?? ''));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Delivered', 'Completed', 'Processed'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Failed', 'DeliveryFailed', 'ReturnedToSender'].includes(status)) {
      return 'danger';
    }

    if (['PendingPayment', 'Pending', 'Authorized', 'Processing', 'Shipped'].includes(status)) {
      return 'warning';
    }

    return 'neutral';
  }

  protected formatDeliveryAddress(address: NonNullable<AdminOrderDetailResponse['deliveryAddress']>): string {
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

  protected deliveryEstimate(order: AdminOrderDetailResponse): string {
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
}
