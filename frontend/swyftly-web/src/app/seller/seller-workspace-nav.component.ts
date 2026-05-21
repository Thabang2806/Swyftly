import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

type SellerWorkspaceNavItem = {
  label: string;
  route: string;
  exact: boolean;
};

@Component({
  selector: 'app-seller-workspace-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="seller-workspace-nav" aria-label="Seller workspace navigation">
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
export class SellerWorkspaceNavComponent {
  protected readonly items: readonly SellerWorkspaceNavItem[] = [
    { label: 'Dashboard', route: '/seller', exact: true },
    { label: 'Products', route: '/seller/products', exact: false },
    { label: 'Inventory', route: '/seller/inventory', exact: false },
    { label: 'Orders', route: '/seller/orders', exact: false },
    { label: 'Returns', route: '/seller/returns', exact: false },
    { label: 'Payouts', route: '/seller/payouts', exact: false },
    { label: 'Support', route: '/seller/support', exact: false },
    { label: 'Ads', route: '/seller/ads', exact: false },
    { label: 'Analytics', route: '/seller/analytics', exact: false },
    { label: 'Store settings', route: '/seller/settings/store', exact: false }
  ];
}
