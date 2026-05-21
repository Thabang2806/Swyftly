import { Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

export interface MobileBottomNavItem {
  label: string;
  route: string;
}

@Component({
  selector: 'app-mobile-bottom-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="hf-mobile-bottom-nav" aria-label="Mobile quick navigation">
      @for (item of items(); track item.route) {
        <a
          [routerLink]="item.route"
          routerLinkActive="active"
          [routerLinkActiveOptions]="{ exact: item.route === '/' }"
          ariaCurrentWhenActive="page"
        >
          <span class="hf-mobile-bottom-nav-dot" aria-hidden="true"></span>
          <span>{{ item.label }}</span>
        </a>
      }
    </nav>
  `
})
export class MobileBottomNavComponent {
  readonly items = input.required<MobileBottomNavItem[]>();
}
