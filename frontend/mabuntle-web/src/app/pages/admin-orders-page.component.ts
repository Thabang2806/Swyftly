import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminOrderSummaryResponse } from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-orders-page',
  imports: [
    AdminWorkspaceNavComponent,
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
    <section class="page admin-finance-page">
      <app-admin-workspace-nav />

      <app-page-header
        eyebrow="Admin operations"
        heading="Orders"
        description="Read-only marketplace order search for finance, fulfilment, and dispute context."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/payments">Payments</a>
          <a data-ui-button="secondary" routerLink="/refunds">Refunds</a>
        </div>
      </app-page-header>

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-support-filters" novalidate>
        <label class="ui-field">
          <span>Search</span>
          <input formControlName="search" placeholder="Order, buyer, seller, product" />
        </label>

        <label class="ui-field">
          <span>Status</span>
          <input formControlName="status" placeholder="Paid, Shipped, Delivered" />
        </label>

        <div class="buyer-action-row">
          <button data-ui-button="primary" type="submit">Apply filters</button>
          <button data-ui-button="secondary" type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading admin orders...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (filteredOrders().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Orders"
            heading="No orders found"
            message="Marketplace orders matching the current filters will appear here."
          />
        } @else {
          <div class="admin-table admin-finance-table" role="table" aria-label="Admin orders">
            <div class="admin-table-row heading admin-finance-table-row" role="row">
              <span role="columnheader">Order</span>
              <span role="columnheader">Seller</span>
              <span role="columnheader">Total</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (order of filteredOrders(); track order.orderId) {
              <div class="admin-table-row admin-finance-table-row" role="row">
                <span role="cell">
                  <strong>{{ order.orderId }}</strong>
                  <small>{{ order.createdAtUtc | date:'medium' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ order.sellerDisplayName ?? order.sellerId }}</strong>
                  <small>Buyer {{ order.buyerId }}</small>
                </span>
                <span role="cell">
                  <strong>{{ order.totalAmount | currency:'ZAR':'symbol-narrow' }}</strong>
                  <small>{{ order.itemCount }} item{{ order.itemCount === 1 ? '' : 's' }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="order.status" [tone]="statusTone(order.status)" />
                  <small>Payment {{ order.paymentStatus ?? 'None' }}{{ order.shipmentStatus ? ', shipment ' + order.shipmentStatus : '' }}</small>
                </span>
                <span role="cell">
                  <a data-ui-button="secondary" [routerLink]="['/orders', order.orderId]">Open</a>
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
export class AdminOrdersPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminOrderPaymentService = inject(AdminOrderPaymentService);

  protected readonly orders = signal<AdminOrderSummaryResponse[]>([]);
  protected readonly filters = signal({ search: '', status: '' });
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: ['']
  });

  protected readonly filteredOrders = computed(() => {
    const search = this.filters().search.trim().toLowerCase();
    const status = this.filters().status.trim().toLowerCase();

    return this.orders().filter(order => {
      const matchesStatus = !status || order.status.toLowerCase().includes(status);
      const searchable = [
        order.orderId,
        order.buyerId,
        order.sellerId,
        order.sellerDisplayName ?? '',
        order.paymentStatus ?? '',
        order.shipmentStatus ?? ''
      ].join(' ').toLowerCase();
      return matchesStatus && (!search || searchable.includes(search));
    });
  });

  async ngOnInit(): Promise<void> {
    await this.loadOrders();
  }

  protected async applyFilters(): Promise<void> {
    this.filters.set(this.filtersForm.getRawValue());
    await this.loadOrders(this.filters().status);
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset({ search: '', status: '' });
    this.filters.set({ search: '', status: '' });
    await this.loadOrders();
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Delivered', 'Completed'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Failed'].includes(status)) {
      return 'danger';
    }

    if (['PendingPayment', 'Processing', 'Shipped'].includes(status)) {
      return 'warning';
    }

    return 'neutral';
  }

  private async loadOrders(status = ''): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.orders.set(await this.adminOrderPaymentService.getOrders(status));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
