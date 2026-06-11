import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminProductListingSnapshotResponse, AdminProductRevisionDetailResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { getApiErrorMessage } from '../auth/api-error';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-product-revision-detail-page',
  imports: [
    AdminWorkspaceNavComponent,
    DatePipe,
    NgTemplateOutlet,
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
        <div class="route-card">Loading listing revision...</div>
      } @else if (revision()) {
        <app-page-header
          eyebrow="Product revision"
          [heading]="revision()!.proposed.title ?? revision()!.current.title ?? 'Untitled listing'"
          description="Review staged seller changes before they replace the live published listing."
        >
          <div pageHeaderActions>
            <app-status-badge [label]="revision()!.status" tone="warning" />
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
            <div class="revision-compare-grid">
              <article class="route-card admin-detail-card">
                <h2>Current live listing</h2>
                <ng-container *ngTemplateOutlet="snapshotTemplate; context: { snapshot: revision()!.current }" />
              </article>

              <article class="route-card admin-detail-card">
                <h2>Proposed revision</h2>
                <ng-container *ngTemplateOutlet="snapshotTemplate; context: { snapshot: revision()!.proposed }" />
              </article>
            </div>

            <article class="route-card admin-detail-card">
              <h2>Revision images</h2>
              <div class="product-image-gallery">
                @for (image of revision()!.proposed.images; track image.imageId) {
                  <article class="product-image-card" [class.primary]="image.isPrimary">
                    <div class="product-image-thumb">
                      <img [src]="image.url" [alt]="image.altText ?? 'Proposed product image'" loading="lazy">
                    </div>
                    <div>
                      <app-status-badge [label]="image.isPrimary ? 'Primary' : 'Image'" tone="accent" />
                      <h3>{{ image.altText ?? 'No alt text' }}</h3>
                      <small>Sort {{ image.sortOrder }} - {{ image.createdAtUtc | date:'medium' }}</small>
                    </div>
                  </article>
                } @empty {
                  <p>No proposed images were provided.</p>
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
              <p>Approval applies the proposed listing content and images to the live product, then refreshes search and embeddings.</p>
              <button data-ui-button="primary" type="button" [disabled]="isSaving() || revision()!.status !== 'PendingReview'" (click)="approve()">Approve revision</button>

              <form [formGroup]="rejectForm" (ngSubmit)="reject()" class="admin-reason-form" novalidate>
                <label class="ui-field">
                  <span>Rejection reason</span>
                  <textarea rows="3" formControlName="reason"></textarea>
                  @if (rejectForm.controls.reason.hasError('required')) {
                    <span class="ui-field-error">Reason is required.</span>
                  }
                </label>
                <button data-ui-button="secondary" type="submit" [disabled]="isSaving() || revision()!.status !== 'PendingReview'">Reject revision</button>
              </form>
            </div>
          </aside>
        </div>
      } @else {
        <app-ui-alert tone="error">{{ errorMessage() ?? 'Revision was not found.' }}</app-ui-alert>
      }

      <ng-template #snapshotTemplate let-snapshot="snapshot">
        <dl class="admin-facts">
          <div><dt>Title</dt><dd>{{ snapshot.title ?? 'Not provided' }}</dd></div>
          <div><dt>Slug</dt><dd>{{ snapshot.slug ?? 'Not provided' }}</dd></div>
          <div><dt>Category</dt><dd>{{ snapshot.categoryPath ?? 'No category' }}</dd></div>
          <div><dt>Short description</dt><dd>{{ snapshot.shortDescription ?? 'Not provided' }}</dd></div>
          <div><dt>Full description</dt><dd>{{ snapshot.fullDescription ?? 'Not provided' }}</dd></div>
          <div><dt>Merchandising label</dt><dd>{{ snapshot.merchandisingLabel ?? 'Not provided' }}</dd></div>
          <div><dt>SEO title</dt><dd>{{ snapshot.seoTitle ?? 'Not provided' }}</dd></div>
          <div><dt>SEO description</dt><dd>{{ snapshot.seoDescription ?? 'Not provided' }}</dd></div>
          <div><dt>Product care</dt><dd>{{ snapshot.careInstructions ?? 'Not provided' }}</dd></div>
          <div><dt>Product disclaimer</dt><dd>{{ snapshot.productDisclaimer ?? 'Not provided' }}</dd></div>
          <div><dt>Tags</dt><dd>{{ snapshot.tags.length ? snapshot.tags.join(', ') : 'None' }}</dd></div>
          <div><dt>Images</dt><dd>{{ snapshot.images.length }}</dd></div>
        </dl>
        <div class="dynamic-attributes">
          @for (attribute of attributeEntries(snapshot); track attribute.key) {
            <p><strong>{{ attribute.key }}</strong>: {{ attribute.value }}</p>
          } @empty {
            <p>No attributes provided.</p>
          }
        </div>
      </ng-template>
    </section>
  `
})
export class AdminProductRevisionDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminProductService = inject(AdminProductService);

  protected readonly revision = signal<AdminProductRevisionDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly rejectForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadRevision();
  }

  protected attributeEntries(snapshot: AdminProductListingSnapshotResponse): readonly { key: string; value: string }[] {
    return Object.entries(snapshot.attributes).map(([key, value]) => ({ key, value: renderAttributeValue(value) }));
  }

  protected async approve(): Promise<void> {
    const revision = this.revision();
    if (!revision) {
      return;
    }

    await this.runAction(
      () => this.adminProductService.approveRevision(revision.revisionId),
      'Revision approved and applied.');
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
      () => this.adminProductService.rejectRevision(revision.revisionId, { reason }),
      'Revision rejected.');
    this.rejectForm.reset();
  }

  private async loadRevision(): Promise<void> {
    const revisionId = this.route.snapshot.paramMap.get('revisionId');
    if (!revisionId) {
      this.errorMessage.set('Revision id is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.revision.set(await this.adminProductService.getRevision(revisionId));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.revision.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(
    action: () => Promise<AdminProductRevisionDetailResponse>,
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

function renderAttributeValue(valueJson: string): string {
  try {
    const parsed = JSON.parse(valueJson) as unknown;
    return Array.isArray(parsed) ? parsed.join(', ') : String(parsed ?? '');
  } catch {
    return valueJson;
  }
}
