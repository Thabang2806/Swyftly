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
import { FRONTEND_HOSTS } from '../frontend-experience';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { getApiErrorMessage } from './api-error';
import { AuthService } from './auth.service';

type PublicRegistrationRole = 'Buyer' | 'Seller';

@Component({
  selector: 'app-register-page',
  imports: [
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
        @if (role === 'Seller') {
          <a class="auth-context-link" [href]="sellGuideUrl">Review seller requirements before applying</a>
        }

        @if (successMessage()) {
          <p class="auth-alert success" role="status">{{ successMessage() }}</p>
          <a class="ui-button ui-button--primary" routerLink="/login">Sign in</a>
        } @else {
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
            <input type="password" formControlName="password" autocomplete="new-password" />
            @if (form.controls.password.hasError('required')) {
              <small class="ui-field-error">Password is required.</small>
            } @else if (form.controls.password.hasError('minlength')) {
              <small class="ui-field-error">Use at least 8 characters.</small>
            } @else if (form.controls.password.hasError('pattern')) {
              <small class="ui-field-error">Use uppercase, lowercase, and a number.</small>
            }
          </label>

          <label class="ui-field">
            <span>Confirm password</span>
            <input type="password" formControlName="confirmPassword" autocomplete="new-password" />
            @if (form.controls.confirmPassword.hasError('required')) {
              <small class="ui-field-error">Confirm your password.</small>
            } @else if (form.hasError('passwordMismatch')) {
              <small class="ui-field-error">Passwords must match.</small>
            }
          </label>

          <button class="ui-button ui-button--primary" type="submit" [disabled]="isSubmitting()">
            {{ isSubmitting() ? 'Creating account...' : 'Create account' }}
          </button>

          <div class="auth-secondary">
            <a routerLink="/login">Already have an account?</a>
            <a [href]="alternateRoute()">{{ alternateLabel() }}</a>
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
  protected readonly sellGuideUrl = `${FRONTEND_HOSTS.client}/sell`;

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
      ? 'Create your seller account, then complete onboarding so Mabuntle can review your storefront before products and ads go live.'
      : 'Save products, manage orders, track returns, and keep your marketplace preferences in one place.';
  }

  protected alternateRoute(): string {
    return this.role === 'Seller'
      ? `${FRONTEND_HOSTS.client}/register/buyer`
      : `${FRONTEND_HOSTS.seller}/register/seller`;
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
