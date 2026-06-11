import { DOCUMENT } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, ViewEncapsulation, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ensureLazyStylesheet } from '../shared/ui/lazy-stylesheet';
import { SellerNotificationService } from './seller-notification.service';

type SellerWorkspaceNavItem = {
  label: string;
  route: string;
  exact: boolean;
  badge?: 'sellerUnread';
};

type SellerWorkspaceNavGroup = {
  label: string;
  items: readonly SellerWorkspaceNavItem[];
};

@Component({
  selector: 'app-seller-workspace-nav',
  encapsulation: ViewEncapsulation.None,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="seller-workspace-nav" aria-label="Seller workspace navigation">
      <div class="seller-workspace-nav-brand">
        <span>S</span>
        <strong>Mabuntle</strong>
        <small>Seller studio</small>
      </div>

      @for (group of groups; track group.label) {
        <section class="workspace-nav-group" [attr.aria-label]="group.label">
          <span class="workspace-nav-group-label">{{ group.label }}</span>

          @for (item of group.items; track item.route) {
            <a
              [routerLink]="item.route"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: item.exact }"
              ariaCurrentWhenActive="page"
            >
              {{ item.label }}
              @if (item.badge === 'sellerUnread' && unreadCount() > 0) {
                <span class="workspace-nav-badge" [attr.aria-label]="unreadCount() + ' unread seller notifications'">
                  {{ unreadCount() }}
                </span>
              }
            </a>
          }
        </section>
      }
    </nav>
  `
})
export class SellerWorkspaceNavComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly notificationService = inject(SellerNotificationService);

  protected readonly unreadCount = this.notificationService.unreadCount;

  protected readonly groups: readonly SellerWorkspaceNavGroup[] = [
    {
      label: 'Overview',
      items: [
        { label: 'Dashboard', route: '/', exact: true },
        { label: 'Analytics', route: '/analytics', exact: false }
      ]
    },
    {
      label: 'Catalog',
      items: [
        { label: 'Products', route: '/products', exact: false },
        { label: 'Inventory', route: '/inventory', exact: false },
        { label: 'Store settings', route: '/settings/store', exact: false }
      ]
    },
    {
      label: 'Operations',
      items: [
        { label: 'Orders', route: '/orders', exact: false },
        { label: 'Returns', route: '/returns', exact: false },
        { label: 'Support', route: '/support', exact: false },
        { label: 'Notifications', route: '/notifications', exact: false, badge: 'sellerUnread' }
      ]
    },
    {
      label: 'Growth and finance',
      items: [
        { label: 'Ads', route: '/ads', exact: false },
        { label: 'Payouts', route: '/payouts', exact: false }
      ]
    }
  ];

  async ngOnInit(): Promise<void> {
    ensureLazyStylesheet(this.document, this.platformId, 'luxury-seller', '/styles/luxury-seller.css');
    await this.loadUnreadCount();
  }

  private async loadUnreadCount(): Promise<void> {
    try {
      await this.notificationService.refreshUnreadCount();
    } catch {
      this.unreadCount.set(0);
    }
  }
}
