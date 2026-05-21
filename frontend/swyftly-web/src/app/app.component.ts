import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { MobileBottomNavComponent, MobileBottomNavItem } from './shared/ui/mobile-bottom-nav.component';

type NavigationItem = {
  label: string;
  route: string;
};

@Component({
  selector: 'app-root',
  imports: [MobileBottomNavComponent, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly publicNavigationItems: NavigationItem[] = [
    { label: 'Shop', route: '/shop' }
  ];

  protected readonly authNavigationItems = computed<NavigationItem[]>(() => {
    if (!this.authService.isAuthenticated()) {
      return [
        { label: 'Sign in', route: '/login' },
        { label: 'Join', route: '/register/buyer' },
        { label: 'Sell', route: '/register/seller' }
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
      { label: 'Account', route: '/account' }
    ];
  });

  protected readonly sellerNavigationItems = computed<NavigationItem[]>(() => {
    if (!this.authService.hasAnyRole(['Seller'])) {
      return [];
    }

    return [
      { label: 'Seller', route: '/seller' }
    ];
  });

  protected readonly adminNavigationItems = computed<NavigationItem[]>(() => {
    const items: NavigationItem[] = [];

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin'])) {
      items.push({ label: 'Admin', route: '/admin' });
      items.push({ label: 'Categories', route: '/admin/categories' });
    }

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin', 'SupportAgent'])) {
      items.push({ label: 'Support', route: '/admin/support' });
    }

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin', 'FinanceOperator', 'FinanceApprover'])) {
      items.push({ label: 'Refunds', route: '/admin/refunds' });
      items.push({ label: 'Payouts', route: '/admin/payouts' });
    }

    return items;
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

    if (this.authService.hasAnyRole(['Admin', 'SuperAdmin', 'FinanceOperator', 'FinanceApprover', 'SupportAgent'])) {
      return [
        ...items,
        { label: 'Admin', route: '/admin' },
        { label: 'Queues', route: '/admin/products' },
        { label: 'Finance', route: '/admin/payouts' }
      ];
    }

    if (this.authService.hasAnyRole(['Seller'])) {
      return [
        ...items,
        { label: 'Seller', route: '/seller' },
        { label: 'Orders', route: '/seller/orders' },
        { label: 'Payouts', route: '/seller/payouts' }
      ];
    }

    if (this.authService.hasAnyRole(['Buyer'])) {
      return [
        ...items,
        { label: 'AI', route: '/assistant' },
        { label: 'Orders', route: '/account/orders' },
        { label: 'Me', route: '/account' }
      ];
    }

    return [
      ...items,
      { label: 'Sell', route: '/register/seller' },
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
}
