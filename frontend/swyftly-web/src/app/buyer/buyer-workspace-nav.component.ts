import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

type BuyerWorkspaceNavItem = {
  label: string;
  route: string;
  exact: boolean;
};

@Component({
  selector: 'app-buyer-workspace-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="buyer-workspace-nav" aria-label="Buyer account navigation">
      @for (item of items; track item.route) {
        <a
          [routerLink]="item.route"
          routerLinkActive="active"
          [routerLinkActiveOptions]="{ exact: item.exact }"
          ariaCurrentWhenActive="page"
        >
          {{ item.label }}
        </a>
      }
    </nav>
  `
})
export class BuyerWorkspaceNavComponent {
  protected readonly items: readonly BuyerWorkspaceNavItem[] = [
    { label: 'Dashboard', route: '/account', exact: true },
    { label: 'Orders', route: '/account/orders', exact: false },
    { label: 'Returns', route: '/account/returns', exact: false },
    { label: 'Wishlist', route: '/account/wishlist', exact: false },
    { label: 'Reviews', route: '/account/reviews', exact: false },
    { label: 'Notifications', route: '/account/notifications', exact: false },
    { label: 'Settings', route: '/account/settings', exact: false },
    { label: 'Disputes', route: '/account/disputes', exact: false },
    { label: 'Support', route: '/account/support', exact: false }
  ];
}
