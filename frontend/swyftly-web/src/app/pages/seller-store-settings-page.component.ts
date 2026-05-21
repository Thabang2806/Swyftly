import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import {
  SellerDeliveryMethodRequest,
  SellerDeliveryMethodResponse,
  SellerDeliveryMethodType
} from '../seller/seller-delivery-method.models';
import { SellerDeliveryMethodService } from '../seller/seller-delivery-method.service';
import {
  SellerOnboardingResponse,
  UpdateSellerAddressRequest,
  UpdateSellerProfileRequest,
  UpdateSellerStorefrontRequest
} from '../seller/seller-onboarding.models';
import { SellerOnboardingService } from '../seller/seller-onboarding.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-store-settings-page',
  imports: [
    CurrencyPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page seller-store-settings-page">
      <app-seller-workspace-nav />

      <app-page-header
        eyebrow="Seller settings"
        heading="Store settings"
        description="Maintain the public store profile and fulfilment address used by your seller workspace."
      >
        @if (storefrontPreviewUrl(); as previewUrl) {
          <a mat-stroked-button pageHeaderActions [routerLink]="previewUrl">View storefront</a>
        }
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading store settings...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <section class="seller-settings-status route-card">
          <div>
            <span class="eyebrow">Current store state</span>
            <h2>{{ storeName() }}</h2>
            <p>Public storefront visibility still depends on verified seller status and a published storefront.</p>
          </div>
          <app-status-badge [label]="onboarding()?.verificationStatus ?? 'Unknown'" [tone]="verificationTone()" />
          <app-status-badge
            [label]="onboarding()?.storefront?.isPublished ? 'Storefront published' : 'Storefront draft'"
            [tone]="onboarding()?.storefront?.isPublished ? 'success' : 'warning'"
          />
        </section>

        <div class="seller-settings-grid">
          <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" class="route-card wizard-form" novalidate>
            <span class="eyebrow">Seller profile</span>
            <h2>Business identity</h2>

            <mat-form-field appearance="outline">
              <mat-label>Display name</mat-label>
              <input matInput formControlName="displayName" />
              @if (profileForm.controls.displayName.hasError('required')) {
                <mat-error>Display name is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Contact email</mat-label>
              <input matInput type="email" formControlName="contactEmail" />
              @if (profileForm.controls.contactEmail.hasError('required')) {
                <mat-error>Contact email is required.</mat-error>
              } @else if (profileForm.controls.contactEmail.hasError('email')) {
                <mat-error>Enter a valid email address.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Phone number</mat-label>
              <input matInput formControlName="phoneNumber" />
              @if (profileForm.controls.phoneNumber.hasError('required')) {
                <mat-error>Phone number is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Business type</mat-label>
              <mat-select formControlName="businessType">
                <mat-option value="Individual">Individual</mat-option>
                <mat-option value="RegisteredBusiness">Registered business</mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Business name</mat-label>
              <input matInput formControlName="businessName" />
              @if (profileForm.hasError('businessNameRequired')) {
                <mat-error>Business name is required for registered businesses.</mat-error>
              }
            </mat-form-field>

            <button mat-flat-button type="submit" [disabled]="isSaving()">Save profile</button>
          </form>

          <form [formGroup]="storefrontForm" (ngSubmit)="saveStorefront()" class="route-card wizard-form" novalidate>
            <span class="eyebrow">Public storefront</span>
            <h2>Brand presentation</h2>

            <mat-form-field appearance="outline">
              <mat-label>Store name</mat-label>
              <input matInput formControlName="storeName" />
              @if (storefrontForm.controls.storeName.hasError('required')) {
                <mat-error>Store name is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Store slug</mat-label>
              <input matInput formControlName="slug" />
              @if (storefrontForm.controls.slug.hasError('required')) {
                <mat-error>Store slug is required.</mat-error>
              } @else if (storefrontForm.controls.slug.hasError('pattern')) {
                <mat-error>Use lowercase letters, numbers, and hyphens.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Description</mat-label>
              <textarea matInput rows="4" formControlName="description"></textarea>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Logo URL</mat-label>
              <input matInput formControlName="logoUrl" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Banner URL</mat-label>
              <input matInput formControlName="bannerUrl" />
            </mat-form-field>

            <button mat-flat-button type="submit" [disabled]="isSaving()">Save storefront</button>
          </form>

          <form [formGroup]="addressForm" (ngSubmit)="saveAddress()" class="route-card wizard-form" novalidate>
            <span class="eyebrow">Fulfilment</span>
            <h2>Dispatch address</h2>

            <mat-form-field appearance="outline">
              <mat-label>Address line 1</mat-label>
              <input matInput formControlName="addressLine1" />
              @if (addressForm.controls.addressLine1.hasError('required')) {
                <mat-error>Address line 1 is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Address line 2</mat-label>
              <input matInput formControlName="addressLine2" />
            </mat-form-field>

            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>City</mat-label>
                <input matInput formControlName="city" />
                @if (addressForm.controls.city.hasError('required')) {
                  <mat-error>City is required.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Province</mat-label>
                <input matInput formControlName="province" />
                @if (addressForm.controls.province.hasError('required')) {
                  <mat-error>Province is required.</mat-error>
                }
              </mat-form-field>
            </div>

            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Postal code</mat-label>
                <input matInput formControlName="postalCode" />
                @if (addressForm.controls.postalCode.hasError('required')) {
                  <mat-error>Postal code is required.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Country code</mat-label>
                <input matInput maxlength="2" formControlName="countryCode" />
                @if (addressForm.controls.countryCode.hasError('required')) {
                  <mat-error>Country code is required.</mat-error>
                } @else if (addressForm.controls.countryCode.hasError('pattern')) {
                  <mat-error>Use a 2-letter country code.</mat-error>
                }
              </mat-form-field>
            </div>

            <button mat-flat-button type="submit" [disabled]="isSaving()">Save address</button>
          </form>

          <section class="route-card wizard-form seller-delivery-methods">
            <span class="eyebrow">Delivery rates</span>
            <h2>Seller delivery methods</h2>
            <p>Set provider-free delivery options buyers can choose during checkout. Keep methods inactive until the rate is ready to use.</p>

            @if (deliveryMethods().length > 0) {
              <div class="seller-delivery-method-list">
                @for (method of deliveryMethods(); track method.deliveryMethodId) {
                  <article class="seller-delivery-method-row">
                    <div>
                      <strong>{{ method.name }}</strong>
                      <span>{{ method.methodType }} - {{ method.countryCode }}{{ method.province ? ' / ' + method.province : '' }}</span>
                      <small>
                        {{ method.basePrice | currency:'ZAR':'symbol-narrow' }}
                        @if (method.freeShippingThreshold) {
                          - free from {{ method.freeShippingThreshold | currency:'ZAR':'symbol-narrow' }}
                        }
                        - {{ method.estimatedMinDays }}-{{ method.estimatedMaxDays }} days
                      </small>
                    </div>
                    <app-status-badge [label]="method.isActive ? 'Active' : 'Inactive'" [tone]="method.isActive ? 'success' : 'warning'" />
                    <div class="seller-delivery-method-actions">
                      <button mat-stroked-button type="button" (click)="editDeliveryMethod(method)">Edit</button>
                      @if (method.isActive) {
                        <button mat-stroked-button type="button" (click)="setDeliveryMethodActive(method, false)" [disabled]="isSaving()">Deactivate</button>
                      } @else {
                        <button mat-stroked-button type="button" (click)="setDeliveryMethodActive(method, true)" [disabled]="isSaving()">Activate</button>
                      }
                    </div>
                  </article>
                }
              </div>
            } @else {
              <app-ui-alert tone="warning">No delivery methods yet. Buyers cannot complete checkout for your products until at least one active method matches their address.</app-ui-alert>
            }

            <form [formGroup]="deliveryMethodForm" (ngSubmit)="saveDeliveryMethod()" class="seller-delivery-method-form" novalidate>
              <h3>{{ editingDeliveryMethodId() ? 'Edit delivery method' : 'Add delivery method' }}</h3>

              <mat-form-field appearance="outline">
                <mat-label>Name</mat-label>
                <input matInput formControlName="name" />
                @if (deliveryMethodForm.controls.name.hasError('required')) {
                  <mat-error>Name is required.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Description</mat-label>
                <textarea matInput rows="3" formControlName="description"></textarea>
              </mat-form-field>

              <div class="form-grid">
                <mat-form-field appearance="outline">
                  <mat-label>Method type</mat-label>
                  <mat-select formControlName="methodType">
                    <mat-option value="Standard">Standard</mat-option>
                    <mat-option value="Express">Express</mat-option>
                    <mat-option value="LocalCourier">Local courier</mat-option>
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Status</mat-label>
                  <mat-select formControlName="isActive">
                    <mat-option [value]="true">Active</mat-option>
                    <mat-option [value]="false">Inactive</mat-option>
                  </mat-select>
                </mat-form-field>
              </div>

              <div class="form-grid">
                <mat-form-field appearance="outline">
                  <mat-label>Country code</mat-label>
                  <input matInput maxlength="2" formControlName="countryCode" />
                  @if (deliveryMethodForm.controls.countryCode.invalid) {
                    <mat-error>Use a two-letter country code.</mat-error>
                  }
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Province</mat-label>
                  <input matInput formControlName="province" />
                </mat-form-field>
              </div>

              <div class="form-grid">
                <mat-form-field appearance="outline">
                  <mat-label>Base price</mat-label>
                  <input matInput type="number" min="0" formControlName="basePrice" />
                  @if (deliveryMethodForm.controls.basePrice.invalid) {
                    <mat-error>Base price cannot be negative.</mat-error>
                  }
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Free shipping threshold</mat-label>
                  <input matInput type="number" min="0" formControlName="freeShippingThreshold" />
                </mat-form-field>
              </div>

              <div class="form-grid">
                <mat-form-field appearance="outline">
                  <mat-label>Estimated min days</mat-label>
                  <input matInput type="number" min="0" formControlName="estimatedMinDays" />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Estimated max days</mat-label>
                  <input matInput type="number" min="0" formControlName="estimatedMaxDays" />
                  @if (deliveryMethodForm.hasError('invalidDayRange')) {
                    <mat-error>Max days must be at least min days.</mat-error>
                  }
                </mat-form-field>
              </div>

              <mat-form-field appearance="outline">
                <mat-label>Display order</mat-label>
                <input matInput type="number" min="0" formControlName="displayOrder" />
              </mat-form-field>

              <div class="form-actions">
                <button mat-flat-button type="submit" [disabled]="isSaving()">Save delivery method</button>
                @if (editingDeliveryMethodId()) {
                  <button mat-stroked-button type="button" (click)="cancelDeliveryMethodEdit()">Cancel edit</button>
                }
              </div>
            </form>
          </section>

          <aside class="route-card seller-settings-readonly">
            <span class="eyebrow">Payout security</span>
            <h2>Payout details are read-only here</h2>
            <p>Changing payout provider or bank information after verification needs re-approval and stronger security controls, so it stays outside this seller settings pass.</p>
            <app-status-badge
              [label]="onboarding()?.payout?.isAdminApproved ? 'Payout approved' : 'Payout review required'"
              [tone]="onboarding()?.payout?.isAdminApproved ? 'success' : 'warning'"
            />
          </aside>
        </div>
      }
    </section>
  `
})
export class SellerStoreSettingsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly deliveryMethodService = inject(SellerDeliveryMethodService);
  private readonly onboardingService = inject(SellerOnboardingService);

  protected readonly onboarding = signal<SellerOnboardingResponse | null>(null);
  protected readonly deliveryMethods = signal<SellerDeliveryMethodResponse[]>([]);
  protected readonly editingDeliveryMethodId = signal<string | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly storeName = computed(() =>
    this.onboarding()?.storefront?.storeName
      ?? this.onboarding()?.profile.displayName
      ?? 'Seller store');

  protected readonly storefrontPreviewUrl = computed(() => {
    const slug = this.onboarding()?.storefront?.slug;
    return slug ? `/seller/${slug}` : null;
  });

  protected readonly profileForm = this.formBuilder.group({
    displayName: ['', [Validators.required]],
    contactEmail: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required]],
    businessType: ['Individual' as 'Individual' | 'RegisteredBusiness', [Validators.required]],
    businessName: ['']
  }, { validators: [businessNameRequiredValidator()] });

  protected readonly storefrontForm = this.formBuilder.group({
    storeName: ['', [Validators.required]],
    slug: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
    description: [''],
    logoUrl: [''],
    bannerUrl: ['']
  });

  protected readonly addressForm = this.formBuilder.group({
    addressLine1: ['', [Validators.required]],
    addressLine2: [''],
    city: ['', [Validators.required]],
    province: ['', [Validators.required]],
    postalCode: ['', [Validators.required]],
    countryCode: ['ZA', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]]
  });

  protected readonly deliveryMethodForm = this.formBuilder.group({
    name: ['', [Validators.required]],
    description: [''],
    methodType: ['Standard' as SellerDeliveryMethodType, [Validators.required]],
    countryCode: ['ZA', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]],
    province: [''],
    basePrice: [0, [Validators.required, Validators.min(0)]],
    freeShippingThreshold: [null as number | null],
    estimatedMinDays: [2, [Validators.required, Validators.min(0)]],
    estimatedMaxDays: [5, [Validators.required, Validators.min(0)]],
    displayOrder: [10, [Validators.required, Validators.min(0)]],
    isActive: [true, [Validators.required]]
  }, { validators: [deliveryDayRangeValidator()] });

  async ngOnInit(): Promise<void> {
    await this.loadSettings();
  }

  protected async saveProfile(): Promise<void> {
    if (!this.ensureValid(this.profileForm)) {
      return;
    }

    const value = this.profileForm.getRawValue();
    await this.saveSettings(
      () => this.onboardingService.updateProfile({
        displayName: value.displayName,
        contactEmail: value.contactEmail,
        phoneNumber: value.phoneNumber,
        businessType: value.businessType,
        businessName: emptyToNull(value.businessName)
      } satisfies UpdateSellerProfileRequest),
      'Profile settings saved.');
  }

  protected async saveStorefront(): Promise<void> {
    if (!this.ensureValid(this.storefrontForm)) {
      return;
    }

    const value = this.storefrontForm.getRawValue();
    await this.saveSettings(
      () => this.onboardingService.updateStorefront({
        storeName: value.storeName,
        slug: value.slug,
        description: emptyToNull(value.description),
        logoUrl: emptyToNull(value.logoUrl),
        bannerUrl: emptyToNull(value.bannerUrl)
      } satisfies UpdateSellerStorefrontRequest),
      'Storefront settings saved.');
  }

  protected async saveAddress(): Promise<void> {
    if (!this.ensureValid(this.addressForm)) {
      return;
    }

    const value = this.addressForm.getRawValue();
    await this.saveSettings(
      () => this.onboardingService.updateAddress({
        addressLine1: value.addressLine1,
        addressLine2: emptyToNull(value.addressLine2),
        city: value.city,
        province: value.province,
        postalCode: value.postalCode,
        countryCode: value.countryCode.toUpperCase()
      } satisfies UpdateSellerAddressRequest),
      'Fulfilment address saved.');
  }

  protected async saveDeliveryMethod(): Promise<void> {
    if (!this.ensureValid(this.deliveryMethodForm)) {
      return;
    }

    const request = this.createDeliveryMethodRequest();
    const editingId = this.editingDeliveryMethodId();
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const saved = editingId
        ? await this.deliveryMethodService.update(editingId, request)
        : await this.deliveryMethodService.create(request);
      this.upsertDeliveryMethod(saved);
      this.cancelDeliveryMethodEdit();
      this.successMessage.set(editingId ? 'Delivery method updated.' : 'Delivery method created.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected editDeliveryMethod(method: SellerDeliveryMethodResponse): void {
    this.editingDeliveryMethodId.set(method.deliveryMethodId);
    this.deliveryMethodForm.patchValue({
      name: method.name,
      description: method.description ?? '',
      methodType: method.methodType,
      countryCode: method.countryCode,
      province: method.province ?? '',
      basePrice: method.basePrice,
      freeShippingThreshold: method.freeShippingThreshold,
      estimatedMinDays: method.estimatedMinDays,
      estimatedMaxDays: method.estimatedMaxDays,
      displayOrder: method.displayOrder,
      isActive: method.isActive
    });
  }

  protected cancelDeliveryMethodEdit(): void {
    this.editingDeliveryMethodId.set(null);
    this.deliveryMethodForm.reset({
      name: '',
      description: '',
      methodType: 'Standard',
      countryCode: 'ZA',
      province: '',
      basePrice: 0,
      freeShippingThreshold: null,
      estimatedMinDays: 2,
      estimatedMaxDays: 5,
      displayOrder: 10,
      isActive: true
    });
  }

  protected async setDeliveryMethodActive(method: SellerDeliveryMethodResponse, isActive: boolean): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const saved = isActive
        ? await this.deliveryMethodService.activate(method.deliveryMethodId)
        : await this.deliveryMethodService.deactivate(method.deliveryMethodId);
      this.upsertDeliveryMethod(saved);
      this.successMessage.set(isActive ? 'Delivery method activated.' : 'Delivery method deactivated.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected verificationTone(): StatusBadgeTone {
    const status = this.onboarding()?.verificationStatus;
    if (status === 'Verified') {
      return 'success';
    }

    if (status === 'Rejected' || status === 'Suspended') {
      return 'danger';
    }

    return 'warning';
  }

  private async loadSettings(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [onboarding, deliveryMethods] = await Promise.all([
        this.onboardingService.getOnboarding(),
        this.deliveryMethodService.list()
      ]);
      this.setOnboarding(onboarding);
      this.deliveryMethods.set(deliveryMethods);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async saveSettings(
    action: () => Promise<SellerOnboardingResponse>,
    successMessage: string)
  {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.setOnboarding(await action());
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private setOnboarding(onboarding: SellerOnboardingResponse): void {
    this.onboarding.set(onboarding);

    this.profileForm.patchValue({
      displayName: onboarding.profile.displayName ?? '',
      contactEmail: onboarding.profile.contactEmail ?? '',
      phoneNumber: onboarding.profile.phoneNumber ?? '',
      businessType: onboarding.profile.businessType === 'RegisteredBusiness'
        ? 'RegisteredBusiness'
        : 'Individual',
      businessName: onboarding.profile.businessName ?? ''
    });

    this.storefrontForm.patchValue({
      storeName: onboarding.storefront?.storeName ?? '',
      slug: onboarding.storefront?.slug ?? '',
      description: onboarding.storefront?.description ?? '',
      logoUrl: onboarding.storefront?.logoUrl ?? '',
      bannerUrl: onboarding.storefront?.bannerUrl ?? ''
    });

    this.addressForm.patchValue({
      addressLine1: onboarding.address?.addressLine1 ?? '',
      addressLine2: onboarding.address?.addressLine2 ?? '',
      city: onboarding.address?.city ?? '',
      province: onboarding.address?.province ?? '',
      postalCode: onboarding.address?.postalCode ?? '',
      countryCode: onboarding.address?.countryCode ?? 'ZA'
    });
  }

  private ensureValid(control: AbstractControl): boolean {
    if (control.invalid || this.isSaving()) {
      control.markAllAsTouched();
      return false;
    }

    return true;
  }

  private createDeliveryMethodRequest(): SellerDeliveryMethodRequest {
    const value = this.deliveryMethodForm.getRawValue();
    return {
      name: value.name,
      description: emptyToNull(value.description),
      methodType: value.methodType,
      countryCode: value.countryCode.toUpperCase(),
      province: emptyToNull(value.province),
      basePrice: Number(value.basePrice),
      freeShippingThreshold: value.freeShippingThreshold === null
        ? null
        : Number(value.freeShippingThreshold),
      estimatedMinDays: Number(value.estimatedMinDays),
      estimatedMaxDays: Number(value.estimatedMaxDays),
      displayOrder: Number(value.displayOrder),
      isActive: value.isActive
    };
  }

  private upsertDeliveryMethod(method: SellerDeliveryMethodResponse): void {
    const methods = this.deliveryMethods();
    const existingIndex = methods.findIndex(item => item.deliveryMethodId === method.deliveryMethodId);
    const updated = existingIndex >= 0
      ? methods.map(item => item.deliveryMethodId === method.deliveryMethodId ? method : item)
      : [...methods, method];

    this.deliveryMethods.set(updated.sort((left, right) =>
      Number(right.isActive) - Number(left.isActive)
      || left.displayOrder - right.displayOrder
      || left.name.localeCompare(right.name)));
  }
}

function businessNameRequiredValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const businessType = control.get('businessType')?.value;
    const businessName = control.get('businessName')?.value;

    return businessType === 'RegisteredBusiness' && !businessName?.trim()
      ? { businessNameRequired: true }
      : null;
  };
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function deliveryDayRangeValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const minDays = Number(control.get('estimatedMinDays')?.value);
    const maxDays = Number(control.get('estimatedMaxDays')?.value);

    return Number.isFinite(minDays) && Number.isFinite(maxDays) && maxDays < minDays
      ? { invalidDayRange: true }
      : null;
  };
}
