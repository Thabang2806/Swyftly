import { DOCUMENT } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, ViewEncapsulation, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ensureLazyStylesheet } from '../shared/ui/lazy-stylesheet';

type AdminWorkspaceNavItem = {
  label: string;
  route: string;
  exact?: boolean;
};

type AdminWorkspaceNavGroup = {
  label: string;
  items: readonly AdminWorkspaceNavItem[];
};

@Component({
  selector: 'app-admin-workspace-nav',
  encapsulation: ViewEncapsulation.None,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="admin-workspace-nav" aria-label="Admin workspace">
      <div class="admin-workspace-nav-brand">
        <span>S</span>
        <strong>Swyftly</strong>
        <small>Admin console</small>
      </div>

      @for (group of groups; track group.label) {
        <section class="workspace-nav-group" [attr.aria-label]="group.label">
          <span class="workspace-nav-group-label">{{ group.label }}</span>

          @for (item of group.items; track item.route) {
            <a
              [routerLink]="item.route"
              routerLinkActive="active"
              [routerLinkActiveOptions]="item.exact ? { exact: true } : { exact: false }"
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
export class AdminWorkspaceNavComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);

  protected readonly groups: readonly AdminWorkspaceNavGroup[] = [
    {
      label: 'Overview',
      items: [
        { label: 'Dashboard', route: '/admin', exact: true }
      ]
    },
    {
      label: 'Moderation',
      items: [
        { label: 'Sellers', route: '/admin/sellers' },
        { label: 'Products', route: '/admin/products' },
        { label: 'Reviews', route: '/admin/reviews' },
        { label: 'Ads', route: '/admin/ads' }
      ]
    },
    {
      label: 'Operations',
      items: [
        { label: 'Orders', route: '/admin/orders' },
        { label: 'Support', route: '/admin/support' },
        { label: 'Disputes', route: '/admin/disputes' }
      ]
    },
    {
      label: 'Finance',
      items: [
        { label: 'Payments', route: '/admin/payments' },
        { label: 'Refunds', route: '/admin/refunds' },
        { label: 'Payouts', route: '/admin/payouts' },
        { label: 'Payout profile', route: '/admin/payout-profile-changes' }
      ]
    },
    {
      label: 'Platform',
      items: [
        { label: 'Categories', route: '/admin/categories' },
        { label: 'Pickup points', route: '/admin/pickup-points' },
        { label: 'Reports', route: '/admin/reports' },
        { label: 'AI usage', route: '/admin/ai-usage' },
        { label: 'Audit logs', route: '/admin/audit-logs' }
      ]
    }
  ];

  ngOnInit(): void {
    ensureLazyStylesheet(this.document, this.platformId, 'luxury-admin', '/styles/luxury-admin.css');
  }
}
