import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-admin-workspace-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="admin-workspace-nav" aria-label="Admin workspace">
      <div class="admin-workspace-nav-brand">
        <span>S</span>
        <strong>Swyftly</strong>
        <small>Admin console</small>
      </div>

      @for (item of items; track item.route) {
        <a
          [routerLink]="item.route"
          routerLinkActive="active"
          [routerLinkActiveOptions]="item.exact ? { exact: true } : { exact: false }"
        >
          {{ item.label }}
        </a>
      }
    </nav>
  `
})
export class AdminWorkspaceNavComponent {
  protected readonly items = [
    { label: 'Dashboard', route: '/admin', exact: true },
    { label: 'Sellers', route: '/admin/sellers' },
    { label: 'Products', route: '/admin/products' },
    { label: 'Reviews', route: '/admin/reviews' },
    { label: 'Support', route: '/admin/support' },
    { label: 'Categories', route: '/admin/categories' },
    { label: 'Orders', route: '/admin/orders' },
    { label: 'Payments', route: '/admin/payments' },
    { label: 'Refunds', route: '/admin/refunds' },
    { label: 'Payouts', route: '/admin/payouts' },
    { label: 'Disputes', route: '/admin/disputes' },
    { label: 'Reports', route: '/admin/reports' },
    { label: 'AI usage', route: '/admin/ai-usage' },
    { label: 'Ads', route: '/admin/ads' },
    { label: 'Audit logs', route: '/admin/audit-logs' }
  ];
}
