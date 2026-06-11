import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-orders-page',
  imports: [
    BuyerWorkspaceNavComponent,
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Orders"
        description="Track purchase status, shipment progress, and after-sales actions."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/shop">Shop again</a>
        </div>
      </app-page-header>

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card buyer-filter-bar" novalidate>
        <label class="ui-field">
          <span>Search orders</span>
          <input formControlName="search" />
        </label>

        <label class="ui-field">
          <span>Status</span>
          <input formControlName="status" placeholder="Delivered, Paid, Shipped" />
        </label>

        <div class="buyer-action-row">
          <button data-ui-button="primary" type="submit">Apply filters</button>
          <button data-ui-button="secondary" type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading orders...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (filteredOrders().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Orders"
            heading="No orders found"
            message="Orders that match your filters will appear here."
          >
            <a data-ui-button="primary" routerLink="/shop">Browse marketplace</a>
          </app-empty-state>
        } @else {
          <div class="admin-table buyer-ops-table" role="table" aria-label="Buyer orders">
            <div class="admin-table-row heading buyer-ops-table-row" role="row">
              <span role="columnheader">Order</span>
              <span role="columnheader">Items</span>
              <span role="columnheader">Total</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (order of filteredOrders(); track order.orderId) {
              <div class="admin-table-row buyer-ops-table-row" role="row">
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
                  <small>{{ paymentSummary(order) }}</small>
                  <small>{{ shipmentSummary(order) }}</small>
                </span>
                <span role="cell">
                  <a data-ui-button="secondary" [routerLink]="['/account/orders', order.orderId]">Open</a>
                </span>
              </div>
            }
          </div>

          <p class="audit-count">{{ filteredOrders().length }} of {{ orders().length }} order{{ orders().length === 1 ? '' : 's' }}</p>
        }
      }
    </section>
  `
})
export class BuyerOrdersPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly orderService = inject(BuyerOrderService);

  protected readonly orders = signal<BuyerOrderResult[]>([]);
  protected readonly filters = signal({ search: '', status: '' });
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: ['']
  });

  protected readonly filteredOrders = computed(() => {
    const { search, status } = this.filters();
    const normalizedSearch = search.trim().toLowerCase();
    const normalizedStatus = status.trim().toLowerCase();

    return this.orders().filter(order => {
      const haystack = [
        order.orderId,
        order.status,
        order.items.map(item => `${item.productTitle ?? ''} ${item.sku} ${item.size} ${item.colour}`).join(' ')
      ].join(' ').toLowerCase();

      return (normalizedSearch.length === 0 || haystack.includes(normalizedSearch)) &&
        (normalizedStatus.length === 0 || order.status.toLowerCase().includes(normalizedStatus));
    });
  });

  async ngOnInit(): Promise<void> {
    await this.loadOrders();
  }

  protected applyFilters(): void {
    this.filters.set(this.filtersForm.getRawValue());
  }

  protected clearFilters(): void {
    this.filtersForm.reset({ search: '', status: '' });
    this.applyFilters();
  }

  protected itemCount(order: BuyerOrderResult): number {
    return order.items.reduce((total, item) => total + item.quantity, 0);
  }

  protected primaryItemLabel(order: BuyerOrderResult): string {
    const firstItem = order.items[0];
    return firstItem?.productTitle ?? firstItem?.sku ?? 'Order items';
  }

  protected latestStatusDate(order: BuyerOrderResult): string | null {
    const latest = [...order.statusHistory].sort((a, b) => b.changedAtUtc.localeCompare(a.changedAtUtc))[0];
    return latest?.changedAtUtc ?? null;
  }

  protected shipmentSummary(order: BuyerOrderResult): string {
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

  protected paymentSummary(order: BuyerOrderResult): string {
    const payment = order.paymentSummary;
    if (!payment) {
      return order.status === 'PendingPayment' ? 'Payment not started yet' : 'No payment record';
    }

    return `Payment ${payment.status} - ${payment.providerName}`;
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Processing', 'ReadyToShip', 'AwaitingFulfilment'].includes(status)) {
      return 'accent';
    }

    if (['Shipped', 'Delivered', 'Completed'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Disputed', 'Failed'].includes(status)) {
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
