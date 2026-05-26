import { DOCUMENT } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, ViewEncapsulation, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ensureLazyStylesheet } from '../shared/ui/lazy-stylesheet';

type SellerWorkspaceNavItem = {
  label: string;
  route: string;
  exact: boolean;
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
        <strong>Swyftly</strong>
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

  protected readonly groups: readonly SellerWorkspaceNavGroup[] = [
    {
      label: 'Overview',
      items: [
        { label: 'Dashboard', route: '/seller', exact: true },
        { label: 'Analytics', route: '/seller/analytics', exact: false }
      ]
    },
    {
      label: 'Catalog',
      items: [
        { label: 'Products', route: '/seller/products', exact: false },
        { label: 'Inventory', route: '/seller/inventory', exact: false },
        { label: 'Store settings', route: '/seller/settings/store', exact: false }
      ]
    },
    {
      label: 'Operations',
      items: [
        { label: 'Orders', route: '/seller/orders', exact: false },
        { label: 'Returns', route: '/seller/returns', exact: false },
        { label: 'Support', route: '/seller/support', exact: false }
      ]
    },
    {
      label: 'Growth and finance',
      items: [
        { label: 'Ads', route: '/seller/ads', exact: false },
        { label: 'Payouts', route: '/seller/payouts', exact: false }
      ]
    }
  ];

  ngOnInit(): void {
    ensureLazyStylesheet(this.document, this.platformId, 'luxury-seller', '/styles/luxury-seller.css');
  }
}
