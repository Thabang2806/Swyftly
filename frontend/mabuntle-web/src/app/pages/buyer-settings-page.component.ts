import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerAiDiscoveryService } from '../buyer/buyer-ai-discovery.service';
import { BuyerSettingsService } from '../buyer/buyer-settings.service';
import {
  BuyerDeliveryAddressResponse,
  BuyerDeliveryAddressVerificationResponse,
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
          <a data-ui-button="secondary" routerLink="/account">Account dashboard</a>
          <a data-ui-button="secondary" routerLink="/account/notifications">Notifications</a>
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
              <label class="ui-field">
                <span>Display name</span>
                <input formControlName="displayName" maxlength="160">
                @if (profileForm.controls.displayName.hasError('maxlength')) {
                  <span class="ui-field-error">Display name must be 160 characters or fewer.</span>
                }
              </label>

              <label class="ui-field">
                <span>Phone number</span>
                <input formControlName="phoneNumber" maxlength="64">
                @if (profileForm.controls.phoneNumber.hasError('maxlength')) {
                  <span class="ui-field-error">Phone number must be 64 characters or fewer.</span>
                }
              </label>
            </div>

            <button data-ui-button="primary" type="submit" [disabled]="profileForm.invalid || isSavingProfile()">
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
                    <label class="ui-checkbox"><input type="checkbox" [formControl]="notificationForm.controls[item.category]"><span>In-app</span></label>
                    <label class="ui-checkbox"><input type="checkbox" [formControl]="emailNotificationForm.controls[item.category]"><span>Email</span></label>
                  </div>
                </div>
              }
            </div>

            <button data-ui-button="primary" type="submit" [disabled]="isSavingPreferences()">
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
                    <p>{{ renderAddress(address) }}</p>
                    @if (address.deliveryInstructions) {
                      <p>Instructions: {{ address.deliveryInstructions }}</p>
                    }
                    @if (address.verificationStatus) {
                      <p>Address check: {{ addressCheckLabel(address.verificationStatus) }}</p>
                    }
                    @if ((address.verificationWarnings?.length ?? 0) > 0) {
                      <p>Review notes: {{ address.verificationWarnings!.join(' ') }}</p>
                    }
                    <div class="buyer-action-row">
                      <button data-ui-button="secondary" type="button" (click)="editDeliveryAddress(address)">Edit</button>
                      <button data-ui-button="secondary" type="button" [disabled]="address.isDefault || isSavingAddress()" (click)="makeDefaultDeliveryAddress(address.deliveryAddressId)">Make default</button>
                      <button data-ui-button="secondary" type="button" [disabled]="isSavingAddress()" (click)="deleteDeliveryAddress(address.deliveryAddressId)">Delete</button>
                    </div>
                  </article>
                }
              </div>
            }

            <form [formGroup]="addressForm" (ngSubmit)="saveDeliveryAddress()" class="buyer-form-grid" novalidate>
              <label class="ui-field">
                <span>Label</span>
                <input formControlName="label" maxlength="80">
              </label>

              <label class="ui-field">
                <span>Recipient name</span>
                <input formControlName="recipientName" maxlength="160">
              </label>

              <label class="ui-field">
                <span>Phone number</span>
                <input formControlName="phoneNumber" maxlength="64">
              </label>

              <label class="ui-field">
                <span>Address line 1</span>
                <input formControlName="addressLine1" maxlength="240">
              </label>

              <label class="ui-field">
                <span>Address line 2</span>
                <input formControlName="addressLine2" maxlength="240">
              </label>

              <label class="ui-field">
                <span>Suburb</span>
                <input formControlName="suburb" maxlength="120">
              </label>

              <label class="ui-field">
                <span>City</span>
                <input formControlName="city" maxlength="120">
              </label>

              <label class="ui-field">
                <span>Province</span>
                <input formControlName="province" maxlength="120">
              </label>

              <label class="ui-field">
                <span>Postal code</span>
                <input formControlName="postalCode" maxlength="32">
              </label>

              <label class="ui-field">
                <span>Country code</span>
                <input formControlName="countryCode" maxlength="2">
              </label>

              <label class="ui-field">
                <span>Delivery instructions</span>
                <textarea rows="3" formControlName="deliveryInstructions" maxlength="500"></textarea>
                @if (addressForm.controls.deliveryInstructions.hasError('maxlength')) {
                  <span class="ui-field-error">Delivery instructions must be 500 characters or fewer.</span>
                }
              </label>

              <label class="ui-checkbox"><input type="checkbox" formControlName="isDefault"><span>Use as default delivery address</span></label>

              @if (addressVerificationPreview(); as verification) {
                <app-ui-alert [tone]="verification.verificationStatus === 'Verified' ? 'success' : 'warning'">
                  Address check: {{ addressCheckLabel(verification.verificationStatus) }}.
                  @if (verification.verificationWarnings.length > 0) {
                    {{ verification.verificationWarnings.join(' ') }}
                  } @else {
                    No address review notes.
                  }
                </app-ui-alert>
              }

              <div class="buyer-action-row">
                <button data-ui-button="secondary" type="button" [disabled]="addressForm.invalid || isVerifyingAddress()" (click)="verifyDeliveryAddress()">
                  {{ isVerifyingAddress() ? 'Checking...' : 'Verify address' }}
                </button>
                <button data-ui-button="primary" type="submit" [disabled]="addressForm.invalid || isSavingAddress()">
                  {{ isSavingAddress() ? 'Saving...' : editingAddressId() ? 'Save address' : 'Add address' }}
                </button>
                @if (editingAddressId()) {
                  <button data-ui-button="secondary" type="button" (click)="resetDeliveryAddressForm()">Cancel edit</button>
                }
              </div>
            </form>
          </section>

          <section class="route-card wizard-form buyer-ai-history-settings">
            <div>
              <h2>AI discovery history</h2>
              <p>
                This is off by default. When enabled, Mabuntle saves safe assistant and visual-search summaries:
                category, colour, material, confidence, result count, and product ids only.
              </p>
              <p>
                Prompts, uploaded images, image previews, base64 content, provider payloads, and full AI responses are not stored.
                Browser-local recent prompts and references stay separate on each page.
              </p>
            </div>

            <label class="ui-checkbox">
              <input type="checkbox" [formControl]="aiHistoryForm.controls.historyEnabled">
              <span>Save AI discovery summaries across devices</span>
            </label>

            <label class="ui-checkbox">
              <input type="checkbox" [formControl]="aiHistoryForm.controls.personalizationEnabled">
              <span>Personalize assistant and visual-search results</span>
            </label>
            <p>
              Personalized AI discovery is optional. When enabled, Mabuntle can use saved items, recent cart/order interest,
              and enabled AI history summaries to reorder matching products and explain why they were suggested.
            </p>

            <div class="buyer-action-row">
              <button data-ui-button="primary" type="button" [disabled]="isSavingAiHistory()" (click)="saveAiHistoryPreference()">
                {{ isSavingAiHistory() ? 'Saving...' : 'Save AI discovery preferences' }}
              </button>
              <button data-ui-button="secondary" type="button" [disabled]="isClearingAiHistory()" (click)="clearAiHistory()">
                {{ isClearingAiHistory() ? 'Clearing...' : 'Clear server history' }}
              </button>
              <a data-ui-button="secondary" routerLink="/account/ai-history">View AI history</a>
            </div>
          </section>

          <aside class="route-card buyer-settings-note">
            <h2>Account channel note</h2>
            <p>In-app and email preferences are available now. SMS, push, password changes, email changes, and external address lookup will be handled in separate account workflows.</p>
          </aside>
        </div>
      }
    </section>
  `
})
export class BuyerSettingsPageComponent implements OnInit {
  private readonly settingsService = inject(BuyerSettingsService);
  private readonly aiDiscoveryService = inject(BuyerAiDiscoveryService);

  protected readonly profile = signal<BuyerProfileSettingsResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSavingProfile = signal(false);
  protected readonly isSavingPreferences = signal(false);
  protected readonly isSavingAiHistory = signal(false);
  protected readonly isClearingAiHistory = signal(false);
  protected readonly isSavingAddress = signal(false);
  protected readonly isVerifyingAddress = signal(false);
  protected readonly deliveryAddresses = signal<BuyerDeliveryAddressResponse[]>([]);
  protected readonly addressVerificationPreview = signal<BuyerDeliveryAddressVerificationResponse | null>(null);
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

  protected readonly aiHistoryForm = new FormGroup({
    historyEnabled: new FormControl(false, { nonNullable: true }),
    personalizationEnabled: new FormControl(false, { nonNullable: true })
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

  protected async saveAiHistoryPreference(): Promise<void> {
    if (this.isSavingAiHistory()) {
      return;
    }

    this.isSavingAiHistory.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const response = await this.aiDiscoveryService.updatePreferences({
        historyEnabled: this.aiHistoryForm.controls.historyEnabled.value,
        personalizationEnabled: this.aiHistoryForm.controls.personalizationEnabled.value
      });
      this.aiHistoryForm.controls.historyEnabled.setValue(response.historyEnabled);
      this.aiHistoryForm.controls.personalizationEnabled.setValue(response.personalizationEnabled);
      if (response.historyEnabled && response.personalizationEnabled) {
        this.successMessage.set('AI discovery history and personalized AI discovery are enabled.');
      } else if (response.personalizationEnabled) {
        this.successMessage.set('Personalized AI discovery enabled. Server history remains off unless you enable it separately.');
      } else if (response.historyEnabled) {
        this.successMessage.set('AI discovery history enabled. Future successful assistant and visual-search results can be saved.');
      } else {
        this.successMessage.set('AI discovery history and personalized AI discovery are disabled. Existing server history remains until you clear it.');
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSavingAiHistory.set(false);
    }
  }

  protected async clearAiHistory(): Promise<void> {
    if (this.isClearingAiHistory()) {
      return;
    }

    this.isClearingAiHistory.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await this.aiDiscoveryService.clearHistory();
      this.successMessage.set('Server-side AI discovery history cleared. Browser-local recent prompts are managed on the assistant and visual-search pages.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isClearingAiHistory.set(false);
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

  protected async verifyDeliveryAddress(): Promise<void> {
    if (this.addressForm.invalid || this.isVerifyingAddress()) {
      this.addressForm.markAllAsTouched();
      return;
    }

    this.isVerifyingAddress.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      this.addressVerificationPreview.set(await this.settingsService.verifyDeliveryAddress(this.toVerificationRequest()));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isVerifyingAddress.set(false);
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
    this.addressVerificationPreview.set(address.verificationStatus
      ? {
          verificationStatus: address.verificationStatus,
          verificationProvider: address.verificationProvider ?? 'LocalRules',
          verificationWarnings: address.verificationWarnings ?? [],
          verifiedAtUtc: address.verifiedAtUtc ?? '',
          recipientName: address.recipientName,
          phoneNumber: address.phoneNumber,
          addressLine1: address.addressLine1,
          addressLine2: address.addressLine2,
          suburb: address.suburb,
          city: address.city,
          province: address.province,
          postalCode: address.postalCode,
          countryCode: address.countryCode,
          deliveryInstructions: address.deliveryInstructions
        }
      : null);
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
    this.addressVerificationPreview.set(null);
  }

  protected renderAddress(address: BuyerDeliveryAddressResponse): string {
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

  protected addressCheckLabel(status: string): string {
    if (status === 'Verified') {
      return 'Looks complete';
    }

    if (status === 'NeedsReview' || status === 'Warning') {
      return 'Needs buyer review';
    }

    return status;
  }

  private async loadSettings(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [profile, preferences, deliveryAddresses, aiHistoryPreference] = await Promise.all([
        this.settingsService.getProfile(),
        this.settingsService.getNotificationPreferences(),
        this.settingsService.listDeliveryAddresses(),
        this.aiDiscoveryService.getPreferences()
      ]);
      this.profile.set(profile);
      this.deliveryAddresses.set(deliveryAddresses);
      this.profileForm.patchValue({
        displayName: profile.displayName,
        phoneNumber: profile.phoneNumber
      });
      this.applyPreferences(preferences.preferences);
      this.aiHistoryForm.controls.historyEnabled.setValue(aiHistoryPreference.historyEnabled);
      this.aiHistoryForm.controls.personalizationEnabled.setValue(aiHistoryPreference.personalizationEnabled);
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

  private toVerificationRequest() {
    const value = this.toAddressRequest();
    return {
      recipientName: value.recipientName,
      phoneNumber: value.phoneNumber,
      addressLine1: value.addressLine1,
      addressLine2: value.addressLine2,
      suburb: value.suburb,
      city: value.city,
      province: value.province,
      postalCode: value.postalCode,
      countryCode: value.countryCode,
      deliveryInstructions: value.deliveryInstructions
    };
  }
}
