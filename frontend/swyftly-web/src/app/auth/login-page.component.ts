import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { getApiErrorMessage } from './api-error';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
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
        <p>Access your Swyftly account.</p>

        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        <mat-form-field class="swyftly-field" appearance="outline" hideRequiredMarker>
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="email" />
          @if (form.controls.email.hasError('required')) {
            <mat-error>Email is required.</mat-error>
          } @else if (form.controls.email.hasError('email')) {
            <mat-error>Enter a valid email address.</mat-error>
          }
        </mat-form-field>

        <mat-form-field class="swyftly-field" appearance="outline" hideRequiredMarker>
          <mat-label>Password</mat-label>
          <input matInput type="password" formControlName="password" autocomplete="current-password" />
          @if (form.controls.password.hasError('required')) {
            <mat-error>Password is required.</mat-error>
          }
        </mat-form-field>

        <button mat-flat-button type="submit" [disabled]="isSubmitting()">
          {{ isSubmitting() ? 'Signing in...' : 'Sign in' }}
        </button>

        <div class="auth-secondary">
          <a routerLink="/register/buyer">Create buyer account</a>
          <a routerLink="/register/seller">Create seller account</a>
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
      await this.router.navigateByUrl(this.resolveReturnUrl(response.roles));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSubmitting.set(false);
    }
  }

  private resolveReturnUrl(roles: readonly string[]): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//')) {
      return returnUrl;
    }

    if (roles.includes('Seller')) {
      return '/seller';
    }

    if (roles.includes('Admin') || roles.includes('SuperAdmin')) {
      return '/admin';
    }

    return '/account';
  }
}
