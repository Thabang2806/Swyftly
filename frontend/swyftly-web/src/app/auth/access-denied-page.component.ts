import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';

@Component({
  selector: 'app-access-denied-page',
  imports: [LuxuryPublicStylesComponent, MatButtonModule, RouterLink],
  template: `
    <app-luxury-public-styles />
    <section class="page auth-page">
      <div class="auth-panel">
        <span class="eyebrow">Access denied</span>
        <h1>Access denied</h1>
        <p>Your account does not have access to this area.</p>
        <div class="auth-actions">
          <a mat-flat-button routerLink="/shop">Go to shop</a>
          <a mat-button routerLink="/login">Use another account</a>
        </div>
      </div>
    </section>
  `
})
export class AccessDeniedPageComponent {}
