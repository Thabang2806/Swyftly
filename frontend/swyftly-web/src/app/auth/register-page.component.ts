import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { getApiErrorMessage } from './api-error';
import { AuthService } from './auth.service';

type PublicRegistrationRole = 'Buyer' | 'Seller';

@Component({
  selector: 'app-register-page',
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
        <span class="eyebrow">{{ roleLabel() }}</span>
        <h1>{{ heading() }}</h1>
        <p>{{ summary() }}</p>

        @if (successMessage()) {
          <p class="auth-alert success" role="status">{{ successMessage() }}</p>
          <a mat-flat-button routerLink="/login">Sign in</a>
        } @else {
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
            <input matInput type="password" formControlName="password" autocomplete="new-password" />
            @if (form.controls.password.hasError('required')) {
              <mat-error>Password is required.</mat-error>
            } @else if (form.controls.password.hasError('minlength')) {
              <mat-error>Use at least 8 characters.</mat-error>
            } @else if (form.controls.password.hasError('pattern')) {
              <mat-error>Use uppercase, lowercase, and a number.</mat-error>
            }
          </mat-form-field>

          <mat-form-field class="swyftly-field" appearance="outline" hideRequiredMarker>
            <mat-label>Confirm password</mat-label>
            <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
            @if (form.controls.confirmPassword.hasError('required')) {
              <mat-error>Confirm your password.</mat-error>
            } @else if (form.hasError('passwordMismatch')) {
              <mat-error>Passwords must match.</mat-error>
            }
          </mat-form-field>

          <button mat-flat-button type="submit" [disabled]="isSubmitting()">
            {{ isSubmitting() ? 'Creating account...' : 'Create account' }}
          </button>

          <div class="auth-secondary">
            <a routerLink="/login">Already have an account?</a>
            <a [routerLink]="alternateRoute()">{{ alternateLabel() }}</a>
          </div>
        }
      </form>
    </section>
  `
})
export class RegisterPageComponent {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);

  protected readonly role = this.route.snapshot.data['role'] as PublicRegistrationRole;
  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [
      Validators.required,
      Validators.minLength(8),
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/)
    ]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: [passwordsMatchValidator()] });

  protected roleLabel(): string {
    return this.role === 'Seller' ? 'Seller' : 'Buyer';
  }

  protected heading(): string {
    return this.role === 'Seller' ? 'Create seller account' : 'Create buyer account';
  }

  protected summary(): string {
    return this.role === 'Seller'
      ? 'Start seller onboarding, prepare your storefront, and submit listings for marketplace review.'
      : 'Save products, manage orders, track returns, and keep your marketplace preferences in one place.';
  }

  protected alternateRoute(): string {
    return this.role === 'Seller' ? '/register/buyer' : '/register/seller';
  }

  protected alternateLabel(): string {
    return this.role === 'Seller' ? 'Create buyer account' : 'Create seller account';
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid || this.isSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const value = this.form.getRawValue();

    try {
      await this.authService.register({
        email: value.email,
        password: value.password,
        role: this.role
      });
      this.successMessage.set(`${this.roleLabel()} account created. You can sign in now.`);
      this.form.reset();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSubmitting.set(false);
    }
  }
}

function passwordsMatchValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const password = control.get('password')?.value;
    const confirmPassword = control.get('confirmPassword')?.value;

    return password && confirmPassword && password !== confirmPassword
      ? { passwordMismatch: true }
      : null;
  };
}
