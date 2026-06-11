import { Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { MobileBottomNavComponent, MobileBottomNavItem } from './shared/ui/mobile-bottom-nav.component';

@Component({
  selector: 'app-admin-root',
  imports: [MobileBottomNavComponent, RouterOutlet],
  template: `
    <main class="subdomain-app-main">
      <router-outlet />
    </main>

    <app-mobile-bottom-nav [items]="mobileNavigationItems()" />
  `
})
export class AdminAppComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly mobileNavigationItems = computed<MobileBottomNavItem[]>(() => {
    if (!this.authService.hasAnyRole(['Admin', 'SuperAdmin', 'FinanceOperator', 'FinanceApprover', 'SupportAgent'])) {
      return [
        { label: 'Home', route: '/' },
        { label: 'Sign in', route: '/login' }
      ];
    }

    return [
      { label: 'Home', route: '/' },
      { label: 'Queues', route: '/products' },
      { label: 'Support', route: '/support' },
      { label: 'Finance', route: '/payouts' },
      { label: 'Reports', route: '/reports' }
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

