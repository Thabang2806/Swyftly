import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FRONTEND_EXPERIENCE, FRONTEND_HOSTS } from '../frontend-experience';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { getApiErrorMessage } from './api-error';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login-page',
  imports: [
    LuxuryPublicStylesComponent,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <app-luxury-public-styles />
    <section class="page auth-page">
      <form class="auth-panel" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <span class="eyebrow">Account</span>
        <h1>Sign in</h1>
        <p>Access your Mabuntle account.</p>

        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        <label class="ui-field">
          <span>Email</span>
          <input type="email" formControlName="email" autocomplete="email" />
          @if (form.controls.email.hasError('required')) {
            <small class="ui-field-error">Email is required.</small>
          } @else if (form.controls.email.hasError('email')) {
            <small class="ui-field-error">Enter a valid email address.</small>
          }
        </label>

        <label class="ui-field">
          <span>Password</span>
          <input type="password" formControlName="password" autocomplete="current-password" />
          @if (form.controls.password.hasError('required')) {
            <small class="ui-field-error">Password is required.</small>
          }
        </label>

        <button class="ui-button ui-button--primary" type="submit" [disabled]="isSubmitting()">
          {{ isSubmitting() ? 'Signing in...' : 'Sign in' }}
        </button>

        <div class="auth-secondary">
          <a routerLink="/register/buyer">Create buyer account</a>
          <a [href]="sellerRegisterUrl">Create seller account</a>
        </div>
      </form>
    </section>
  `
})
export class LoginPageComponent {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly experience = inject(FRONTEND_EXPERIENCE);

  protected readonly sellerRegisterUrl = `${FRONTEND_HOSTS.seller}/register/seller`;
  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  protected async submit(): Promise<void> {
    if (this.form.invalid || this.isSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    try {
      const response = await this.authService.login(this.form.getRawValue());
      await this.navigateAfterLogin(response.roles);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSubmitting.set(false);
    }
  }

  private async navigateAfterLogin(roles: readonly string[]): Promise<void> {
    const target = this.resolveReturnUrl(roles);
    if (target.startsWith('https://')) {
      window.location.assign(target);
      return;
    }

    await this.router.navigateByUrl(target);
  }

  private resolveReturnUrl(roles: readonly string[]): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//')) {
      return returnUrl;
    }

    if (roles.includes('Seller')) {
      return this.experience === 'seller' ? '/' : FRONTEND_HOSTS.seller;
    }

    if (roles.includes('Admin') || roles.includes('SuperAdmin')) {
      return this.experience === 'admin' ? '/' : FRONTEND_HOSTS.admin;
    }

    return this.experience === 'client' ? '/account' : FRONTEND_HOSTS.client;
  }
}
