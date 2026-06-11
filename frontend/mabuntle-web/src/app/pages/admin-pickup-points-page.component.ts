import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminPickupPointRequest, AdminPickupPointResponse } from '../admin/admin-pickup-point.models';
import { AdminPickupPointService } from '../admin/admin-pickup-point.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-pickup-points-page',
  imports: [
    AdminWorkspaceNavComponent,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-finance-page">
      <app-admin-workspace-nav />

      <app-page-header
        eyebrow="Admin delivery"
        heading="Pickup points"
        description="Manage platform pickup locations that sellers can expose through pickup-point delivery methods."
      />

      @if (errorMessage()) {
        <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
      }

      @if (successMessage()) {
        <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
      }

      <div class="buyer-detail-grid">
        <section class="buyer-panel">
          <h2>Pickup point list</h2>
          @if (isLoading()) {
            <div class="route-card">Loading pickup points...</div>
          } @else if (pickupPoints().length === 0) {
            <app-empty-state
              eyebrow="No pickup points"
              heading="Create the first pickup location"
              message="Pickup delivery methods need at least one active matching platform pickup point before buyers can select pickup."
            />
          } @else {
            <div class="seller-item-list">
              @for (point of pickupPoints(); track point.pickupPointId) {
                <article class="seller-item-row">
                  <span>
                    <strong>{{ point.name }}</strong>
                    <small>{{ point.providerName }} / {{ point.code }} - {{ point.city }}, {{ point.province }}</small>
                    @if (point.openingHours) {
                      <small>{{ point.openingHours }}</small>
                    }
                  </span>
                  <app-status-badge [label]="point.isActive ? 'Active' : 'Inactive'" [tone]="point.isActive ? 'success' : 'warning'" />
                  <span class="seller-action-row">
                    <button data-ui-button="secondary" type="button" (click)="edit(point)">Edit</button>
                    @if (point.isActive) {
                      <button data-ui-button="secondary" type="button" [disabled]="isSaving()" (click)="setActive(point, false)">Deactivate</button>
                    } @else {
                      <button data-ui-button="secondary" type="button" [disabled]="isSaving()" (click)="setActive(point, true)">Activate</button>
                    }
                  </span>
                </article>
              }
            </div>
          }
        </section>

        <form class="buyer-panel buyer-form-grid" [formGroup]="form" (ngSubmit)="save()" novalidate>
          <h2>{{ editingPickupPointId() ? 'Edit pickup point' : 'Create pickup point' }}</h2>
          <label class="ui-field">
            <span>Provider</span>
            <input formControlName="providerName" />
          </label>
          <label class="ui-field">
            <span>Code</span>
            <input formControlName="code" />
          </label>
          <label class="ui-field">
            <span>Name</span>
            <input formControlName="name" />
          </label>
          <label class="ui-field">
            <span>Address line 1</span>
            <input formControlName="addressLine1" />
          </label>
          <label class="ui-field">
            <span>Address line 2</span>
            <input formControlName="addressLine2" />
          </label>
          <label class="ui-field">
            <span>Suburb</span>
            <input formControlName="suburb" />
          </label>
          <label class="ui-field">
            <span>City</span>
            <input formControlName="city" />
          </label>
          <label class="ui-field">
            <span>Province</span>
            <input formControlName="province" />
          </label>
          <label class="ui-field">
            <span>Postal code</span>
            <input formControlName="postalCode" />
          </label>
          <label class="ui-field">
            <span>Country code</span>
            <input maxlength="2" formControlName="countryCode" />
          </label>
          <label class="ui-field">
            <span>Latitude</span>
            <input type="number" formControlName="latitude" />
          </label>
          <label class="ui-field">
            <span>Longitude</span>
            <input type="number" formControlName="longitude" />
          </label>
          <label class="ui-field">
            <span>Opening hours</span>
            <textarea rows="3" formControlName="openingHours"></textarea>
          </label>
          <label class="ui-checkbox"><input type="checkbox" formControlName="isActive" /><span>Active</span></label>
          <div class="seller-action-row">
            <button data-ui-button="primary" type="submit" [disabled]="form.invalid || isSaving()">
              {{ isSaving() ? 'Saving...' : editingPickupPointId() ? 'Save pickup point' : 'Create pickup point' }}
            </button>
            @if (editingPickupPointId()) {
              <button data-ui-button="secondary" type="button" (click)="resetForm()">Cancel edit</button>
            }
          </div>
        </form>
      </div>
    </section>
  `
})
export class AdminPickupPointsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly pickupPointService = inject(AdminPickupPointService);

  protected readonly pickupPoints = signal<AdminPickupPointResponse[]>([]);
  protected readonly editingPickupPointId = signal<string | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly form = this.formBuilder.group({
    providerName: ['Manual', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    addressLine1: ['', [Validators.required]],
    addressLine2: [''],
    suburb: [''],
    city: ['', [Validators.required]],
    province: ['', [Validators.required]],
    postalCode: ['', [Validators.required]],
    countryCode: ['ZA', [Validators.required, Validators.minLength(2), Validators.maxLength(2)]],
    latitude: [null as number | null],
    longitude: [null as number | null],
    openingHours: [''],
    isActive: [true]
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async save(): Promise<void> {
    if (this.form.invalid || this.isSaving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      const editingId = this.editingPickupPointId();
      const saved = editingId
        ? await this.pickupPointService.update(editingId, this.toRequest())
        : await this.pickupPointService.create(this.toRequest());
      this.upsert(saved);
      this.resetForm();
      this.successMessage.set(editingId ? 'Pickup point updated.' : 'Pickup point created.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected edit(point: AdminPickupPointResponse): void {
    this.editingPickupPointId.set(point.pickupPointId);
    this.form.patchValue({
      providerName: point.providerName,
      code: point.code,
      name: point.name,
      addressLine1: point.addressLine1,
      addressLine2: point.addressLine2 ?? '',
      suburb: point.suburb ?? '',
      city: point.city,
      province: point.province,
      postalCode: point.postalCode,
      countryCode: point.countryCode,
      latitude: point.latitude,
      longitude: point.longitude,
      openingHours: point.openingHours ?? '',
      isActive: point.isActive
    });
  }

  protected async setActive(point: AdminPickupPointResponse, isActive: boolean): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      const saved = isActive
        ? await this.pickupPointService.activate(point.pickupPointId)
        : await this.pickupPointService.deactivate(point.pickupPointId);
      this.upsert(saved);
      this.successMessage.set(isActive ? 'Pickup point activated.' : 'Pickup point deactivated.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected resetForm(): void {
    this.editingPickupPointId.set(null);
    this.form.reset({
      providerName: 'Manual',
      code: '',
      name: '',
      addressLine1: '',
      addressLine2: '',
      suburb: '',
      city: '',
      province: '',
      postalCode: '',
      countryCode: 'ZA',
      latitude: null,
      longitude: null,
      openingHours: '',
      isActive: true
    });
  }

  private async load(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    try {
      this.pickupPoints.set(await this.pickupPointService.list());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private toRequest(): AdminPickupPointRequest {
    const value = this.form.getRawValue();
    return {
      providerName: value.providerName.trim(),
      code: value.code.trim(),
      name: value.name.trim(),
      addressLine1: value.addressLine1.trim(),
      addressLine2: emptyToNull(value.addressLine2),
      suburb: emptyToNull(value.suburb),
      city: value.city.trim(),
      province: value.province.trim(),
      postalCode: value.postalCode.trim(),
      countryCode: value.countryCode.trim().toUpperCase(),
      latitude: value.latitude === null ? null : Number(value.latitude),
      longitude: value.longitude === null ? null : Number(value.longitude),
      openingHours: emptyToNull(value.openingHours),
      isActive: value.isActive
    };
  }

  private upsert(point: AdminPickupPointResponse): void {
    const points = this.pickupPoints();
    const updated = points.some(item => item.pickupPointId === point.pickupPointId)
      ? points.map(item => item.pickupPointId === point.pickupPointId ? point : item)
      : [...points, point];

    this.pickupPoints.set(updated.sort((left, right) =>
      Number(right.isActive) - Number(left.isActive)
      || left.countryCode.localeCompare(right.countryCode)
      || left.province.localeCompare(right.province)
      || left.name.localeCompare(right.name)));
  }
}

function emptyToNull(value: string): string | null {
  const normalized = value.trim();
  return normalized.length > 0 ? normalized : null;
}
