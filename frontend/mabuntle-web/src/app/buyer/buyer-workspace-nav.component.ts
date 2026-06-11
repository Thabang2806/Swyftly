import { DOCUMENT } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, ViewEncapsulation, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ensureLazyStylesheet } from '../shared/ui/lazy-stylesheet';

type BuyerWorkspaceNavItem = {
  label: string;
  route: string;
  exact: boolean;
};

type BuyerWorkspaceNavGroup = {
  label: string;
  items: readonly BuyerWorkspaceNavItem[];
};

@Component({
  selector: 'app-buyer-workspace-nav',
  encapsulation: ViewEncapsulation.None,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="buyer-workspace-nav" aria-label="Buyer account navigation">
      <div class="buyer-workspace-nav__brand">
        <span>Account</span>
        <strong>Buyer workspace</strong>
      </div>

      @for (group of groups; track group.label) {
        <section class="buyer-workspace-nav__group">
          <span>{{ group.label }}</span>
          @for (item of group.items; track item.route) {
            <a
              [routerLink]="item.route"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: item.exact }"
              ariaCurrentWhenActive="page"
            >
              {{ item.label }}
            </a>
          }
        </section>
      }
    </nav>
  `
})
export class BuyerWorkspaceNavComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);

  protected readonly groups: readonly BuyerWorkspaceNavGroup[] = [
    {
      label: 'Activity',
      items: [
        { label: 'Dashboard', route: '/account', exact: true },
        { label: 'Orders', route: '/account/orders', exact: false },
        { label: 'Returns', route: '/account/returns', exact: false },
        { label: 'Refunds', route: '/account/refunds', exact: false }
      ]
    },
    {
      label: 'Saved',
      items: [
        { label: 'Wishlist', route: '/account/wishlist', exact: false },
        { label: 'Reviews', route: '/account/reviews', exact: false },
        { label: 'Notifications', route: '/account/notifications', exact: false },
        { label: 'AI history', route: '/account/ai-history', exact: false },
        { label: 'Settings', route: '/account/settings', exact: false }
      ]
    },
    {
      label: 'Help',
      items: [
        { label: 'Disputes', route: '/account/disputes', exact: false },
        { label: 'Support', route: '/account/support', exact: false }
      ]
    }
  ];

  ngOnInit(): void {
    ensureLazyStylesheet(this.document, this.platformId, 'luxury-buyer', '/styles/luxury-buyer.css');
  }
}
