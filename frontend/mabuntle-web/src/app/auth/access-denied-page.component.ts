import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FRONTEND_HOSTS } from '../frontend-experience';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';

@Component({
  selector: 'app-access-denied-page',
  imports: [LuxuryPublicStylesComponent, RouterLink],
  template: `
    <app-luxury-public-styles />
    <section class="page auth-page">
      <div class="auth-panel">
        <span class="eyebrow">Access denied</span>
        <h1>Access denied</h1>
        <p>Your account does not have access to this area.</p>
        <div class="auth-actions">
          <a class="ui-button ui-button--primary" [href]="shopUrl">Go to shop</a>
          <a class="ui-button ui-button--ghost" routerLink="/login">Use another account</a>
        </div>
      </div>
    </section>
  `
})
export class AccessDeniedPageComponent {
  protected readonly shopUrl = `${FRONTEND_HOSTS.client}/shop`;
}
