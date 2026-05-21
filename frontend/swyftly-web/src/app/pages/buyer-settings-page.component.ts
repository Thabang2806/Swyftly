import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerSettingsService } from '../buyer/buyer-settings.service';
import {
  BuyerDeliveryAddressResponse,
  BuyerNotificationPreferenceCategory,
  BuyerProfileSettingsResponse
} from '../buyer/buyer-settings.models';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

type PreferenceControlGroup = Record<BuyerNotificationPreferenceCategory, FormControl<boolean>>;

@Component({
  selector: 'app-buyer-settings-page',
  imports: [
    BuyerWorkspaceNavComponent,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page buyer-settings-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Settings"
        description="Manage lightweight account details and the account updates you want to receive."
      >
        <div pageHeaderActions>
          <a mat-stroked-button routerLink="/account">Account dashboard</a>
          <a mat-stroked-button routerLink="/account/notifications">Notifications</a>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading settings...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <div class="buyer-settings-grid">
          <form class="route-card wizard-form" [formGroup]="profileForm" (ngSubmit)="saveProfile()" novalidate>
            <div>
              <h2>Profile</h2>
              <p>These details help support identify your account. Your sign-in email is managed separately.</p>
            </div>

            <dl class="buyer-settings-facts">
              <div>
                <dt>Email</dt>
                <dd>{{ profile()?.email }}</dd>
              </div>
            </dl>

            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Display name</mat-label>
                <input matInput formControlName="displayName" maxlength="160">
                @if (profileForm.controls.displayName.hasError('maxlength')) {
                  <mat-error>Display name must be 160 characters or fewer.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Phone number</mat-label>
                <input matInput formControlName="phoneNumber" maxlength="64">
                @if (profileForm.controls.phoneNumber.hasError('maxlength')) {
                  <mat-error>Phone number must be 64 characters or fewer.</mat-error>
                }
              </mat-form-field>
            </div>

            <button mat-flat-button type="submit" [disabled]="profileForm.invalid || isSavingProfile()">
              {{ isSavingProfile() ? 'Saving...' : 'Save profile' }}
            </button>
          </form>

          <form class="route-card wizard-form" (ngSubmit)="savePreferences()" novalidate>
            <div>
              <h2>Notification preferences</h2>
              <p>Choose in-app and email updates separately. Existing notifications stay in your account.</p>
            </div>

            <div class="buyer-preference-list">
              @for (item of preferenceLabels; track item.category) {
                <div class="buyer-preference-item">
                  <div>
                    <strong>{{ item.label }}</strong>
                    <span>{{ item.description }}</span>
                  </div>
                  <div class="buyer-preference-channels">
                    <mat-checkbox [formControl]="notificationForm.controls[item.category]">In-app</mat-checkbox>
                    <mat-checkbox [formControl]="emailNotificationForm.controls[item.category]">Email</mat-checkbox>
                  </div>
                </div>
              }
            </div>

            <button mat-flat-button type="submit" [disabled]="isSavingPreferences()">
              {{ isSavingPreferences() ? 'Saving...' : 'Save notification preferences' }}
            </button>
          </form>

          <section class="route-card wizard-form buyer-address-settings">
            <div>
              <h2>Saved delivery addresses</h2>
              <p>Use saved addresses at checkout. Orders keep their own address snapshot after payment starts.</p>
            </div>

            @if (deliveryAddresses().length === 0) {
              <app-ui-alert tone="info">No saved delivery addresses yet. Add one now or enter a one-off address during checkout.</app-ui-alert>
            } @else {
              <div class="buyer-address-list">
                @for (address of deliveryAddresses(); track address.deliveryAddressId) {
                  <article class="buyer-address-card">
                    <div>
                      <strong>{{ address.label }}</strong>
                      @if (address.isDefault) {
                        <span>Default</span>
                      }
                    </div>
                    <p>{{ address.recipientName }} - {{ address.phoneNumber }}</p>
                    <p>{{ formatAddress(address) }}</p>
                    @if (address.deliveryInstructions) {
                      <p>Instructions: {{ address.deliveryInstructions }}</p>
                    }
                    <div class="buyer-action-row">
                      <button mat-stroked-button type="button" (click)="editDeliveryAddress(address)">Edit</button>
                      <button mat-stroked-button type="button" [disabled]="address.isDefault || isSavingAddress()" (click)="makeDefaultDeliveryAddress(address.deliveryAddressId)">Make default</button>
                      <button mat-stroked-button type="button" [disabled]="isSavingAddress()" (click)="deleteDeliveryAddress(address.deliveryAddressId)">Delete</button>
                    </div>
                  </article>
                }
              </div>
            }

            <form [formGroup]="addressForm" (ngSubmit)="saveDeliveryAddress()" class="buyer-form-grid" novalidate>
              <mat-form-field appearance="outline">
                <mat-label>Label</mat-label>
                <input matInput formControlName="label" maxlength="80">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Recipient name</mat-label>
                <input matInput formControlName="recipientName" maxlength="160">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Phone number</mat-label>
                <input matInput formControlName="phoneNumber" maxlength="64">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Address line 1</mat-label>
                <input matInput formControlName="addressLine1" maxlength="240">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Address line 2</mat-label>
                <input matInput formControlName="addressLine2" maxlength="240">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Suburb</mat-label>
                <input matInput formControlName="suburb" maxlength="120">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>City</mat-label>
                <input matInput formControlName="city" maxlength="120">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Province</mat-label>
                <input matInput formControlName="province" maxlength="120">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Postal code</mat-label>
                <input matInput formControlName="postalCode" maxlength="32">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Country code</mat-label>
                <input matInput formControlName="countryCode" maxlength="2">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Delivery instructions</mat-label>
                <textarea matInput rows="3" formControlName="deliveryInstructions" maxlength="500"></textarea>
                @if (addressForm.controls.deliveryInstructions.hasError('maxlength')) {
                  <mat-error>Delivery instructions must be 500 characters or fewer.</mat-error>
                }
              </mat-form-field>

              <mat-checkbox formControlName="isDefault">Use as default delivery address</mat-checkbox>

              <div class="buyer-action-row">
                <button mat-flat-button type="submit" [disabled]="addressForm.invalid || isSavingAddress()">
                  {{ isSavingAddress() ? 'Saving...' : editingAddressId() ? 'Save address' : 'Add address' }}
                </button>
                @if (editingAddressId()) {
                  <button mat-stroked-button type="button" (click)="resetDeliveryAddressForm()">Cancel edit</button>
                }
              </div>
            </form>
          </section>

          <aside class="route-card buyer-settings-note">
            <h2>Not included yet</h2>
            <p>SMS, push delivery channels, password or email changes, carrier delivery options, and verified address lookup are separate workflows.</p>
          </aside>
        </div>
      }
    </section>
  `
})
export class BuyerSettingsPageComponent implements OnInit {
  private readonly settingsService = inject(BuyerSettingsService);

  protected readonly profile = signal<BuyerProfileSettingsResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSavingProfile = signal(false);
  protected readonly isSavingPreferences = signal(false);
  protected readonly isSavingAddress = signal(false);
  protected readonly deliveryAddresses = signal<BuyerDeliveryAddressResponse[]>([]);
  protected readonly editingAddressId = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly profileForm = new FormGroup({
    displayName: new FormControl<string | null>(null, [Validators.maxLength(160)]),
    phoneNumber: new FormControl<string | null>(null, [Validators.maxLength(64)])
  });

  protected readonly notificationForm = new FormGroup<PreferenceControlGroup>({
    Orders: new FormControl(true, { nonNullable: true }),
    Returns: new FormControl(true, { nonNullable: true }),
    Reviews: new FormControl(true, { nonNullable: true }),
    Support: new FormControl(true, { nonNullable: true })
  });

  protected readonly emailNotificationForm = new FormGroup<PreferenceControlGroup>({
    Orders: new FormControl(true, { nonNullable: true }),
    Returns: new FormControl(true, { nonNullable: true }),
    Reviews: new FormControl(true, { nonNullable: true }),
    Support: new FormControl(true, { nonNullable: true })
  });

  protected readonly addressForm = new FormGroup({
    label: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(80)] }),
    recipientName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(160)] }),
    phoneNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(64)] }),
    addressLine1: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(240)] }),
    addressLine2: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(240)] }),
    suburb: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120)] }),
    city: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    province: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    postalCode: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(32)] }),
    countryCode: new FormControl('ZA', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(2)] }),
    deliveryInstructions: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
    isDefault: new FormControl(false, { nonNullable: true })
  });

  protected readonly preferenceLabels: readonly {
    category: BuyerNotificationPreferenceCategory;
    label: string;
    description: string;
  }[] = [
    {
      category: 'Orders',
      label: 'Order updates',
      description: 'Tracking, shipped, and delivered order updates.'
    },
    {
      category: 'Returns',
      label: 'Return updates',
      description: 'Seller approval or rejection decisions for return requests.'
    },
    {
      category: 'Reviews',
      label: 'Review moderation',
      description: 'Review approved or needs-change decisions.'
    },
    {
      category: 'Support',
      label: 'Support replies',
      description: 'Public support-agent replies to your tickets.'
    }
  ];

  async ngOnInit(): Promise<void> {
    await this.loadSettings();
  }

  protected async saveProfile(): Promise<void> {
    if (this.profileForm.invalid || this.isSavingProfile()) {
      return;
    }

    this.isSavingProfile.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.settingsService.updateProfile({
        displayName: this.emptyToNull(this.profileForm.controls.displayName.value),
        phoneNumber: this.emptyToNull(this.profileForm.controls.phoneNumber.value)
      });
      this.profile.set(updated);
      this.profileForm.patchValue({
        displayName: updated.displayName,
        phoneNumber: updated.phoneNumber
      });
      this.successMessage.set('Profile settings saved.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingProfile.set(false);
    }
  }

  protected async savePreferences(): Promise<void> {
    if (this.isSavingPreferences()) {
      return;
    }

    this.isSavingPreferences.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const response = await this.settingsService.updateNotificationPreferences({
        preferences: this.preferenceLabels.map(item => ({
          category: item.category,
          isEnabled: this.notificationForm.controls[item.category].value,
          emailEnabled: this.emailNotificationForm.controls[item.category].value
        }))
      });
      this.applyPreferences(response.preferences);
      this.successMessage.set('Notification preferences saved.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingPreferences.set(false);
    }
  }

  protected async saveDeliveryAddress(): Promise<void> {
    if (this.addressForm.invalid || this.isSavingAddress()) {
      this.addressForm.markAllAsTouched();
      return;
    }

    this.isSavingAddress.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const editingId = this.editingAddressId();
      if (editingId) {
        await this.settingsService.updateDeliveryAddress(editingId, this.toAddressRequest());
        this.successMessage.set('Delivery address saved.');
      } else {
        await this.settingsService.createDeliveryAddress(this.toAddressRequest());
        this.successMessage.set('Delivery address added.');
      }

      this.deliveryAddresses.set(await this.settingsService.listDeliveryAddresses());
      this.resetDeliveryAddressForm();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingAddress.set(false);
    }
  }

  protected editDeliveryAddress(address: BuyerDeliveryAddressResponse): void {
    this.editingAddressId.set(address.deliveryAddressId);
    this.addressForm.setValue({
      label: address.label,
      recipientName: address.recipientName,
      phoneNumber: address.phoneNumber,
      addressLine1: address.addressLine1,
      addressLine2: address.addressLine2 ?? '',
      suburb: address.suburb ?? '',
      city: address.city,
      province: address.province,
      postalCode: address.postalCode,
      countryCode: address.countryCode,
      deliveryInstructions: address.deliveryInstructions ?? '',
      isDefault: address.isDefault
    });
  }

  protected async deleteDeliveryAddress(deliveryAddressId: string): Promise<void> {
    if (this.isSavingAddress()) {
      return;
    }

    this.isSavingAddress.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      await this.settingsService.deleteDeliveryAddress(deliveryAddressId);
      this.deliveryAddresses.set(await this.settingsService.listDeliveryAddresses());
      if (this.editingAddressId() === deliveryAddressId) {
        this.resetDeliveryAddressForm();
      }
      this.successMessage.set('Delivery address deleted.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingAddress.set(false);
    }
  }

  protected async makeDefaultDeliveryAddress(deliveryAddressId: string): Promise<void> {
    if (this.isSavingAddress()) {
      return;
    }

    this.isSavingAddress.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      this.deliveryAddresses.set(await this.settingsService.makeDefaultDeliveryAddress(deliveryAddressId));
      this.successMessage.set('Default delivery address updated.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingAddress.set(false);
    }
  }

  protected resetDeliveryAddressForm(): void {
    this.editingAddressId.set(null);
    this.addressForm.reset({
      label: '',
      recipientName: '',
      phoneNumber: '',
      addressLine1: '',
      addressLine2: '',
      suburb: '',
      city: '',
      province: '',
      postalCode: '',
      countryCode: 'ZA',
      deliveryInstructions: '',
      isDefault: this.deliveryAddresses().length === 0
    });
  }

  protected formatAddress(address: BuyerDeliveryAddressResponse): string {
    return [
      address.addressLine1,
      address.addressLine2,
      address.suburb,
      address.city,
      address.province,
      address.postalCode,
      address.countryCode
    ].filter(Boolean).join(', ');
  }

  private async loadSettings(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [profile, preferences, deliveryAddresses] = await Promise.all([
        this.settingsService.getProfile(),
        this.settingsService.getNotificationPreferences(),
        this.settingsService.listDeliveryAddresses()
      ]);
      this.profile.set(profile);
      this.deliveryAddresses.set(deliveryAddresses);
      this.profileForm.patchValue({
        displayName: profile.displayName,
        phoneNumber: profile.phoneNumber
      });
      this.applyPreferences(preferences.preferences);
      this.resetDeliveryAddressForm();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private applyPreferences(
    preferences: readonly { category: BuyerNotificationPreferenceCategory; isEnabled: boolean; emailEnabled: boolean }[]
  ): void {
    for (const preference of preferences) {
      this.notificationForm.controls[preference.category].setValue(preference.isEnabled);
      this.emailNotificationForm.controls[preference.category].setValue(preference.emailEnabled);
    }
  }

  private emptyToNull(value: string | null): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }

  private toAddressRequest() {
    const value = this.addressForm.getRawValue();
    return {
      label: value.label.trim(),
      recipientName: value.recipientName.trim(),
      phoneNumber: value.phoneNumber.trim(),
      addressLine1: value.addressLine1.trim(),
      addressLine2: this.emptyToNull(value.addressLine2),
      suburb: this.emptyToNull(value.suburb),
      city: value.city.trim(),
      province: value.province.trim(),
      postalCode: value.postalCode.trim(),
      countryCode: value.countryCode.trim().toUpperCase(),
      deliveryInstructions: this.emptyToNull(value.deliveryInstructions),
      isDefault: value.isDefault
    };
  }
}
