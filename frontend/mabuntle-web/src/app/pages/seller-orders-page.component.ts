import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerOrderResult } from '../seller/seller-order.models';
import { SellerOrderService } from '../seller/seller-order.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-orders-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page">
      <app-seller-workspace-nav />

      <app-page-header
        eyebrow="Seller operations"
        heading="Orders"
        description="Review paid and fulfilment orders for your store."
      />

      @if (isLoading()) {
        <div class="route-card">Loading orders...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (orders().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Orders"
            heading="No seller orders yet"
            message="Orders will appear here after checkout and payment activity starts for your products."
          />
        } @else {
          <div class="admin-table seller-ops-table" role="table" aria-label="Seller orders">
            <div class="admin-table-row heading seller-ops-table-row" role="row">
              <span role="columnheader">Order</span>
              <span role="columnheader">Items</span>
              <span role="columnheader">Total</span>
              <span role="columnheader">Fulfilment</span>
              <span role="columnheader">Action</span>
            </div>

            @for (order of orders(); track order.orderId) {
              <div class="admin-table-row seller-ops-table-row" role="row">
                <span role="cell">
                  <strong>{{ order.orderId }}</strong>
                  <small>{{ latestStatusDate(order) | date:'medium' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ itemCount(order) }} item{{ itemCount(order) === 1 ? '' : 's' }}</strong>
                  <small>{{ primaryItemLabel(order) }}</small>
                </span>
                <span role="cell">
                  <strong>{{ order.totalAmount | currency:'ZAR':'symbol-narrow' }}</strong>
                  <small>Subtotal {{ order.itemsSubtotal | currency:'ZAR':'symbol-narrow' }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="order.status" [tone]="statusTone(order.status)" />
                  <small>{{ shipmentSummary(order) }}</small>
                </span>
                <span role="cell">
                  <a data-ui-button="secondary" [routerLink]="['/orders', order.orderId]">Open</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class SellerOrdersPageComponent implements OnInit {
  private readonly orderService = inject(SellerOrderService);

  protected readonly orders = signal<SellerOrderResult[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadOrders();
  }

  protected itemCount(order: SellerOrderResult): number {
    return order.items.reduce((total, item) => total + item.quantity, 0);
  }

  protected primaryItemLabel(order: SellerOrderResult): string {
    const firstItem = order.items[0];
    return firstItem?.productTitle ?? firstItem?.sku ?? 'Order items';
  }

  protected latestStatusDate(order: SellerOrderResult): string | null {
    const latest = [...order.statusHistory].sort((a, b) => b.changedAtUtc.localeCompare(a.changedAtUtc))[0];
    return latest?.changedAtUtc ?? null;
  }

  protected shipmentSummary(order: SellerOrderResult): string {
    const latestShipment = order.shipments[order.shipments.length - 1];
    if (!latestShipment) {
      return 'No shipment yet';
    }

    const carrierState = latestShipment.providerStatus ?? latestShipment.status;
    return latestShipment.trackingNumber
      ? `${carrierState} - ${latestShipment.trackingNumber}`
      : latestShipment.providerShipmentReference
        ? `${carrierState} - ${latestShipment.providerShipmentReference}`
        : carrierState;
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Processing', 'ReadyToShip'].includes(status)) {
      return 'accent';
    }

    if (['Shipped', 'Delivered', 'Completed'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Disputed'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  private async loadOrders(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.orders.set(await this.orderService.listOrders());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
