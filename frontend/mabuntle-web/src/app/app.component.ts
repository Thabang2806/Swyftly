import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { BuyerNotificationResponse } from './buyer/buyer-engagement.models';
import { BuyerNotificationRealtimeService } from './buyer/buyer-notification-realtime.service';
import { FRONTEND_HOSTS } from './frontend-experience';
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
  private readonly router = inject(Router);
  protected readonly sellerRegisterUrl = `${FRONTEND_HOSTS.seller}/register/seller`;

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
    return [];
  });

  protected readonly adminNavigationItems = computed<NavigationItem[]>(() => {
    return [];
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

}
