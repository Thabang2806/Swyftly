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
import { getApiErrorMessage } from '../auth/api-error';
import { SellerDashboardSummaryResponse } from '../seller/seller-dashboard.models';
import { SellerDashboardService } from '../seller/seller-dashboard.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import {
  SellerOnboardingResponse,
  UpdateSellerAddressRequest,
  UpdateSellerPayoutRequest,
  UpdateSellerProfileRequest,
  UpdateSellerStorefrontRequest
} from '../seller/seller-onboarding.models';
import { SellerOnboardingService } from '../seller/seller-onboarding.service';
import {
  SellerVerificationEvidenceResponse,
  SellerVerificationEvidenceType
} from '../seller/seller-verification-evidence.models';
import { SellerVerificationEvidenceService } from '../seller/seller-verification-evidence.service';
import { DashboardCardComponent } from '../shared/ui/dashboard-card.component';
import { MetricTileComponent } from '../shared/ui/metric-tile.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { WorkspaceShellComponent } from '../shared/ui/workspace-shell.component';

type WizardStep = 0 | 1 | 2 | 3 | 4;

@Component({
  selector: 'app-seller-page',
  imports: [
    DashboardCardComponent,
    MetricTileComponent,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    WorkspaceShellComponent
  ],
  template: `
    <section class="page seller-onboarding">
      <app-workspace-shell>
        <app-seller-workspace-nav workspaceNav />

        <div class="page-header hf-seller-page-header">
          <div>
            <span class="eyebrow">{{ isVerified() ? 'Seller dashboard' : 'Seller onboarding' }}</span>
            <h1>{{ isVerified() ? (storeName()) : 'Seller workspace' }}</h1>
            <p>{{ isVerified() ? 'Manage daily operations, listing quality, payouts, support, ads, and analytics from one seller workspace.' : 'Complete the required setup details before submitting your seller profile for review.' }}</p>
          </div>
          <div class="auth-actions">
            @if (isVerified()) {
              <a data-ui-button="primary" routerLink="/products/new">Create product</a>
              <a data-ui-button="secondary" routerLink="/orders">Orders</a>
            } @else {
              <a data-ui-button="primary" routerLink="/products">Prepare drafts</a>
              <a data-ui-button="secondary" routerLink="/sell">Seller guide</a>
            }
          </div>
        </div>

        <div class="onboarding-status hf-seller-status-strip">
          <span class="status-pill">{{ onboarding()?.verificationStatus ?? 'Loading' }}</span>
          @if (onboarding()) {
            <span>{{ completionLabel() }}</span>
          }
        </div>

        @if (isLoading()) {
          <div class="route-card">Loading seller onboarding...</div>
        } @else if (isVerified()) {
          <div class="seller-dashboard hf-seller-dashboard">
            @if (dashboardErrorMessage()) {
              <p class="auth-alert error" role="alert">{{ dashboardErrorMessage() }}</p>
            }

            <section class="seller-dashboard-hero hf-seller-dashboard-hero">
              <div>
                <app-status-badge label="Verified seller" tone="success" />
                <h2>{{ storeName() }}</h2>
                <p>Use this workspace for the daily seller loop: create listings, fulfil orders, answer support, monitor payouts, and decide what to promote next.</p>
              </div>
              <div class="seller-dashboard-status hf-seller-quality-panel">
                <strong>{{ setupPercent() }}%</strong>
                <span>{{ onboarding()?.storefront?.isPublished ? 'Storefront published' : 'Storefront not published' }}</span>
                <a data-ui-button="secondary" routerLink="/products">Improve listings</a>
              </div>
            </section>

            <section class="hf-metric-grid" aria-label="Seller dashboard summary">
              @for (metric of dashboardMetrics(); track metric.label) {
                <app-metric-tile
                  [label]="metric.label"
                  [value]="metric.value"
                  [badge]="metric.badge"
                  [badgeTone]="metric.tone"
                />
              }
            </section>

            @if (isDashboardLoading()) {
              <div class="route-card">Loading live seller summary...</div>
            }

            <section class="hf-seller-dashboard-layout">
              <div class="hf-seller-queue-card">
                <div class="seller-products-header">
                  <div>
                    <span class="eyebrow">Operational queues</span>
                    <h2>What to check next</h2>
                  </div>
                  <a data-ui-button="secondary" routerLink="/orders">View orders</a>
                </div>

                @for (item of operationHighlights(); track item.route) {
                  <a class="hf-seller-queue-row" [routerLink]="item.route">
                    <span>{{ item.label }}</span>
                    <strong>{{ item.heading }}</strong>
                    <small>{{ item.description }}</small>
                  </a>
                }
              </div>

              <aside class="hf-seller-opportunity-card hf-seller-alert-panel">
                <span class="eyebrow">Operational alerts</span>
                <h2>Attention needed</h2>
                @if (dashboardSummary()?.alerts?.length) {
                  <div class="hf-seller-alert-list">
                    @for (alert of dashboardSummary()!.alerts; track alert.title) {
                      <a class="hf-seller-alert-row severity-{{ alert.severity }}" [routerLink]="alert.route">
                        <span>{{ alert.count }}</span>
                        <strong>{{ alert.title }}</strong>
                        <small>{{ alert.message }}</small>
                      </a>
                    }
                  </div>
                } @else {
                  <p>No urgent operational alerts. Keep checking orders, inventory, and support as new buyer activity arrives.</p>
                }
              </aside>
            </section>

            <section class="hf-seller-dashboard-layout">
              <div class="hf-seller-queue-card">
                <div class="seller-products-header">
                  <div>
                    <span class="eyebrow">Recent activity</span>
                    <h2>Latest seller events</h2>
                  </div>
                  <a data-ui-button="secondary" routerLink="/notifications">Notifications</a>
                </div>

                @if (dashboardSummary()?.recentActivity?.length) {
                  @for (activity of dashboardSummary()!.recentActivity; track activity.type + activity.route + activity.occurredAtUtc) {
                    <a class="hf-seller-queue-row" [routerLink]="activity.route">
                      <span>{{ activity.type }}</span>
                      <strong>{{ activity.title }}</strong>
                      <small>{{ activity.status }} - {{ renderDate(activity.occurredAtUtc) }}</small>
                    </a>
                  }
                } @else {
                  <p class="supporting-copy">No seller activity has been recorded yet. New orders, support tickets, product changes, returns, and ads will appear here.</p>
                }
              </div>

              <aside class="hf-seller-opportunity-card">
                <span class="eyebrow">AI opportunity</span>
                <h2>Improve product quality before spending on ads</h2>
                <p>Use the AI listing assistant to tighten titles, attributes, missing fields, and image alt text before submitting products or campaigns.</p>
                <div class="hf-progress-ring" aria-label="Listing quality emphasis"><strong>AI</strong></div>
                <a data-ui-button="primary" routerLink="/products/new">Open listing assistant</a>
              </aside>
            </section>

            <div class="seller-dashboard-grid hf-seller-dashboard-grid">
              @for (card of dashboardCards; track card.route) {
                <app-dashboard-card [eyebrow]="card.eyebrow" [heading]="card.heading" [description]="card.description">
                  <a data-ui-button="secondary" [routerLink]="card.route">{{ card.action }}</a>
                </app-dashboard-card>
              }
            </div>
          </div>
        } @else {
          @if (errorMessage()) {
            <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
          }

          @if (successMessage()) {
            <p class="auth-alert success" role="status">{{ successMessage() }}</p>
          }

          <section class="seller-verification-state-card state-{{ verificationStatusKey() }}">
            <div>
              <span class="eyebrow">{{ verificationEyebrow() }}</span>
              <h2>{{ verificationHeading() }}</h2>
              <p>{{ verificationDescription() }}</p>
              @if (verificationReason()) {
                <p class="auth-alert warning" role="status">{{ verificationReason() }}</p>
              }
              @if (verificationTimeline()) {
                <p class="seller-verification-timeline">{{ verificationTimeline() }}</p>
              }
            </div>
            <div class="seller-verification-actions">
              @if (canPrepareDrafts()) {
                <a data-ui-button="primary" routerLink="/products">Prepare product drafts</a>
              }
              <a data-ui-button="secondary" routerLink="/support">Contact support</a>
              <a data-ui-button="secondary" routerLink="/notifications">Notifications</a>
            </div>
          </section>

          <section class="seller-readiness-grid" aria-label="Seller onboarding readiness">
            @for (item of onboardingReadinessItems(); track item.label) {
              <article class="seller-readiness-card" [class.complete]="item.complete">
                <span>{{ item.complete ? 'Complete' : 'Needed' }}</span>
                <h3>{{ item.label }}</h3>
                <p>{{ item.summary }}</p>
              </article>
            }
          </section>

          <section class="route-card seller-evidence-card">
            <div class="seller-products-header">
              <div>
                <span class="eyebrow">Verification evidence</span>
                <h2>Supporting documents</h2>
                <p>Optional evidence helps reviewers understand business registration, identity, fulfilment address, and brand or product authenticity context.</p>
              </div>
              <app-status-badge
                [label]="verificationEvidence().length ? verificationEvidence().length + ' uploaded' : 'Optional'"
                [tone]="verificationEvidence().length ? 'success' : 'neutral'"
              />
            </div>

            @if (evidenceErrorMessage()) {
              <p class="auth-alert error" role="alert">{{ evidenceErrorMessage() }}</p>
            }
            @if (evidenceSuccessMessage()) {
              <p class="auth-alert success" role="status">{{ evidenceSuccessMessage() }}</p>
            }

            @if (canMutateEvidence()) {
              <form [formGroup]="evidenceForm" (ngSubmit)="uploadEvidence()" class="wizard-form evidence-upload-form" novalidate>
                <div class="form-grid">
                  <label class="ui-field">
                    <span>Evidence type</span>
                    <select formControlName="evidenceType">
                      @for (type of evidenceTypes; track type.value) {
                        <option [value]="type.value">{{ type.label }}</option>
                      }
                    </select>
                  </label>

                  <label class="ui-field">
                    <span>Note for reviewer</span>
                    <input formControlName="note" maxlength="500" />
                    @if (evidenceForm.controls.note.hasError('maxlength')) {
                      <span class="ui-field-error">Note cannot exceed 500 characters.</span>
                    }
                  </label>
                </div>

                <label class="seller-evidence-file-control">
                  <span>{{ selectedEvidenceFile()?.name ?? 'Choose PDF, JPEG, PNG, or WebP evidence file' }}</span>
                  <input type="file" accept="application/pdf,image/jpeg,image/png,image/webp" (change)="onEvidenceFileSelected($event)" />
                </label>

                <button data-ui-button="primary" type="submit" [disabled]="isEvidenceSaving() || !selectedEvidenceFile()">Upload evidence</button>
              </form>
            } @else {
              <p class="supporting-copy">Evidence is read-only for {{ onboarding()?.verificationStatus }} seller accounts.</p>
            }

            @if (isEvidenceLoading()) {
              <p class="supporting-copy">Loading evidence...</p>
            } @else if (verificationEvidence().length === 0) {
              <p class="supporting-copy">No evidence has been uploaded. This does not block submission, but it can help reviewers resolve questions faster.</p>
            } @else {
              <div class="seller-evidence-list">
                @for (item of verificationEvidence(); track item.evidenceId) {
                  <article class="seller-evidence-row">
                    <div>
                      <span>{{ evidenceTypeLabel(item.evidenceType) }}</span>
                      <h3>{{ item.originalFileName }}</h3>
                      <p>{{ item.note ?? 'No reviewer note provided.' }}</p>
                      <small>{{ renderFileSize(item.byteSize) }} - uploaded {{ renderDate(item.uploadedAtUtc) }}</small>
                    </div>
                    <div class="auth-actions">
                      <button data-ui-button="secondary" type="button" (click)="downloadEvidence(item)">Download</button>
                      @if (canMutateEvidence()) {
                        <button data-ui-button="secondary" type="button" [disabled]="isEvidenceSaving()" (click)="removeEvidence(item)">Remove</button>
                      }
                    </div>
                  </article>
                }
              </div>
            }
          </section>

          @if (showOnboardingWizard()) {
            <div class="wizard-layout">
            <nav class="wizard-steps" aria-label="Seller onboarding steps">
              @for (step of steps; track step.index) {
                <button
                  type="button"
                  [class.active]="currentStep() === step.index"
                  [class.complete]="isStepComplete(step.index)"
                  (click)="currentStep.set(step.index)"
                >
                  <span>{{ step.index + 1 }}</span>
                  {{ step.label }}
                </button>
              }
            </nav>

            <div class="wizard-panel">
              @switch (currentStep()) {
                @case (0) {
                  <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" class="wizard-form" novalidate>
                  <h2>Basic seller details</h2>
                  <label class="ui-field">
                    <span>Display name</span>
                    <input formControlName="displayName" />
                    @if (profileForm.controls.displayName.hasError('required')) {
                      <span class="ui-field-error">Display name is required.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Contact email</span>
                    <input type="email" formControlName="contactEmail" />
                    @if (profileForm.controls.contactEmail.hasError('required')) {
                      <span class="ui-field-error">Contact email is required.</span>
                    } @else if (profileForm.controls.contactEmail.hasError('email')) {
                      <span class="ui-field-error">Enter a valid email address.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Phone number</span>
                    <input formControlName="phoneNumber" />
                    @if (profileForm.controls.phoneNumber.hasError('required')) {
                      <span class="ui-field-error">Phone number is required.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Business type</span>
                    <select formControlName="businessType">
                      <option value="Individual">Individual</option>
                      <option value="RegisteredBusiness">Registered business</option>
                    </select>
                  </label>

                  <label class="ui-field">
                    <span>Business name</span>
                    <input formControlName="businessName" />
                    @if (profileForm.hasError('businessNameRequired')) {
                      <span class="ui-field-error">Business name is required for registered businesses.</span>
                    }
                  </label>

                  <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Save and continue</button>
                  </form>
                }
                @case (1) {
                  <form [formGroup]="storefrontForm" (ngSubmit)="saveStorefront()" class="wizard-form" novalidate>
                  <h2>Storefront details</h2>
                  <label class="ui-field">
                    <span>Store name</span>
                    <input formControlName="storeName" />
                    @if (storefrontForm.controls.storeName.hasError('required')) {
                      <span class="ui-field-error">Store name is required.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Store slug</span>
                    <input formControlName="slug" />
                    @if (storefrontForm.controls.slug.hasError('required')) {
                      <span class="ui-field-error">Store slug is required.</span>
                    } @else if (storefrontForm.controls.slug.hasError('pattern')) {
                      <span class="ui-field-error">Use lowercase letters, numbers, and hyphens.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Description</span>
                    <textarea rows="4" formControlName="description"></textarea>
                  </label>

                  <label class="ui-field">
                    <span>Logo URL</span>
                    <input formControlName="logoUrl" />
                  </label>

                  <label class="ui-field">
                    <span>Banner URL</span>
                    <input formControlName="bannerUrl" />
                  </label>

                  <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Save and continue</button>
                  </form>
                }
                @case (2) {
                  <form [formGroup]="addressForm" (ngSubmit)="saveAddress()" class="wizard-form" novalidate>
                  <h2>Address and fulfilment</h2>
                  <label class="ui-field">
                    <span>Address line 1</span>
                    <input formControlName="addressLine1" />
                    @if (addressForm.controls.addressLine1.hasError('required')) {
                      <span class="ui-field-error">Address line 1 is required.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Address line 2</span>
                    <input formControlName="addressLine2" />
                  </label>

                  <div class="form-grid">
                    <label class="ui-field">
                      <span>City</span>
                      <input formControlName="city" />
                      @if (addressForm.controls.city.hasError('required')) {
                        <span class="ui-field-error">City is required.</span>
                      }
                    </label>

                    <label class="ui-field">
                      <span>Province</span>
                      <input formControlName="province" />
                      @if (addressForm.controls.province.hasError('required')) {
                        <span class="ui-field-error">Province is required.</span>
                      }
                    </label>
                  </div>

                  <div class="form-grid">
                    <label class="ui-field">
                      <span>Postal code</span>
                      <input formControlName="postalCode" />
                      @if (addressForm.controls.postalCode.hasError('required')) {
                        <span class="ui-field-error">Postal code is required.</span>
                      }
                    </label>

                    <label class="ui-field">
                      <span>Country code</span>
                      <input maxlength="2" formControlName="countryCode" />
                      @if (addressForm.controls.countryCode.hasError('required')) {
                        <span class="ui-field-error">Country code is required.</span>
                      } @else if (addressForm.controls.countryCode.hasError('pattern')) {
                        <span class="ui-field-error">Use a 2-letter country code.</span>
                      }
                    </label>
                  </div>

                  <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Save and continue</button>
                  </form>
                }
                @case (3) {
                  <form [formGroup]="payoutForm" (ngSubmit)="savePayout()" class="wizard-form" novalidate>
                  <h2>Payout setup</h2>
                  <p>No bank details are stored here. Use a provider reference or setup token for now.</p>
                  <label class="ui-field">
                    <span>Payout provider reference</span>
                    <input formControlName="payoutProviderReference" />
                    @if (payoutForm.controls.payoutProviderReference.hasError('required')) {
                      <span class="ui-field-error">Payout provider reference is required.</span>
                    }
                  </label>

                  <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Save and continue</button>
                  </form>
                }
                @case (4) {
                  <div class="review-panel">
                  <h2>Review and submit</h2>
                  <div class="review-grid">
                    @for (item of onboardingReadinessItems(); track item.label) {
                      <article class="route-card">
                        <span class="status-pill">{{ item.complete ? 'Complete' : 'Missing' }}</span>
                        <h2>{{ item.label }}</h2>
                        <p>{{ item.summary }}</p>
                      </article>
                    }
                  </div>

                  <button
                    data-ui-button="primary"
                    type="button"
                    [disabled]="!onboarding()?.canSubmitForVerification || isSaving()"
                    (click)="submitVerification()"
                  >
                    Submit for verification
                  </button>

                  @if (!onboarding()?.canSubmitForVerification) {
                    <p>Complete every onboarding step before submitting for review.</p>
                  }
                  </div>
                }
              }
            </div>
          </div>
          } @else {
            <div class="route-card seller-locked-status-card">
              <span class="status-pill">{{ onboarding()?.verificationStatus }}</span>
              <h2>{{ lockedStatusHeading() }}</h2>
              <p>{{ lockedStatusDescription() }}</p>
              <div class="auth-secondary">
                @if (canPrepareDrafts()) {
                  <a routerLink="/products">Prepare drafts</a>
                }
                <a routerLink="/support">Open seller support</a>
                <a routerLink="/sell">Review seller requirements</a>
              </div>
            </div>
          }
        }
      </app-workspace-shell>
    </section>
  `
})
export class SellerPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly onboardingService = inject(SellerOnboardingService);
  private readonly dashboardService = inject(SellerDashboardService);
  private readonly evidenceService = inject(SellerVerificationEvidenceService);

  protected readonly onboarding = signal<SellerOnboardingResponse | null>(null);
  protected readonly dashboardSummary = signal<SellerDashboardSummaryResponse | null>(null);
  protected readonly verificationEvidence = signal<SellerVerificationEvidenceResponse[]>([]);
  protected readonly selectedEvidenceFile = signal<File | null>(null);
  protected readonly currentStep = signal<WizardStep>(0);
  protected readonly isLoading = signal(true);
  protected readonly isDashboardLoading = signal(false);
  protected readonly isEvidenceLoading = signal(false);
  protected readonly isEvidenceSaving = signal(false);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly dashboardErrorMessage = signal<string | null>(null);
  protected readonly evidenceErrorMessage = signal<string | null>(null);
  protected readonly evidenceSuccessMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly steps: readonly { index: WizardStep; label: string }[] = [
    { index: 0, label: 'Details' },
    { index: 1, label: 'Storefront' },
    { index: 2, label: 'Address' },
    { index: 3, label: 'Payout' },
    { index: 4, label: 'Review' }
  ];

  protected readonly evidenceTypes: readonly { value: SellerVerificationEvidenceType; label: string }[] = [
    { value: 'BusinessRegistration', label: 'Business registration' },
    { value: 'IdentityOrRepresentative', label: 'Identity or representative' },
    { value: 'FulfilmentAddress', label: 'Fulfilment address' },
    { value: 'BrandAuthorization', label: 'Brand authorization' },
    { value: 'ProductAuthenticity', label: 'Product authenticity' },
    { value: 'Other', label: 'Other evidence' }
  ];

  protected readonly dashboardCards: readonly {
    eyebrow: string;
    heading: string;
    description: string;
    route: string;
    action: string;
  }[] = [
    {
      eyebrow: 'Catalog',
      heading: 'Products',
      description: 'Manage drafts, review submissions, images, variants, stock, and listing quality.',
      route: '/products',
      action: 'Manage products'
    },
    {
      eyebrow: 'Stock',
      heading: 'Inventory',
      description: 'Adjust live variant stock, reserved-stock visibility, and sellable inventory status.',
      route: '/inventory',
      action: 'Manage inventory'
    },
    {
      eyebrow: 'Fulfilment',
      heading: 'Orders',
      description: 'Review seller orders, add tracking, and move orders through manual fulfilment.',
      route: '/orders',
      action: 'View orders'
    },
    {
      eyebrow: 'After-sales',
      heading: 'Returns',
      description: 'Respond to return requests and keep buyer-facing return decisions clear.',
      route: '/returns',
      action: 'Review returns'
    },
    {
      eyebrow: 'Finance',
      heading: 'Payouts',
      description: 'Read seller balances and payout history without exposing admin finance controls.',
      route: '/payouts',
      action: 'View payouts'
    },
    {
      eyebrow: 'Support',
      heading: 'Support',
      description: 'Create and follow seller support tickets linked to orders, products, or operations.',
      route: '/support',
      action: 'Open support'
    },
    {
      eyebrow: 'Growth',
      heading: 'Ads and analytics',
      description: 'Access existing advertising and aggregate analytics tools after core operations.',
      route: '/analytics',
      action: 'View analytics'
    },
    {
      eyebrow: 'Settings',
      heading: 'Store profile',
      description: 'Maintain storefront presentation and fulfilment address details after verification.',
      route: '/settings/store',
      action: 'Open settings'
    }
  ];

  protected readonly isVerified = computed(() => this.onboarding()?.verificationStatus === 'Verified');

  protected readonly completionLabel = computed(() => {
    const onboarding = this.onboarding();
    if (!onboarding) {
      return '';
    }

    const completed = [
      onboarding.isProfileComplete,
      onboarding.isStorefrontComplete,
      onboarding.isAddressComplete,
      onboarding.isPayoutPlaceholderComplete
    ].filter(Boolean).length;

    return `${completed} of 4 setup sections complete`;
  });

  protected readonly storeName = computed(() =>
    this.onboarding()?.storefront?.storeName
      ?? this.onboarding()?.profile?.displayName
      ?? 'Seller store');

  protected readonly setupPercent = computed(() => {
    const onboarding = this.onboarding();
    if (!onboarding) {
      return 0;
    }

    const completed = [
      onboarding.isProfileComplete,
      onboarding.isStorefrontComplete,
      onboarding.isAddressComplete,
      onboarding.isPayoutPlaceholderComplete
    ].filter(Boolean).length;

    return Math.round((completed / 4) * 100);
  });

  protected readonly dashboardMetrics = computed<readonly {
    label: string;
    value: string;
    badge: string;
    tone: StatusBadgeTone;
  }[]>(() => {
    const onboarding = this.onboarding();
    const summary = this.dashboardSummary();
    if (summary) {
      return [
        {
          label: 'Sales 30d',
          value: this.renderCurrency(summary.salesLast30Days),
          badge: `${summary.ordersLast30Days} orders`,
          tone: 'success'
        },
        {
          label: 'Fulfilment',
          value: summary.pendingFulfilmentOrders.toString(),
          badge: `${summary.deliveryExceptionOrderCount} exceptions`,
          tone: summary.deliveryExceptionOrderCount > 0 ? 'warning' : 'accent'
        },
        {
          label: 'Inventory',
          value: summary.lowStockProductCount.toString(),
          badge: `${summary.outOfStockVariantCount} out of stock`,
          tone: summary.lowStockProductCount > 0 || summary.outOfStockVariantCount > 0 ? 'warning' : 'neutral'
        },
        {
          label: 'Care',
          value: (summary.openReturnCount + summary.openSupportTicketCount + summary.activeDisputeCount).toString(),
          badge: 'returns, support, disputes',
          tone: summary.activeDisputeCount > 0 ? 'warning' : 'neutral'
        }
      ];
    }

    return [
      {
        label: 'Setup',
        value: `${this.setupPercent()}%`,
        badge: onboarding?.verificationStatus ?? 'Loading',
        tone: this.isVerified() ? 'success' : 'warning'
      },
      {
        label: 'Storefront',
        value: onboarding?.storefront?.isPublished ? 'Published' : 'Draft',
        badge: 'Buyer visibility',
        tone: onboarding?.storefront?.isPublished ? 'success' : 'warning'
      },
      {
        label: 'Operations',
        value: '4 queues',
        badge: 'Orders, returns, payouts, support',
        tone: 'accent'
      },
      {
        label: 'Growth',
        value: 'AI + Ads',
        badge: 'Listing quality first',
        tone: 'neutral'
      }
    ];
  });

  protected readonly operationHighlights = computed<readonly {
    label: string;
    heading: string;
    description: string;
    route: string;
  }[]>(() => {
    const summary = this.dashboardSummary();
    if (!summary) {
      return [
        {
          label: 'Orders',
          heading: 'Fulfil paid orders',
          description: 'Add tracking, mark shipped, and confirm delivery from the order workspace.',
          route: '/orders'
        },
        {
          label: 'Inventory',
          heading: 'Check low-stock variants',
          description: 'Keep published products sellable without reopening listing review.',
          route: '/inventory'
        },
        {
          label: 'Returns',
          heading: 'Review buyer requests',
          description: 'Respond clearly so refunds and disputes do not drift.',
          route: '/returns'
        },
        {
          label: 'Support',
          heading: 'Keep tickets moving',
          description: 'Use support threads for operational issues and buyer follow-up.',
          route: '/support'
        }
      ];
    }

    return [
      {
        label: 'Orders',
        heading: `${summary.pendingFulfilmentOrders} orders need fulfilment`,
        description: `${summary.paidOrderCount} paid, ${summary.processingOrderCount} processing, ${summary.readyToShipOrderCount} ready to ship, ${summary.deliveryExceptionOrderCount} exceptions.`,
        route: '/orders'
      },
      {
        label: 'Inventory',
        heading: `${summary.lowStockProductCount} low-stock products`,
        description: `${summary.outOfStockVariantCount} variants are out of stock and ${summary.reservedStockCount} units are reserved.`,
        route: '/inventory'
      },
      {
        label: 'Returns',
        heading: `${summary.returnsAwaitingSellerResponseCount} returns await response`,
        description: `${summary.openReturnCount} open returns and ${summary.activeDisputeCount} active disputes across the seller account.`,
        route: '/returns'
      },
      {
        label: 'Support',
        heading: `${summary.openSupportTicketCount} support tickets open`,
        description: 'Keep buyer and operational support threads moving from the support queue.',
        route: '/support'
      },
      {
        label: 'Updates',
        heading: `${summary.unreadNotificationCount} unread seller updates`,
        description: `${summary.pendingAdReviewCount} ad campaigns are in review. Use updates to catch moderation and approval outcomes.`,
        route: summary.unreadNotificationCount > 0 ? '/notifications' : '/ads'
      }
    ];
  });

  protected showOnboardingWizard(): boolean {
    const status = this.onboarding()?.verificationStatus;
    return status === 'PendingVerification' || status === 'Rejected';
  }

  protected canPrepareDrafts(): boolean {
    return this.onboarding()?.verificationStatus !== 'Suspended';
  }

  protected verificationStatusKey(): string {
    return (this.onboarding()?.verificationStatus ?? 'loading').toLowerCase();
  }

  protected verificationEyebrow(): string {
    const status = this.onboarding()?.verificationStatus;
    if (status === 'UnderReview') {
      return 'Application submitted';
    }

    if (status === 'Rejected') {
      return 'Action required';
    }

    if (status === 'Suspended') {
      return 'Account restricted';
    }

    return 'Application setup';
  }

  protected verificationHeading(): string {
    const status = this.onboarding()?.verificationStatus;
    if (status === 'UnderReview') {
      return 'Your seller profile is under review';
    }

    if (status === 'Rejected') {
      return 'Update your application and resubmit';
    }

    if (status === 'Suspended') {
      return 'Seller account suspended';
    }

    return 'Complete onboarding to apply';
  }

  protected verificationDescription(): string {
    const status = this.onboarding()?.verificationStatus;
    if (status === 'UnderReview') {
      return 'Mabuntle is reviewing your storefront, fulfilment details, and payout reference. You can prepare product drafts now, but product submission, publishing, and ads unlock after verification.';
    }

    if (status === 'Rejected') {
      return 'Review the reason below, update the relevant onboarding sections, then submit again when the application is ready.';
    }

    if (status === 'Suspended') {
      return 'This seller account cannot publish products, run ads, or receive new operational privileges until support or admin review resolves the restriction.';
    }

    return 'Add the minimum seller details Mabuntle needs to review your storefront before products and ads can go live.';
  }

  protected verificationReason(): string | null {
    const review = this.onboarding()?.latestVerificationReview;
    const status = this.onboarding()?.verificationStatus;
    if (status === 'Rejected' && review?.rejectionReason) {
      return `Review reason: ${review.rejectionReason}`;
    }

    if (status === 'Suspended' && review?.suspensionReason) {
      return `Suspension reason: ${review.suspensionReason}`;
    }

    return null;
  }

  protected verificationTimeline(): string | null {
    const review = this.onboarding()?.latestVerificationReview;
    const status = this.onboarding()?.verificationStatus;
    if (!review) {
      return null;
    }

    if (status === 'UnderReview' && review.submittedAtUtc) {
      return `Submitted for review on ${this.renderDate(review.submittedAtUtc)}.`;
    }

    if ((status === 'Rejected' || status === 'Verified') && review.reviewedAtUtc) {
      return `Reviewed on ${this.renderDate(review.reviewedAtUtc)}.`;
    }

    return null;
  }

  protected lockedStatusHeading(): string {
    return this.onboarding()?.verificationStatus === 'Suspended'
      ? 'Contact support before continuing'
      : 'Verification is the publishing gate';
  }

  protected lockedStatusDescription(): string {
    return this.onboarding()?.verificationStatus === 'Suspended'
      ? 'Use seller support to resolve the account restriction. Existing notifications remain available for review.'
      : 'Your submitted profile is waiting for admin review. Draft preparation remains available, but marketplace publishing stays locked until approval.';
  }

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

  protected readonly payoutForm = this.formBuilder.group({
    payoutProviderReference: ['', [Validators.required]]
  });

  protected readonly evidenceForm = this.formBuilder.group({
    evidenceType: ['BusinessRegistration' as SellerVerificationEvidenceType, [Validators.required]],
    note: ['', [Validators.maxLength(500)]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadOnboarding();
  }

  protected async saveProfile(): Promise<void> {
    if (!this.ensureValid(this.profileForm)) {
      return;
    }

    const value = this.profileForm.getRawValue();
    await this.saveStep(
      () => this.onboardingService.updateProfile({
        displayName: value.displayName,
        contactEmail: value.contactEmail,
        phoneNumber: value.phoneNumber,
        businessType: value.businessType,
        businessName: emptyToNull(value.businessName)
      } satisfies UpdateSellerProfileRequest),
      1);
  }

  protected async saveStorefront(): Promise<void> {
    if (!this.ensureValid(this.storefrontForm)) {
      return;
    }

    const value = this.storefrontForm.getRawValue();
    await this.saveStep(
      () => this.onboardingService.updateStorefront({
        storeName: value.storeName,
        slug: value.slug,
        description: emptyToNull(value.description),
        logoUrl: emptyToNull(value.logoUrl),
        bannerUrl: emptyToNull(value.bannerUrl)
      } satisfies UpdateSellerStorefrontRequest),
      2);
  }

  protected async saveAddress(): Promise<void> {
    if (!this.ensureValid(this.addressForm)) {
      return;
    }

    const value = this.addressForm.getRawValue();
    await this.saveStep(
      () => this.onboardingService.updateAddress({
        addressLine1: value.addressLine1,
        addressLine2: emptyToNull(value.addressLine2),
        city: value.city,
        province: value.province,
        postalCode: value.postalCode,
        countryCode: value.countryCode.toUpperCase()
      } satisfies UpdateSellerAddressRequest),
      3);
  }

  protected async savePayout(): Promise<void> {
    if (!this.ensureValid(this.payoutForm)) {
      return;
    }

    const value = this.payoutForm.getRawValue();
    await this.saveStep(
      () => this.onboardingService.updatePayout({
        payoutProviderReference: value.payoutProviderReference
      } satisfies UpdateSellerPayoutRequest),
      4);
  }

  protected async submitVerification(): Promise<void> {
    await this.saveStep(
      () => this.onboardingService.submitVerification(),
      4,
      'Seller profile submitted for verification.');
  }

  protected renderCurrency(amount: number): string {
    return new Intl.NumberFormat('en-ZA', {
      style: 'currency',
      currency: 'ZAR',
      maximumFractionDigits: 2
    }).format(amount);
  }

  protected renderDate(value: string): string {
    return new Intl.DateTimeFormat('en-ZA', {
      day: '2-digit',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit'
    }).format(new Date(value));
  }

  protected renderFileSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }

    if (bytes < 1024 * 1024) {
      return `${Math.round(bytes / 1024)} KB`;
    }

    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  protected evidenceTypeLabel(type: SellerVerificationEvidenceType): string {
    return this.evidenceTypes.find(item => item.value === type)?.label ?? type;
  }

  protected canMutateEvidence(): boolean {
    const status = this.onboarding()?.verificationStatus;
    return status === 'PendingVerification' || status === 'UnderReview' || status === 'Rejected';
  }

  protected onEvidenceFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedEvidenceFile.set(input.files?.[0] ?? null);
    this.evidenceErrorMessage.set(null);
    this.evidenceSuccessMessage.set(null);
  }

  protected async uploadEvidence(): Promise<void> {
    if (!this.ensureValid(this.evidenceForm) || this.isEvidenceSaving()) {
      return;
    }

    const file = this.selectedEvidenceFile();
    if (!file) {
      this.evidenceErrorMessage.set('Choose an evidence file before uploading.');
      return;
    }

    const value = this.evidenceForm.getRawValue();
    this.isEvidenceSaving.set(true);
    this.evidenceErrorMessage.set(null);
    this.evidenceSuccessMessage.set(null);

    try {
      await this.evidenceService.upload(file, value.evidenceType, emptyToNull(value.note));
      this.selectedEvidenceFile.set(null);
      this.evidenceForm.patchValue({ note: '' });
      this.evidenceSuccessMessage.set('Evidence uploaded for admin review context.');
      await this.loadEvidence();
    } catch (error) {
      this.evidenceErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isEvidenceSaving.set(false);
    }
  }

  protected async removeEvidence(item: SellerVerificationEvidenceResponse): Promise<void> {
    this.isEvidenceSaving.set(true);
    this.evidenceErrorMessage.set(null);
    this.evidenceSuccessMessage.set(null);

    try {
      await this.evidenceService.remove(item.evidenceId);
      this.evidenceSuccessMessage.set('Evidence removed.');
      await this.loadEvidence();
    } catch (error) {
      this.evidenceErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isEvidenceSaving.set(false);
    }
  }

  protected async downloadEvidence(item: SellerVerificationEvidenceResponse): Promise<void> {
    this.evidenceErrorMessage.set(null);
    try {
      const blob = await this.evidenceService.download(item.evidenceId);
      triggerBrowserDownload(blob, item.originalFileName);
    } catch (error) {
      this.evidenceErrorMessage.set(getApiErrorMessage(error));
    }
  }

  protected isStepComplete(step: WizardStep): boolean {
    const onboarding = this.onboarding();
    if (!onboarding) {
      return false;
    }

    return [
      onboarding.isProfileComplete,
      onboarding.isStorefrontComplete,
      onboarding.isAddressComplete,
      onboarding.isPayoutPlaceholderComplete,
      onboarding.verificationStatus === 'UnderReview' || onboarding.verificationStatus === 'Verified'
    ][step];
  }

  protected onboardingReadinessItems(): readonly { label: string; complete: boolean; summary: string }[] {
    const onboarding = this.onboarding();
    const submitted = onboarding?.verificationStatus === 'UnderReview'
      || onboarding?.verificationStatus === 'Verified';

    return [
      {
        label: 'Profile',
        complete: onboarding?.isProfileComplete ?? false,
        summary: onboarding?.profile.displayName ?? 'Seller profile details are required.'
      },
      {
        label: 'Storefront',
        complete: onboarding?.isStorefrontComplete ?? false,
        summary: onboarding?.storefront?.storeName ?? 'Storefront name and slug are required.'
      },
      {
        label: 'Address',
        complete: onboarding?.isAddressComplete ?? false,
        summary: onboarding?.address?.city ?? 'Fulfilment address is required.'
      },
      {
        label: 'Payout',
        complete: onboarding?.isPayoutPlaceholderComplete ?? false,
        summary: onboarding?.payout?.payoutProviderReference ?? 'A provider reference is required.'
      },
      {
        label: 'Review',
        complete: submitted,
        summary: submitted
          ? 'Your seller application has been submitted for Mabuntle review.'
          : 'Submit the completed application when every setup section is ready.'
      }
    ];
  }

  private async loadOnboarding(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const onboarding = await this.onboardingService.getOnboarding();
      this.setOnboarding(onboarding);
      await this.loadEvidence();
      if (onboarding.verificationStatus === 'Verified') {
        await this.loadDashboardSummary();
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async saveStep(
    action: () => Promise<SellerOnboardingResponse>,
    nextStep: WizardStep,
    successMessage = 'Saved.')
  {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSaving.set(true);

    try {
      const onboarding = await action();
      this.setOnboarding(onboarding);
      if (onboarding.verificationStatus === 'Verified') {
        await this.loadDashboardSummary();
      }
      this.currentStep.set(nextStep);
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private setOnboarding(onboarding: SellerOnboardingResponse): void {
    this.onboarding.set(onboarding);
    if (onboarding.verificationStatus !== 'Verified') {
      this.dashboardSummary.set(null);
      this.dashboardErrorMessage.set(null);
    }

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

    this.payoutForm.patchValue({
      payoutProviderReference: onboarding.payout?.payoutProviderReference ?? ''
    });
  }

  private async loadDashboardSummary(): Promise<void> {
    this.isDashboardLoading.set(true);
    this.dashboardErrorMessage.set(null);

    try {
      this.dashboardSummary.set(await this.dashboardService.getSummary());
    } catch (error) {
      this.dashboardSummary.set(null);
      this.dashboardErrorMessage.set(`${getApiErrorMessage(error)} Workspace links remain available below.`);
    } finally {
      this.isDashboardLoading.set(false);
    }
  }

  private async loadEvidence(): Promise<void> {
    this.isEvidenceLoading.set(true);
    this.evidenceErrorMessage.set(null);

    try {
      this.verificationEvidence.set(await this.evidenceService.list());
    } catch (error) {
      this.verificationEvidence.set([]);
      this.evidenceErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isEvidenceLoading.set(false);
    }
  }

  private ensureValid(control: AbstractControl): boolean {
    if (control.invalid || this.isSaving()) {
      control.markAllAsTouched();
      return false;
    }

    return true;
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

function triggerBrowserDownload(blob: Blob, fileName: string): void {
  if (typeof document === 'undefined') {
    return;
  }

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
