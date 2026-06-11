import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminSellerDetailResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { getApiErrorMessage } from '../auth/api-error';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';
import { SellerVerificationEvidenceType } from '../seller/seller-verification-evidence.models';

@Component({
  selector: 'app-admin-seller-detail-page',
  imports: [
    AdminWorkspaceNavComponent,
    DatePipe,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />
      <a class="admin-back-link" routerLink="/sellers">Back to seller approvals</a>

      @if (isLoading()) {
        <div class="route-card">Loading seller review...</div>
      } @else if (seller()) {
        <app-page-header
          eyebrow="Seller review"
          [heading]="seller()?.displayName ?? seller()?.storefront?.storeName ?? 'Seller review'"
          [description]="seller()?.contactEmail ?? 'No contact email'"
        >
          <div pageHeaderActions>
            <app-status-badge [label]="seller()!.verificationStatus" [tone]="sellerStatusTone(seller()!.verificationStatus)" />
          </div>
        </app-page-header>

        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <article class="route-card admin-detail-card">
              <app-status-badge [label]="seller()!.verificationStatus" [tone]="sellerStatusTone(seller()!.verificationStatus)" />
              <h2>Profile</h2>
              <dl class="admin-facts">
                <div><dt>Business type</dt><dd>{{ seller()?.businessType ?? 'Not provided' }}</dd></div>
                <div><dt>Business name</dt><dd>{{ seller()?.businessName ?? 'Not provided' }}</dd></div>
                <div><dt>Phone</dt><dd>{{ seller()?.phoneNumber ?? 'Not provided' }}</dd></div>
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Review completeness</h2>
              <div class="admin-checklist">
                @for (item of completenessItems(); track item.label) {
                  <div>
                    <app-status-badge [label]="item.isComplete ? 'Ready' : 'Missing'" [tone]="item.isComplete ? 'success' : 'warning'" />
                    <span>{{ item.label }}</span>
                    <small>{{ item.description }}</small>
                  </div>
                }
              </div>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Storefront</h2>
              <dl class="admin-facts">
                <div><dt>Store name</dt><dd>{{ seller()?.storefront?.storeName ?? 'Not provided' }}</dd></div>
                <div><dt>Slug</dt><dd>{{ seller()?.storefront?.slug ?? 'Not provided' }}</dd></div>
                <div><dt>Description</dt><dd>{{ seller()?.storefront?.description ?? 'Not provided' }}</dd></div>
                <div><dt>Published</dt><dd>{{ seller()?.storefront?.isPublished ? 'Yes' : 'No' }}</dd></div>
                <div><dt>Logo</dt><dd>{{ seller()?.storefront?.logoUrl ? 'Provided' : 'Not provided' }}</dd></div>
                <div><dt>Banner</dt><dd>{{ seller()?.storefront?.bannerUrl ? 'Provided' : 'Not provided' }}</dd></div>
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Address</h2>
              <dl class="admin-facts">
                <div><dt>Line 1</dt><dd>{{ seller()?.address?.addressLine1 ?? 'Not provided' }}</dd></div>
                <div><dt>Line 2</dt><dd>{{ seller()?.address?.addressLine2 ?? 'Not provided' }}</dd></div>
                <div><dt>City</dt><dd>{{ seller()?.address?.city ?? 'Not provided' }}</dd></div>
                <div><dt>Province</dt><dd>{{ seller()?.address?.province ?? 'Not provided' }}</dd></div>
                <div><dt>Postal code</dt><dd>{{ seller()?.address?.postalCode ?? 'Not provided' }}</dd></div>
                <div><dt>Country</dt><dd>{{ seller()?.address?.countryCode ?? 'Not provided' }}</dd></div>
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Payout setup</h2>
              <dl class="admin-facts">
                <div><dt>Provider reference</dt><dd>{{ seller()?.payout?.payoutProviderReference ?? 'Not provided' }}</dd></div>
                <div><dt>Submitted</dt><dd>{{ seller()?.payout?.hasSubmittedPlaceholder ? 'Yes' : 'No' }}</dd></div>
                <div><dt>Admin approved</dt><dd>{{ seller()?.payout?.isAdminApproved ? 'Yes' : 'No' }}</dd></div>
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Store policies</h2>
              <app-status-badge
                [label]="seller()!.storePolicy.isComplete ? 'Policy complete' : 'Policy context missing'"
                [tone]="seller()!.storePolicy.isComplete ? 'success' : 'warning'"
              />
              @if (!seller()!.storePolicy.isComplete) {
                <p>Missing: {{ seller()!.storePolicy.missingFields.join(', ') }}.</p>
              }
              @if (storePolicyEntries().length > 0) {
                <dl class="admin-facts">
                  @for (entry of storePolicyEntries(); track entry.label) {
                    <div><dt>{{ entry.label }}</dt><dd>{{ entry.value }}</dd></div>
                  }
                </dl>
              } @else {
                <app-ui-alert tone="info">No buyer-facing store policy has been saved yet. This is review context only and does not block admin actions.</app-ui-alert>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Verification evidence</h2>
              @if ((seller()?.verificationEvidence?.length ?? 0) === 0) {
                <app-ui-alert tone="info">No supporting evidence has been uploaded. Evidence is optional context and does not change approval rules.</app-ui-alert>
              } @else {
                <div class="admin-evidence-list">
                  @for (item of seller()?.verificationEvidence; track item.evidenceId) {
                    <div class="admin-evidence-row">
                      <div>
                        <app-status-badge [label]="evidenceTypeLabel(item.evidenceType)" tone="neutral" />
                        <h3>{{ item.originalFileName }}</h3>
                        <p>{{ item.note ?? 'No reviewer note provided.' }}</p>
                        <small>{{ renderFileSize(item.byteSize) }} - uploaded {{ item.uploadedAtUtc | date:'medium' }}</small>
                      </div>
                      <button data-ui-button="secondary" type="button" (click)="downloadEvidence(item.evidenceId, item.originalFileName)">Download</button>
                    </div>
                  }
                </div>
              }
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card">
              <h2>Review actions</h2>
              <button data-ui-button="primary" type="button" [disabled]="isSaving()" (click)="approve()">Approve seller</button>

              <form [formGroup]="rejectForm" (ngSubmit)="reject()" class="admin-reason-form" novalidate>
                <label class="ui-field">
                  <span>Rejection reason</span>
                  <textarea rows="3" formControlName="reason"></textarea>
                  @if (rejectForm.controls.reason.hasError('required')) {
                    <span class="ui-field-error">Reason is required.</span>
                  }
                </label>
                <button data-ui-button="secondary" type="submit" [disabled]="isSaving()">Reject seller</button>
              </form>

              <form [formGroup]="suspendForm" (ngSubmit)="suspend()" class="admin-reason-form" novalidate>
                <label class="ui-field">
                  <span>Suspension reason</span>
                  <textarea rows="3" formControlName="reason"></textarea>
                  @if (suspendForm.controls.reason.hasError('required')) {
                    <span class="ui-field-error">Reason is required.</span>
                  }
                </label>
                <button data-ui-button="secondary" type="submit" [disabled]="isSaving()">Suspend seller</button>
              </form>
            </div>

            <div class="route-card admin-action-card">
              <h2>Audit trail</h2>
              @if ((seller()?.auditTrail?.length ?? 0) === 0) {
                <p>No admin actions have been recorded for this seller.</p>
              } @else {
                <ol class="audit-list">
                  @for (entry of seller()?.auditTrail; track entry.id) {
                    <li>
                      <strong>{{ entry.actionType }}</strong>
                      <span>{{ entry.createdAtUtc | date:'medium' }}</span>
                      <span>{{ entry.actorRole ?? 'Admin' }}</span>
                      @if (entry.reason) {
                        <p>{{ entry.reason }}</p>
                      }
                    </li>
                  }
                </ol>
              }
            </div>
          </aside>
        </div>
      } @else {
        <app-ui-alert tone="error">{{ errorMessage() ?? 'Seller was not found.' }}</app-ui-alert>
      }
    </section>
  `
})
export class AdminSellerDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminSellerService = inject(AdminSellerService);

  protected readonly seller = signal<AdminSellerDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly rejectForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  protected readonly suspendForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadSeller();
  }

  protected async approve(): Promise<void> {
    const seller = this.seller();
    if (!seller) {
      return;
    }

    await this.runAction(
      () => this.adminSellerService.approveSeller(seller.sellerId),
      'Seller approved.');
  }

  protected async reject(): Promise<void> {
    if (this.rejectForm.invalid) {
      this.rejectForm.markAllAsTouched();
      return;
    }

    const seller = this.seller();
    if (!seller) {
      return;
    }

    const reason = this.rejectForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminSellerService.rejectSeller(seller.sellerId, { reason }),
      'Seller rejected.');
    this.rejectForm.reset();
  }

  protected async suspend(): Promise<void> {
    if (this.suspendForm.invalid) {
      this.suspendForm.markAllAsTouched();
      return;
    }

    const seller = this.seller();
    if (!seller) {
      return;
    }

    const reason = this.suspendForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminSellerService.suspendSeller(seller.sellerId, { reason }),
      'Seller suspended.');
    this.suspendForm.reset();
  }

  protected completenessItems(): { label: string; description: string; isComplete: boolean }[] {
    const seller = this.seller();
    if (!seller) {
      return [];
    }

    return [
      {
        label: 'Profile',
        description: 'Business type, business name, contact email, and phone are present.',
        isComplete: Boolean(seller.businessType && seller.businessName && seller.contactEmail && seller.phoneNumber)
      },
      {
        label: 'Storefront',
        description: 'Store name, slug, description, and publication state are available for review.',
        isComplete: Boolean(seller.storefront?.storeName && seller.storefront.slug && seller.storefront.description)
      },
      {
        label: 'Address',
        description: 'Primary address, city, province, postal code, and country are present.',
        isComplete: Boolean(seller.address?.addressLine1 && seller.address.city && seller.address.province && seller.address.postalCode && seller.address.countryCode)
      },
      {
        label: 'Payout setup',
        description: 'Provider reference placeholder was submitted for admin approval.',
        isComplete: Boolean(seller.payout?.payoutProviderReference && seller.payout.hasSubmittedPlaceholder)
      },
      {
        label: 'Store policies',
        description: 'Return, exchange, fulfilment, and support policy context is available for buyer-facing screens.',
        isComplete: seller.storePolicy.isComplete
      },
      {
        label: 'Verification evidence',
        description: 'Supporting documents or images were uploaded for admin review context.',
        isComplete: (seller.verificationEvidence?.length ?? 0) > 0
      }
    ];
  }

  protected storePolicyEntries(): { label: string; value: string }[] {
    const policy = this.seller()?.storePolicy;
    if (!policy) {
      return [];
    }

    return [
      policy.returnWindowDays === null ? null : { label: 'Return window', value: `${policy.returnWindowDays} day${policy.returnWindowDays === 1 ? '' : 's'}` },
      policy.returnPolicy ? { label: 'Returns', value: policy.returnPolicy } : null,
      policy.exchangePolicy ? { label: 'Exchanges', value: policy.exchangePolicy } : null,
      policy.fulfilmentPolicy ? { label: 'Fulfilment', value: policy.fulfilmentPolicy } : null,
      policy.supportPolicy ? { label: 'Support', value: policy.supportPolicy } : null,
      policy.careInstructions ? { label: 'Care', value: policy.careInstructions } : null,
      policy.productDisclaimer ? { label: 'Disclaimer', value: policy.productDisclaimer } : null
    ].filter((entry): entry is { label: string; value: string } => entry !== null);
  }

  protected sellerStatusTone(status: string): StatusBadgeTone {
    if (['Verified', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'Suspended'].includes(status)) {
      return 'danger';
    }

    return 'warning';
  }

  protected evidenceTypeLabel(type: SellerVerificationEvidenceType): string {
    const labels: Record<SellerVerificationEvidenceType, string> = {
      BusinessRegistration: 'Business registration',
      IdentityOrRepresentative: 'Identity or representative',
      FulfilmentAddress: 'Fulfilment address',
      BrandAuthorization: 'Brand authorization',
      ProductAuthenticity: 'Product authenticity',
      Other: 'Other'
    };

    return labels[type] ?? type;
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

  protected async downloadEvidence(evidenceId: string, fileName: string): Promise<void> {
    const seller = this.seller();
    if (!seller) {
      return;
    }

    this.errorMessage.set(null);
    try {
      const blob = await this.adminSellerService.downloadVerificationEvidence(seller.sellerId, evidenceId);
      triggerBrowserDownload(blob, fileName);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    }
  }

  private async loadSeller(): Promise<void> {
    const sellerId = this.route.snapshot.paramMap.get('sellerId');
    if (!sellerId) {
      this.errorMessage.set('Seller id is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.seller.set(await this.adminSellerService.getSeller(sellerId));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.seller.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(
    action: () => Promise<AdminSellerDetailResponse>,
    message: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.seller.set(await action());
      this.successMessage.set(message);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }
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
