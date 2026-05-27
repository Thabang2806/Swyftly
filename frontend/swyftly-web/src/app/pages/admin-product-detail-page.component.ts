import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminProductDetailResponse, AdminProductImageResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { getApiErrorMessage } from '../auth/api-error';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-product-detail-page',
  imports: [
    AdminWorkspaceNavComponent,
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />
      <a class="admin-back-link" routerLink="/admin/products">Back to product queue</a>

      @if (isLoading()) {
        <div class="route-card">Loading product review...</div>
      } @else if (product()) {
        <app-page-header
          eyebrow="Product review"
          [heading]="product()?.title ?? 'Untitled product'"
          [description]="product()?.categoryPath ?? 'No category'"
        >
          <div pageHeaderActions>
            <app-status-badge [label]="product()!.status" [tone]="productStatusTone(product()!.status)" />
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
              <app-status-badge [label]="product()!.status" [tone]="productStatusTone(product()!.status)" />
              <h2>Listing</h2>
              <dl class="admin-facts">
                <div><dt>Seller</dt><dd>{{ product()?.seller?.displayName ?? 'Unnamed seller' }}</dd></div>
                <div><dt>Seller contact</dt><dd>{{ product()?.seller?.contactEmail ?? 'No contact email' }}</dd></div>
                <div><dt>Seller status</dt><dd>{{ product()?.seller?.verificationStatus ?? 'Unknown' }}</dd></div>
                <div><dt>Slug</dt><dd>{{ product()?.slug ?? 'Not provided' }}</dd></div>
                <div><dt>Created</dt><dd>{{ product()?.createdAtUtc | date:'medium' }}</dd></div>
                <div><dt>Updated</dt><dd>{{ product()?.updatedAtUtc | date:'medium' }}</dd></div>
                <div><dt>Short description</dt><dd>{{ product()?.shortDescription ?? 'Not provided' }}</dd></div>
                <div><dt>Full description</dt><dd>{{ product()?.fullDescription ?? 'Not provided' }}</dd></div>
                <div><dt>Merchandising label</dt><dd>{{ product()?.merchandisingLabel ?? 'Not provided' }}</dd></div>
                <div><dt>SEO title</dt><dd>{{ product()?.seoTitle ?? 'Not provided' }}</dd></div>
                <div><dt>SEO description</dt><dd>{{ product()?.seoDescription ?? 'Not provided' }}</dd></div>
                <div><dt>Product care</dt><dd>{{ product()?.careInstructions ?? 'Not provided' }}</dd></div>
                <div><dt>Product disclaimer</dt><dd>{{ product()?.productDisclaimer ?? 'Not provided' }}</dd></div>
                <div><dt>Tags</dt><dd>{{ product()?.tags?.length ? product()?.tags?.join(', ') : 'None' }}</dd></div>
                @if (product()?.rejectionReason) {
                  <div><dt>Latest reason</dt><dd>{{ product()?.rejectionReason }}</dd></div>
                }
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Images</h2>
              @if ((product()?.images?.length ?? 0) === 0) {
                <div class="admin-image-fallback">
                  <strong>No images attached</strong>
                  <span>Use listing text, attributes, and seller context for this review.</span>
                </div>
              } @else {
                <div class="admin-image-review">
                  <figure class="admin-primary-image">
                    <img [src]="selectedImage()?.url" [alt]="selectedImage()?.altText ?? 'Product image'" loading="lazy">
                    <figcaption>
                      <app-status-badge [label]="selectedImage()?.isPrimary ? 'Primary' : 'Image'" tone="accent" />
                      <span>{{ selectedImage()?.altText ?? 'No alt text' }}</span>
                    </figcaption>
                  </figure>

                  <div class="admin-image-thumbnails" aria-label="Product image thumbnails">
                    @for (image of product()?.images; track image.imageId) {
                      <button
                        type="button"
                        [class.active]="selectedImage()?.imageId === image.imageId"
                        (click)="selectImage(image)"
                      >
                        <img [src]="image.url" [alt]="image.altText ?? 'Product thumbnail'" loading="lazy">
                      </button>
                    }
                  </div>
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Attributes</h2>
              @if (attributeEntries().length === 0) {
                <p>No attributes were provided.</p>
              } @else {
                <dl class="admin-facts">
                  @for (attribute of attributeEntries(); track attribute.key) {
                    <div>
                      <dt>{{ attribute.key }}</dt>
                      <dd>{{ attribute.value }}</dd>
                    </div>
                  }
                </dl>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Variants</h2>
              <div class="admin-review-summary">
                <div><span>Total stock</span><strong>{{ totalStockQuantity() }}</strong></div>
                <div><span>Reserved</span><strong>{{ totalReservedQuantity() }}</strong></div>
                <div><span>Available</span><strong>{{ totalAvailableQuantity() }}</strong></div>
              </div>
              @if ((product()?.variants?.length ?? 0) === 0) {
                <p>No variants were provided.</p>
              } @else {
                <div class="admin-product-variants">
                  @for (variant of product()?.variants; track variant.variantId) {
                    <div>
                      <app-status-badge [label]="variant.status" [tone]="variant.status === 'Active' ? 'success' : 'neutral'" />
                      <strong>{{ variant.sku }}</strong>
                      <span>{{ variant.size }} / {{ variant.colour }}</span>
                      <span>{{ variant.price | currency:'ZAR':'symbol-narrow' }} - {{ variant.availableQuantity }} available</span>
                      <small>{{ variant.reservedQuantity }} reserved of {{ variant.stockQuantity }} stock</small>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>AI risk flags</h2>
              @if ((product()?.moderationResults?.length ?? 0) === 0) {
                <p>No moderation flags were recorded for this product.</p>
              } @else {
                <div class="admin-product-risks">
                  @for (result of product()?.moderationResults; track result.moderationResultId) {
                    <div>
                      <app-status-badge [label]="result.riskLevel" [tone]="riskTone(result.riskLevel)" />
                      <strong>{{ result.reason }}</strong>
                      <span>{{ result.provider }} - {{ result.createdAtUtc | date:'medium' }}</span>
                      @if (result.needsAdminReview) {
                        <small>Needs admin review</small>
                      }
                      @if (result.flags.length > 0) {
                        <small>Flags: {{ result.flags.join(', ') }}</small>
                      }
                      @if (result.detectedTerms.length > 0) {
                        <small>Terms: {{ result.detectedTerms.join(', ') }}</small>
                      }
                      @if (result.missingFields.length > 0) {
                        <small>Missing: {{ result.missingFields.join(', ') }}</small>
                      }
                    </div>
                  }
                </div>
              }
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card">
              <h2>Review actions</h2>

              <form [formGroup]="approveForm" (ngSubmit)="approve()" class="admin-reason-form" novalidate>
                @if (hasHighRiskModeration()) {
                  <mat-form-field appearance="outline">
                    <mat-label>Override reason</mat-label>
                    <textarea matInput rows="3" formControlName="overrideReason"></textarea>
                    <mat-hint>Required for unresolved high-risk AI flags.</mat-hint>
                  </mat-form-field>
                }
                <button mat-flat-button type="submit" [disabled]="isSaving()">Approve product</button>
              </form>

              <form [formGroup]="changesForm" (ngSubmit)="requestChanges()" class="admin-reason-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Change request reason</mat-label>
                  <textarea matInput rows="3" formControlName="reason"></textarea>
                  @if (changesForm.controls.reason.hasError('required')) {
                    <mat-error>Reason is required.</mat-error>
                  }
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Request changes</button>
              </form>

              <form [formGroup]="rejectForm" (ngSubmit)="reject()" class="admin-reason-form" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Rejection reason</mat-label>
                  <textarea matInput rows="3" formControlName="reason"></textarea>
                  @if (rejectForm.controls.reason.hasError('required')) {
                    <mat-error>Reason is required.</mat-error>
                  }
                </mat-form-field>
                <button mat-stroked-button type="submit" [disabled]="isSaving()">Reject product</button>
              </form>
            </div>

            <div class="route-card admin-action-card">
              <h2>Audit trail</h2>
              @if ((product()?.auditTrail?.length ?? 0) === 0) {
                <p>No admin actions have been recorded for this product.</p>
              } @else {
                <ol class="audit-list">
                  @for (entry of product()?.auditTrail; track entry.id) {
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
        <app-ui-alert tone="error">{{ errorMessage() ?? 'Product was not found.' }}</app-ui-alert>
      }
    </section>
  `
})
export class AdminProductDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminProductService = inject(AdminProductService);

  protected readonly product = signal<AdminProductDetailResponse | null>(null);
  protected readonly selectedImageId = signal<string | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly attributeEntries = computed(() => {
    const attributes = this.product()?.attributes ?? {};
    return Object.entries(attributes).map(([key, value]) => ({
      key,
      value: this.formatAttributeValue(value)
    }));
  });
  protected readonly selectedImage = computed(() => {
    const product = this.product();
    if (!product || product.images.length === 0) {
      return null;
    }

    const selectedImageId = this.selectedImageId();
    return product.images.find(image => image.imageId === selectedImageId)
      ?? product.images.find(image => image.isPrimary)
      ?? product.images[0];
  });

  protected readonly approveForm = this.formBuilder.group({
    overrideReason: ['']
  });

  protected readonly changesForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  protected readonly rejectForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadProduct();
  }

  protected hasHighRiskModeration(): boolean {
    return this.product()?.moderationResults.some(result => result.needsAdminReview && result.riskLevel === 'High') ?? false;
  }

  protected selectImage(image: AdminProductImageResponse): void {
    this.selectedImageId.set(image.imageId);
  }

  protected totalStockQuantity(): number {
    return this.product()?.variants.reduce((total, variant) => total + variant.stockQuantity, 0) ?? 0;
  }

  protected totalReservedQuantity(): number {
    return this.product()?.variants.reduce((total, variant) => total + variant.reservedQuantity, 0) ?? 0;
  }

  protected totalAvailableQuantity(): number {
    return this.product()?.variants.reduce((total, variant) => total + variant.availableQuantity, 0) ?? 0;
  }

  protected productStatusTone(status: string): StatusBadgeTone {
    if (['Published', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'NeedsAdminReview'].includes(status)) {
      return 'danger';
    }

    return 'warning';
  }

  protected riskTone(riskLevel: string): StatusBadgeTone {
    if (riskLevel === 'High') {
      return 'danger';
    }

    if (riskLevel === 'Medium') {
      return 'warning';
    }

    return 'neutral';
  }

  protected async approve(): Promise<void> {
    const product = this.product();
    if (!product) {
      return;
    }

    const overrideReason = this.approveForm.getRawValue().overrideReason.trim();
    if (this.hasHighRiskModeration() && !overrideReason) {
      this.errorMessage.set('Override reason is required for high-risk products.');
      return;
    }

    await this.runAction(
      () => this.adminProductService.approveProduct(product.productId, { overrideReason: overrideReason || null }),
      'Product approved.');
    this.approveForm.reset();
  }

  protected async requestChanges(): Promise<void> {
    if (this.changesForm.invalid) {
      this.changesForm.markAllAsTouched();
      return;
    }

    const product = this.product();
    if (!product) {
      return;
    }

    const reason = this.changesForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminProductService.requestChanges(product.productId, { reason }),
      'Changes requested.');
    this.changesForm.reset();
  }

  protected async reject(): Promise<void> {
    if (this.rejectForm.invalid) {
      this.rejectForm.markAllAsTouched();
      return;
    }

    const product = this.product();
    if (!product) {
      return;
    }

    const reason = this.rejectForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminProductService.rejectProduct(product.productId, { reason }),
      'Product rejected.');
    this.rejectForm.reset();
  }

  private async loadProduct(): Promise<void> {
    const productId = this.route.snapshot.paramMap.get('productId');
    if (!productId) {
      this.errorMessage.set('Product id is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const product = await this.adminProductService.getProduct(productId);
      this.product.set(product);
      this.selectedImageId.set(product.images.find(image => image.isPrimary)?.imageId ?? product.images[0]?.imageId ?? null);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.product.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(
    action: () => Promise<AdminProductDetailResponse>,
    message: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.product.set(await action());
      this.successMessage.set(message);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private formatAttributeValue(valueJson: string): string {
    try {
      const parsed = JSON.parse(valueJson) as unknown;
      if (Array.isArray(parsed)) {
        return parsed.join(', ');
      }

      return String(parsed ?? '');
    } catch {
      return valueJson;
    }
  }
}
