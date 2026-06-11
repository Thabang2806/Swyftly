import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { SellerNotificationResponse } from './seller/seller-notification.models';
import { SellerNotificationRealtimeService } from './seller/seller-notification-realtime.service';
import { MobileBottomNavComponent, MobileBottomNavItem } from './shared/ui/mobile-bottom-nav.component';

@Component({
  selector: 'app-seller-root',
  imports: [MobileBottomNavComponent, RouterLink, RouterOutlet],
  template: `
    <main class="subdomain-app-main">
      <router-outlet />
    </main>

    <app-mobile-bottom-nav [items]="mobileNavigationItems()" />

    @if (notificationRealtime.latestNotification(); as notification) {
      <aside class="notification-toast" aria-live="polite">
        <div>
          <strong>{{ notification.title }}</strong>
          <p>{{ notification.message }}</p>
        </div>
        <a [routerLink]="notificationRoute(notification)" (click)="notificationRealtime.dismissLatestNotification()">Open</a>
        <button type="button" aria-label="Dismiss notification" (click)="notificationRealtime.dismissLatestNotification()">Close</button>
      </aside>
    }
  `
})
export class SellerAppComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  protected readonly notificationRealtime = inject(SellerNotificationRealtimeService);
  private readonly router = inject(Router);

  protected readonly mobileNavigationItems = computed<MobileBottomNavItem[]>(() => {
    if (!this.authService.hasAnyRole(['Seller'])) {
      return [
        { label: 'Home', route: '/' },
        { label: 'Register', route: '/register/seller' },
        { label: 'Sign in', route: '/login' }
      ];
    }

    return [
      { label: 'Home', route: '/' },
      { label: 'Alerts', route: '/notifications', badge: this.notificationRealtime.unreadCount() },
      { label: 'Orders', route: '/orders' },
      { label: 'Products', route: '/products' },
      { label: 'Payouts', route: '/payouts' }
    ];
  });

  ngOnInit(): void {
    void this.authService.initialize();
  }

  protected async logout(): Promise<void> {
    await this.authService.logout();
    await this.router.navigateByUrl('/login');
  }

  protected notificationRoute(notification: SellerNotificationResponse): string {
    if (notification.relatedEntityType === 'Product' && notification.relatedEntityId) {
      return `/products/${notification.relatedEntityId}/edit`;
    }

    if (notification.relatedEntityType === 'AdCampaign' && notification.relatedEntityId) {
      return `/ads/${notification.relatedEntityId}`;
    }

    if (notification.relatedEntityType === 'SellerProfile') {
      return '/';
    }

    if (notification.relatedEntityType === 'SellerAnalytics') {
      return '/analytics';
    }

    return '/notifications';
  }
}

