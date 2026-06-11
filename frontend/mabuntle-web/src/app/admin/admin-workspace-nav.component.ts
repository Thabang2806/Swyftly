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
        <strong>Mabuntle</strong>
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
        { label: 'Dashboard', route: '/', exact: true }
      ]
    },
    {
      label: 'Moderation',
      items: [
        { label: 'Sellers', route: '/sellers' },
        { label: 'Products', route: '/products' },
        { label: 'Reviews', route: '/reviews' },
        { label: 'Ads', route: '/ads' }
      ]
    },
    {
      label: 'Operations',
      items: [
        { label: 'Orders', route: '/orders' },
        { label: 'Support', route: '/support' },
        { label: 'Disputes', route: '/disputes' }
      ]
    },
    {
      label: 'Finance',
      items: [
        { label: 'Payments', route: '/payments' },
        { label: 'Refunds', route: '/refunds' },
        { label: 'Payouts', route: '/payouts' },
        { label: 'Payout profile', route: '/payout-profile-changes' }
      ]
    },
    {
      label: 'Platform',
      items: [
        { label: 'Categories', route: '/categories' },
        { label: 'Pickup points', route: '/pickup-points' },
        { label: 'Reports', route: '/reports' },
        { label: 'AI usage', route: '/ai-usage' },
        { label: 'Audit logs', route: '/audit-logs' }
      ]
    }
  ];

  ngOnInit(): void {
    ensureLazyStylesheet(this.document, this.platformId, 'luxury-admin', '/styles/luxury-admin.css');
  }
}
