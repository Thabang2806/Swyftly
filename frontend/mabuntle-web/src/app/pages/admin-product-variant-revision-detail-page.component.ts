import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import {
  AdminProductVariantRevisionDetailResponse,
  AdminProductVariantRevisionFinalVariantResponse
} from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { getApiErrorMessage } from '../auth/api-error';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-product-variant-revision-detail-page',
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
      <a class="admin-back-link" routerLink="/products">Back to product queue</a>

      @if (isLoading()) {
        <div class="route-card">Loading variant revision...</div>
      } @else if (revision()) {
        <app-page-header
          eyebrow="Variant and pricing revision"
          [heading]="revision()!.productTitle ?? 'Published product variants'"
          description="Review staged SKU, size, colour, price, barcode, addition, and deactivation changes before they affect live buyers."
        >
          <div pageHeaderActions>
            <app-status-badge [label]="revision()!.status" [tone]="statusTone(revision()!.status)" />
          </div>
        </app-page-header>

        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }
        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (validationMessages().length > 0) {
          <app-ui-alert tone="warning">
            <strong>Validation warnings</strong>
            <ul>
              @for (message of validationMessages(); track message) {
                <li>{{ message }}</li>
              }
            </ul>
          </app-ui-alert>
        }

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <div class="revision-compare-grid">
              <article class="route-card admin-detail-card">
                <h2>Current live variants</h2>
                <div class="admin-table compact" role="table" aria-label="Current live variants">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Variant</span>
                    <span role="columnheader">Price</span>
                    <span role="columnheader">Stock</span>
                    <span role="columnheader">Status</span>
                  </div>
                  @for (variant of revision()!.currentVariants; track variantKey(variant)) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">
                        <strong>{{ variant.sku }}</strong>
                        <small>{{ variant.size }} / {{ variant.colour }} {{ variant.barcode ? '/ ' + variant.barcode : '' }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ renderAmount(variant.price) }}</strong>
                        <small>{{ variant.compareAtPrice ? 'Compare ' + renderAmount(variant.compareAtPrice) : 'No compare price' }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ variant.stockQuantity }} stock</strong>
                        <small>{{ variant.reservedQuantity }} reserved</small>
                      </span>
                      <span role="cell">
                        <app-status-badge [label]="variant.status" [tone]="variant.status === 'Active' ? 'success' : 'warning'" />
                      </span>
                    </div>
                  } @empty {
                    <p>No live variants were found.</p>
                  }
                </div>
              </article>

              <article class="route-card admin-detail-card">
                <h2>Proposed final variants</h2>
                <div class="admin-table compact" role="table" aria-label="Proposed final variants">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Variant</span>
                    <span role="columnheader">Price</span>
                    <span role="columnheader">Stock</span>
                    <span role="columnheader">Change</span>
                  </div>
                  @for (variant of revision()!.proposedFinalVariants; track variantKey(variant)) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">
                        <strong>{{ variant.sku }}</strong>
                        <small>{{ variant.size }} / {{ variant.colour }} {{ variant.barcode ? '/ ' + variant.barcode : '' }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ renderAmount(variant.price) }}</strong>
                        <small>{{ variant.compareAtPrice ? 'Compare ' + renderAmount(variant.compareAtPrice) : 'No compare price' }}</small>
                      </span>
                      <span role="cell">
                        <strong>{{ variant.stockQuantity }} stock</strong>
                        <small>{{ variant.reservedQuantity }} reserved</small>
                      </span>
                      <span role="cell">
                        <app-status-badge [label]="variant.changeType" [tone]="changeTone(variant.changeType)" />
                        <small>{{ variant.status }}</small>
                      </span>
                    </div>
                  } @empty {
                    <p>No proposed variants were provided.</p>
                  }
                </div>
              </article>
            </div>

            <article class="route-card admin-detail-card">
              <h2>Staged changes</h2>
              <div class="admin-table" role="table" aria-label="Staged variant changes">
                <div class="admin-table-row heading" role="row">
                  <span role="columnheader">Operation</span>
                  <span role="columnheader">Variant</span>
                  <span role="columnheader">Price</span>
                  <span role="columnheader">Initial stock</span>
                </div>
                @for (item of revision()!.items; track item.revisionItemId) {
                  <div class="admin-table-row" role="row">
                    <span role="cell">
                      <app-status-badge [label]="item.operation" [tone]="changeTone(item.operation)" />
                      <small>{{ item.proposedStatus }}</small>
                    </span>
                    <span role="cell">
                      <strong>{{ item.sku }}</strong>
                      <small>{{ item.size }} / {{ item.colour }} {{ item.barcode ? '/ ' + item.barcode : '' }}</small>
                    </span>
                    <span role="cell">
                      <strong>{{ renderAmount(item.price) }}</strong>
                      <small>{{ item.compareAtPrice ? 'Compare ' + renderAmount(item.compareAtPrice) : 'No compare price' }}</small>
                    </span>
                    <span role="cell">
                      <strong>{{ item.initialStockQuantity ?? 'Existing stock' }}</strong>
                    </span>
                  </div>
                }
              </div>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Audit trail</h2>
              @for (entry of revision()!.auditTrail; track entry.id) {
                <p><strong>{{ entry.actionType }}</strong> - {{ entry.createdAtUtc | date:'medium' }} {{ entry.reason ?? '' }}</p>
              } @empty {
                <p>No revision moderation actions have been recorded.</p>
              }
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card">
              <h2>Review actions</h2>
              <dl class="admin-facts">
                <div><dt>Seller</dt><dd>{{ revision()!.seller.displayName ?? 'Unnamed seller' }}</dd></div>
                <div><dt>Contact</dt><dd>{{ revision()!.seller.contactEmail ?? 'No email' }}</dd></div>
                <div><dt>Submitted</dt><dd>{{ revision()!.submittedAtUtc ? (revision()!.submittedAtUtc | date:'medium') : 'Not submitted' }}</dd></div>
                <div><dt>Seller reason</dt><dd>{{ revision()!.sellerReason ?? 'No reason supplied' }}</dd></div>
              </dl>
              <p>Approval applies variant updates atomically, refreshes active cart snapshots, and leaves historical order items unchanged.</p>
              <button data-ui-button="primary" type="button" [disabled]="isSaving() || revision()!.status !== 'PendingReview'" (click)="approve()">Approve variant revision</button>

              <form [formGroup]="rejectForm" (ngSubmit)="reject()" class="admin-reason-form" novalidate>
                <label class="ui-field">
                  <span>Rejection reason</span>
                  <textarea rows="3" formControlName="reason"></textarea>
                  @if (rejectForm.controls.reason.hasError('required')) {
                    <span class="ui-field-error">Reason is required.</span>
                  }
                </label>
                <button data-ui-button="secondary" type="submit" [disabled]="isSaving() || revision()!.status !== 'PendingReview'">Reject variant revision</button>
              </form>
            </div>
          </aside>
        </div>
      } @else {
        <app-ui-alert tone="error">{{ errorMessage() ?? 'Variant revision was not found.' }}</app-ui-alert>
      }
    </section>
  `
})
export class AdminProductVariantRevisionDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminProductService = inject(AdminProductService);

  protected readonly revision = signal<AdminProductVariantRevisionDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly rejectForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  protected readonly validationMessages = computed(() => {
    const errors = this.revision()?.validationErrors ?? {};
    return Object.entries(errors)
      .flatMap(([field, messages]) => messages.map(message => `${field}: ${message}`));
  });

  async ngOnInit(): Promise<void> {
    await this.loadRevision();
  }

  protected variantKey(variant: AdminProductVariantRevisionFinalVariantResponse): string {
    return variant.sourceVariantId ?? `${variant.sku}-${variant.size}-${variant.colour}-${variant.changeType}`;
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (status === 'Approved') {
      return 'success';
    }

    if (status === 'Rejected' || status === 'Cancelled') {
      return 'danger';
    }

    return 'warning';
  }

  protected changeTone(changeType: string): StatusBadgeTone {
    if (changeType === 'Add') {
      return 'success';
    }

    if (changeType === 'Deactivate') {
      return 'danger';
    }

    return 'accent';
  }

  protected renderAmount(value: number): string {
    return value.toLocaleString('en-ZA', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  protected async approve(): Promise<void> {
    const revision = this.revision();
    if (!revision) {
      return;
    }

    await this.runAction(
      () => this.adminProductService.approveVariantRevision(revision.revisionId),
      'Variant revision approved and applied.');
  }

  protected async reject(): Promise<void> {
    if (this.rejectForm.invalid) {
      this.rejectForm.markAllAsTouched();
      return;
    }

    const revision = this.revision();
    if (!revision) {
      return;
    }

    const reason = this.rejectForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminProductService.rejectVariantRevision(revision.revisionId, { reason }),
      'Variant revision rejected.');
    this.rejectForm.reset();
  }

  private async loadRevision(): Promise<void> {
    const revisionId = this.route.snapshot.paramMap.get('revisionId');
    if (!revisionId) {
      this.errorMessage.set('Variant revision id is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.revision.set(await this.adminProductService.getVariantRevision(revisionId));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.revision.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(
    action: () => Promise<AdminProductVariantRevisionDetailResponse>,
    message: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.revision.set(await action());
      this.successMessage.set(message);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }
}
