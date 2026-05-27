import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { BuyerNotificationResponse } from './buyer/buyer-engagement.models';
import { BuyerNotificationRealtimeService } from './buyer/buyer-notification-realtime.service';
import { SellerNotificationResponse } from './seller/seller-notification.models';
import { SellerNotificationRealtimeService } from './seller/seller-notification-realtime.service';
import { MobileBottomNavComponent, MobileBottomNavItem } from './shared/ui/mobile-bottom-nav.component';

type NavigationItem = {
  label: string;
  route: string;
  badge?: number;
};

@Component({
  selector: 'app-root',
  imports: [MobileBottomNavComponent, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  protected readonly notificationRealtime = inject(BuyerNotificationRealtimeService);
  protected readonly sellerNotificationRealtime = inject(SellerNotificationRealtimeService);
  private readonly router = inject(Router);

  protected readonly publicNavigationItems: NavigationItem[] = [
    { label: 'Shop', route: '/shop' },
    { label: 'Sell', route: '/sell' }
  ];

  protected readonly authNavigationItems = computed<NavigationItem[]>(() => {
    if (!this.authService.isAuthenticated()) {
      return [
        { label: 'Sign in', route: '/login' },
        { label: 'Join', route: '/register/buyer' }
      ];
    }

    return [];
  });

  protected readonly buyerNavigationItems = computed<NavigationItem[]>(() => {
    if (!this.authService.hasAnyRole(['Buyer'])) {
      return [];
    }

    return [
      { label: 'Assistant', route: '/assistant' },
      { label: 'Visual search', route: '/visual-search' },
      { label: 'Cart', route: '/cart' },
      { label: 'Wishlist', route: '/account/wishlist' },
      { label: 'Notifications', route: '/account/notifications', badge: this.notificationRealtime.unreadCount() },
      { label: 'Account', route: '/account' }
    ];
  });

  protected readonly sellerNavigationItems = computed<NavigationItem[]>(() => {
    if (!this.authService.hasAnyRole(['Seller'])) {
      return [];
    }

    return [
      { label: 'Seller', route: '/seller', badge: this.sellerNotificationRealtime.unreadCount() }
    ];
  });

  protected readonly adminNavigationItems = computed<NavigationItem[]>(() => {
    const items: NavigationItem[] = [];

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin'])) {
      items.push({ label: 'Admin', route: '/admin' });
      items.push({ label: 'Categories', route: '/admin/categories' });
      items.push({ label: 'Pickup points', route: '/admin/pickup-points' });
    }

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin', 'SupportAgent'])) {
      items.push({ label: 'Support', route: '/admin/support' });
    }

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin', 'FinanceOperator', 'FinanceApprover'])) {
      items.push({ label: 'Refunds', route: '/admin/refunds' });
      items.push({ label: 'Payouts', route: '/admin/payouts' });
      items.push({ label: 'Payout profile', route: '/admin/payout-profile-changes' });
    }

    return items;
  });

  protected readonly hasWorkspaceNavigation = computed(() => {
    return this.buyerNavigationItems().length > 0 ||
      this.sellerNavigationItems().length > 0 ||
      this.adminNavigationItems().length > 0;
  });

  protected readonly mobileNavigationItems = computed<MobileBottomNavItem[]>(() => {
    const items: MobileBottomNavItem[] = [
      { label: 'Home', route: '/' },
      { label: 'Search', route: '/shop' }
    ];

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin', 'FinanceOperator', 'FinanceApprover', 'SupportAgent'])) {
      return [
        ...items,
        { label: 'Admin', route: '/admin' },
        { label: 'Queues', route: '/admin/products' },
        { label: 'Finance', route: '/admin/payouts' }
      ];
    }

    if (this.authService.hasAnyRole(['Seller'])) {
      return [
        ...items,
        { label: 'Seller', route: '/seller' },
        { label: 'Alerts', route: '/seller/notifications', badge: this.sellerNotificationRealtime.unreadCount() },
        { label: 'Orders', route: '/seller/orders' },
        { label: 'Payouts', route: '/seller/payouts' }
      ];
    }

    if (this.authService.hasAnyRole(['Buyer'])) {
      return [
        ...items,
        { label: 'AI', route: '/assistant' },
        { label: 'Alerts', route: '/account/notifications', badge: this.notificationRealtime.unreadCount() },
        { label: 'Orders', route: '/account/orders' },
        { label: 'Me', route: '/account' }
      ];
    }

    return [
      ...items,
      { label: 'Sell', route: '/sell' },
      { label: 'Join', route: '/register/buyer' },
      { label: 'Sign in', route: '/login' }
    ];
  });

  ngOnInit(): void {
    void this.authService.initialize();
  }

  protected async logout(): Promise<void> {
    await this.authService.logout();
    await this.router.navigateByUrl('/login');
  }

  protected notificationRoute(notification: BuyerNotificationResponse): string {
    if (notification.relatedEntityType === 'Order' && notification.relatedEntityId) {
      return `/account/orders/${notification.relatedEntityId}`;
    }

    if (notification.relatedEntityType === 'ReturnRequest' && notification.relatedEntityId) {
      return `/account/returns/${notification.relatedEntityId}`;
    }

    if (notification.relatedEntityType === 'SupportTicket' && notification.relatedEntityId) {
      return `/account/support/${notification.relatedEntityId}`;
    }

    if (notification.relatedEntityType === 'ProductReview') {
      return '/account/reviews';
    }

    return '/account/notifications';
  }

  protected sellerNotificationRoute(notification: SellerNotificationResponse): string {
    if (notification.relatedEntityType === 'Product' && notification.relatedEntityId) {
      return `/seller/products/${notification.relatedEntityId}/edit`;
    }

    if (notification.relatedEntityType === 'AdCampaign' && notification.relatedEntityId) {
      return `/seller/ads/${notification.relatedEntityId}`;
    }

    if (notification.relatedEntityType === 'SellerProfile') {
      return '/seller';
    }

    if (notification.relatedEntityType === 'SellerAnalytics') {
      return '/seller/analytics';
    }

    return '/seller/notifications';
  }
}
