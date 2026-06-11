import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BuyerNotificationResponse, BuyerProductReviewResponse, BuyerWishlistItemResponse } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerDisputeResponse } from '../buyer/buyer-dispute.models';
import { BuyerDisputeService } from '../buyer/buyer-dispute.service';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerRefundResult } from '../buyer/buyer-refund.models';
import { BuyerRefundService } from '../buyer/buyer-refund.service';
import { BuyerReturnRequestResult } from '../buyer/buyer-return.models';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerSupportTicketResponse } from '../buyer/buyer-support.models';
import { BuyerSupportService } from '../buyer/buyer-support.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { DashboardCardComponent } from '../shared/ui/dashboard-card.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-account-page',
  imports: [
    BuyerWorkspaceNavComponent,
    CurrencyPipe,
    DashboardCardComponent,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Account dashboard"
        description="Track purchases, returns, disputes, and marketplace support from one workspace."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/shop">Continue shopping</a>
          <a data-ui-button="secondary" routerLink="/cart">Cart</a>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading account activity...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        <div class="buyer-dashboard-grid">
          <app-dashboard-card
            eyebrow="Purchases"
            heading="Orders"
            [description]="orders().length + ' order' + (orders().length === 1 ? '' : 's') + ' in your account.'"
          >
            <a data-ui-button="secondary" routerLink="/account/orders">View orders</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="After-sales"
            heading="Returns"
            [description]="activeReturns().length + ' active return' + (activeReturns().length === 1 ? '' : 's') + ' need attention.'"
          >
            <a data-ui-button="secondary" routerLink="/account/returns">View returns</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Finance"
            heading="Refunds"
            [description]="activeRefunds().length + ' refund' + (activeRefunds().length === 1 ? '' : 's') + ' in progress or needing attention.'"
          >
            <a data-ui-button="secondary" routerLink="/account/refunds">View refunds</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Resolution"
            heading="Disputes"
            [description]="openDisputes().length + ' open dispute' + (openDisputes().length === 1 ? '' : 's') + ' on record.'"
          >
            <a data-ui-button="secondary" routerLink="/account/disputes">View disputes</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Help"
            heading="Support"
            [description]="openTickets().length + ' open support ticket' + (openTickets().length === 1 ? '' : 's') + '.'"
          >
            <a data-ui-button="secondary" routerLink="/account/support">Open support</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Saved"
            heading="Wishlist"
            [description]="wishlist().length + ' saved product' + (wishlist().length === 1 ? '' : 's') + ' in your account.'"
          >
            <a data-ui-button="secondary" routerLink="/account/wishlist">View wishlist</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Feedback"
            heading="Reviews"
            [description]="reviews().length + ' product review' + (reviews().length === 1 ? '' : 's') + ' written.'"
          >
            <a data-ui-button="secondary" routerLink="/account/reviews">Manage reviews</a>
          </app-dashboard-card>

          <app-dashboard-card
            eyebrow="Updates"
            heading="Notifications"
            [description]="unreadNotifications().length + ' unread notification' + (unreadNotifications().length === 1 ? '' : 's') + '.'"
          >
            <a data-ui-button="secondary" routerLink="/account/notifications">View notifications</a>
          </app-dashboard-card>
        </div>

        @if (nextActions().length > 0) {
          <section class="buyer-panel">
            <h2>Next best actions</h2>
            <div class="seller-result-steps">
              @for (action of nextActions(); track action.route) {
                <div>
                  <strong>{{ action.title }}</strong>
                  <span>{{ action.description }}</span>
                  <a data-ui-button="secondary" [routerLink]="action.route">{{ action.cta }}</a>
                </div>
              }
            </div>
          </section>
        }

        @if (hasNoActivity() && !errorMessage()) {
          <app-empty-state
            eyebrow="Account"
            heading="No buyer activity yet"
            message="Orders, returns, disputes, and support tickets will appear here after you start shopping."
          >
            <a data-ui-button="primary" routerLink="/shop">Browse marketplace</a>
          </app-empty-state>
        } @else {
          <div class="buyer-detail-grid">
            <section class="buyer-panel">
              <h2>Recent orders</h2>
              @if (recentOrders().length === 0) {
                <p>No order activity yet.</p>
              } @else {
                <div class="buyer-activity-list">
                  @for (order of recentOrders(); track order.orderId) {
                    <a [routerLink]="['/account/orders', order.orderId]">
                      <span>
                        <strong>{{ primaryItemLabel(order) }}</strong>
                        <small>{{ itemCount(order) }} item{{ itemCount(order) === 1 ? '' : 's' }} - {{ order.totalAmount | currency:'ZAR':'symbol-narrow' }}</small>
                      </span>
                      <app-status-badge [label]="order.status" [tone]="statusTone(order.status)" />
                    </a>
                  }
                </div>
              }
            </section>

            <section class="buyer-panel">
              <h2>Recent support</h2>
              @if (recentTickets().length === 0) {
                <p>No support tickets yet.</p>
              } @else {
                <div class="buyer-activity-list">
                  @for (ticket of recentTickets(); track ticket.supportTicketId) {
                    <a [routerLink]="['/account/support', ticket.supportTicketId]">
                      <span>
                        <strong>{{ ticket.subject }}</strong>
                        <small>{{ ticket.openedAtUtc | date:'mediumDate' }}</small>
                      </span>
                      <app-status-badge [label]="ticket.status" [tone]="ticketStatusTone(ticket.status)" />
                    </a>
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
export class AccountPageComponent implements OnInit {
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly disputeService = inject(BuyerDisputeService);
  private readonly orderService = inject(BuyerOrderService);
  private readonly refundService = inject(BuyerRefundService);
  private readonly returnService = inject(BuyerReturnService);
  private readonly supportService = inject(BuyerSupportService);

  protected readonly orders = signal<BuyerOrderResult[]>([]);
  protected readonly returns = signal<BuyerReturnRequestResult[]>([]);
  protected readonly refunds = signal<BuyerRefundResult[]>([]);
  protected readonly disputes = signal<BuyerDisputeResponse[]>([]);
  protected readonly tickets = signal<BuyerSupportTicketResponse[]>([]);
  protected readonly wishlist = signal<BuyerWishlistItemResponse[]>([]);
  protected readonly reviews = signal<BuyerProductReviewResponse[]>([]);
  protected readonly notifications = signal<BuyerNotificationResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly activeReturns = computed(() =>
    this.returns().filter(item => !['Refunded', 'Closed', 'Rejected'].includes(item.status)));
  protected readonly activeRefunds = computed(() =>
    this.refunds().filter(item => ['Requested', 'Approved', 'Processing', 'Failed'].includes(item.status)));
  protected readonly openDisputes = computed(() =>
    this.disputes().filter(item => !['Resolved', 'Closed'].includes(item.status)));
  protected readonly openTickets = computed(() =>
    this.tickets().filter(item => !['Resolved', 'Closed'].includes(item.status)));
  protected readonly unreadNotifications = computed(() =>
    this.notifications().filter(item => !item.readAtUtc));
  protected readonly recentOrders = computed(() => this.orders().slice(0, 3));
  protected readonly recentTickets = computed(() => this.tickets().slice(0, 3));
  protected readonly nextActions = computed(() => {
    const actions: { title: string; description: string; cta: string; route: string | string[] }[] = [];
    const pendingPaymentOrder = this.orders().find(order =>
      order.status === 'PendingPayment' || order.paymentSummary?.status === 'Pending');
    const deliveredOrder = this.orders().find(order => order.status === 'Delivered');
    const activeRefund = this.activeRefunds()[0];
    const openTicket = this.openTickets()[0];
    const unreadNotification = this.unreadNotifications()[0];

    if (pendingPaymentOrder) {
      actions.push({
        title: 'Payment still pending',
        description: 'Refresh the order payment status or retry payment from the order page.',
        cta: 'Review payment',
        route: ['/account/orders', pendingPaymentOrder.orderId]
      });
    }

    if (deliveredOrder) {
      actions.push({
        title: 'Delivered order ready for after-sales',
        description: 'Open the delivered order to request a return or leave a moderated product review.',
        cta: 'Open order',
        route: ['/account/orders', deliveredOrder.orderId]
      });
    }

    if (activeRefund) {
      actions.push({
        title: activeRefund.status === 'Failed' ? 'Refund needs support' : 'Refund in progress',
        description: activeRefund.statusMessage,
        cta: 'Track refund',
        route: '/account/refunds'
      });
    }

    if (openTicket) {
      actions.push({
        title: 'Support ticket open',
        description: 'Check whether support has asked for more detail or replied to your request.',
        cta: 'View ticket',
        route: ['/account/support', openTicket.supportTicketId]
      });
    }

    if (unreadNotification) {
      actions.push({
        title: 'Unread account updates',
        description: 'Read the latest marketplace update and mark it handled when you are done.',
        cta: 'View notifications',
        route: '/account/notifications'
      });
    }

    return actions.slice(0, 4);
  });

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [orders, returns, refunds, disputes, tickets, wishlist, reviews, notifications] = await Promise.all([
        this.orderService.listOrders(),
        this.returnService.listReturns(),
        this.refundService.listRefunds(),
        this.disputeService.listDisputes(),
        this.supportService.listTickets(),
        this.engagementService.listWishlist(),
        this.engagementService.listBuyerReviews(),
        this.engagementService.listNotifications()
      ]);
      this.orders.set(orders);
      this.returns.set(returns);
      this.refunds.set(refunds);
      this.disputes.set(disputes);
      this.tickets.set(tickets);
      this.wishlist.set(wishlist);
      this.reviews.set(reviews);
      this.notifications.set(notifications);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  protected hasNoActivity(): boolean {
    return this.orders().length === 0 &&
      this.returns().length === 0 &&
      this.refunds().length === 0 &&
      this.disputes().length === 0 &&
      this.tickets().length === 0 &&
      this.wishlist().length === 0 &&
      this.reviews().length === 0 &&
      this.notifications().length === 0;
  }

  protected itemCount(order: BuyerOrderResult): number {
    return order.items.reduce((total, item) => total + item.quantity, 0);
  }

  protected primaryItemLabel(order: BuyerOrderResult): string {
    const firstItem = order.items[0];
    return firstItem?.productTitle ?? firstItem?.sku ?? 'Order items';
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

  protected ticketStatusTone(status: string): StatusBadgeTone {
    if (['Open', 'WaitingForBuyer', 'Escalated'].includes(status)) {
      return 'warning';
    }

    if (['Resolved', 'Closed'].includes(status)) {
      return 'success';
    }

    return 'neutral';
  }
}
