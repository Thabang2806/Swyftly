import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminReviewService } from '../admin/admin-review.service';
import { AdminProductReviewDetailResponse } from '../admin/admin-review.models';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { MetricTileComponent } from '../shared/ui/metric-tile.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { ProductVisualFallbackComponent } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';
import { WorkspaceShellComponent } from '../shared/ui/workspace-shell.component';

@Component({
  selector: 'app-admin-reviews-page',
  imports: [
    AdminWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MetricTileComponent,
    PageHeaderComponent,
    ProductVisualFallbackComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent,
    WorkspaceShellComponent
  ],
  template: `
    <section class="page admin-review hf-admin-console">
      <app-workspace-shell>
        <app-admin-workspace-nav workspaceNav />

        <app-page-header
          eyebrow="Admin console"
          heading="Buyer review moderation"
          description="Moderate verified-purchase reviews before they become visible on product pages."
        >
          <div pageHeaderActions>
            <a mat-stroked-button routerLink="/admin/products">Product queue</a>
            <a mat-stroked-button routerLink="/admin/audit-logs">Audit logs</a>
          </div>
        </app-page-header>

        <div class="hf-metric-grid">
          @for (metric of reviewMetrics(); track metric.label) {
            <app-metric-tile
              [label]="metric.label"
              [value]="metric.value"
              [badge]="metric.badge"
              [badgeTone]="metric.tone"
            />
          }
        </div>

      <form [formGroup]="filtersForm" (ngSubmit)="applyFilters()" class="route-card admin-moderation-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Search reviews</mat-label>
          <input matInput formControlName="search" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <input matInput formControlName="status" placeholder="PendingReview" />
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit">Apply filters</button>
          <button mat-stroked-button type="button" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading buyer reviews...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (filteredReviews().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Clear"
            heading="No reviews pending moderation"
            message="New and edited buyer reviews will appear here before publication."
          />
        } @else {
          <div class="admin-review-layout hf-admin-review-layout">
            <div class="hf-admin-queue-card">
            <div class="hf-admin-card-heading">
              <div>
                <span>Review list</span>
                <h2>Pending buyer reviews</h2>
              </div>
              <app-status-badge [label]="filteredReviews().length + ' visible'" tone="accent" />
            </div>

            <div class="admin-table admin-moderation-table" role="table" aria-label="Pending buyer reviews">
              <div class="admin-table-row heading admin-moderation-table-row" role="row">
                <span role="columnheader">Review</span>
                <span role="columnheader">Product</span>
                <span role="columnheader">Seller</span>
                <span role="columnheader">Submitted</span>
                <span role="columnheader">Action</span>
              </div>

              @for (review of filteredReviews(); track review.reviewId) {
                <div
                  class="admin-table-row admin-moderation-table-row hf-admin-select-row"
                  role="row"
                  [class.active]="selectedReview()?.reviewId === review.reviewId"
                  (click)="selectReview(review)"
                >
                  <span role="cell">
                    <strong>{{ review.rating }}/5</strong>
                    <small>{{ review.title ?? 'Untitled review' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ review.product.title ?? review.order.productTitle ?? 'Reviewed product' }}</strong>
                    <small>{{ review.order.size ?? 'No size' }} / {{ review.order.colour ?? 'No colour' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ review.seller.displayName ?? 'Unnamed seller' }}</strong>
                    <small>{{ review.seller.verificationStatus ?? 'Unknown status' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ review.createdAtUtc | date:'mediumDate' }}</strong>
                    <app-status-badge [label]="review.status" [tone]="reviewTone(review.status)" />
                  </span>
                  <span role="cell">
                    <button mat-stroked-button type="button" (click)="selectReview(review); $event.stopPropagation()">Review</button>
                  </span>
                </div>
              }
            </div>

            <p class="audit-count">{{ filteredReviews().length }} of {{ pendingReviews().length }} review{{ pendingReviews().length === 1 ? '' : 's' }}</p>
            </div>

            @if (selectedReview()) {
              <article class="hf-admin-evidence-panel moderation-detail-panel">
                <div class="hf-admin-card-heading">
                  <div>
                    <span>Selected review</span>
                    <h2>{{ selectedReview()!.title ?? 'Untitled review' }}</h2>
                  </div>
                  <app-status-badge [label]="selectedReview()!.status" [tone]="reviewTone(selectedReview()!.status)" />
                </div>

                @if (selectedReview()!.product.primaryImageUrl) {
                  <img
                    class="hf-admin-review-image"
                    [src]="selectedReview()!.product.primaryImageUrl!"
                    [alt]="selectedReview()!.product.primaryImageAltText ?? selectedReview()!.product.title ?? 'Reviewed product'"
                    loading="lazy"
                  >
                } @else {
                  <app-product-visual-fallback
                    [title]="selectedReview()!.product.title ?? selectedReview()!.order.productTitle ?? 'Reviewed product'"
                    label="Reviewed item"
                    tone="dress"
                  />
                }

                <div class="hf-admin-summary-panel">
                  <strong>{{ selectedReview()!.rating }}/5 buyer rating</strong>
                  <span>{{ selectedReview()!.body ?? 'No written review body.' }}</span>
                </div>

                <div class="detail-grid">
                  <span>
                    <small>Product</small>
                    <strong>{{ selectedReview()!.product.title ?? selectedReview()!.order.productTitle ?? 'Reviewed product' }}</strong>
                    @if (selectedReview()!.product.slug) {
                      <a [routerLink]="['/product', selectedReview()!.product.slug]">View product</a>
                    }
                  </span>
                  <span>
                    <small>Seller</small>
                    <strong>{{ selectedReview()!.seller.displayName ?? 'Unnamed seller' }}</strong>
                    <small>{{ selectedReview()!.seller.contactEmail ?? 'No seller email' }}</small>
                  </span>
                  <span>
                    <small>Buyer</small>
                    <strong>{{ selectedReview()!.buyerId }}</strong>
                    <small>{{ selectedReview()!.buyer.userId ?? 'No user id' }}</small>
                  </span>
                  <span>
                    <small>Order item</small>
                    <strong>{{ selectedReview()!.order.status ?? 'Unknown order status' }}</strong>
                    <small>{{ selectedReview()!.order.sku ?? 'No SKU' }} / Qty {{ selectedReview()!.order.quantity ?? 0 }}</small>
                  </span>
                </div>

                <form [formGroup]="reasonForm" class="buyer-form-grid" novalidate>
                  <mat-form-field appearance="outline">
                    <mat-label>Moderation reason</mat-label>
                    <textarea matInput rows="3" formControlName="reason"></textarea>
                  </mat-form-field>

                  <div class="buyer-action-row">
                    <button mat-flat-button type="button" [disabled]="isSaving()" (click)="approveSelected()">Approve</button>
                    <button mat-stroked-button type="button" [disabled]="isSaving() || reasonForm.invalid" (click)="rejectSelected()">Reject</button>
                    <button mat-stroked-button type="button" [disabled]="isSaving() || reasonForm.invalid" (click)="removeSelected()">Remove</button>
                  </div>
                </form>
              </article>
            }
          </div>

        }
      }
      </app-workspace-shell>
    </section>
  `
})
export class AdminReviewsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly reviewService = inject(AdminReviewService);

  protected readonly pendingReviews = signal<AdminProductReviewDetailResponse[]>([]);
  protected readonly selectedReview = signal<AdminProductReviewDetailResponse | null>(null);
  protected readonly filters = signal({ search: '', status: '' });
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    search: [''],
    status: ['']
  });

  protected readonly reasonForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  protected readonly filteredReviews = computed(() => {
    const search = this.filters().search.trim().toLowerCase();
    const status = this.filters().status.trim().toLowerCase();

    return this.pendingReviews().filter(review => {
      const haystack = [
        review.title,
        review.body,
        review.status,
        review.product.title,
        review.order.productTitle,
        review.order.sku,
        review.seller.displayName,
        review.seller.verificationStatus,
        review.buyerId
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();

      return (search.length === 0 || haystack.includes(search)) &&
        (status.length === 0 || review.status.toLowerCase().includes(status));
    });
  });

  protected readonly reviewMetrics = computed(() => {
    const reviews = this.pendingReviews();
    const lowRatings = reviews.filter(review => review.rating <= 2).length;
    const sellerCount = new Set(reviews.map(review => review.sellerId)).size;

    return [
      {
        label: 'Pending reviews',
        value: reviews.length.toString(),
        badge: 'Moderate',
        tone: 'warning' as StatusBadgeTone
      },
      {
        label: 'Low ratings',
        value: lowRatings.toString(),
        badge: lowRatings > 0 ? 'Check' : 'Clear',
        tone: lowRatings > 0 ? 'warning' as StatusBadgeTone : 'success' as StatusBadgeTone
      },
      {
        label: 'Seller count',
        value: sellerCount.toString(),
        badge: 'Context',
        tone: 'accent' as StatusBadgeTone
      },
      {
        label: 'Filtered view',
        value: this.filteredReviews().length.toString(),
        badge: 'Visible',
        tone: 'neutral' as StatusBadgeTone
      }
    ];
  });

  async ngOnInit(): Promise<void> {
    await this.loadReviews();
  }

  protected applyFilters(): void {
    this.filters.set(this.filtersForm.getRawValue());
    const filteredReviews = this.filteredReviews();
    if (!filteredReviews.some(review => review.reviewId === this.selectedReview()?.reviewId)) {
      this.selectedReview.set(filteredReviews[0] ?? null);
    }
  }

  protected clearFilters(): void {
    this.filtersForm.reset({ search: '', status: '' });
    this.applyFilters();
  }

  protected selectReview(review: AdminProductReviewDetailResponse): void {
    this.selectedReview.set(review);
    this.reasonForm.reset({ reason: review.moderationReason ?? '' });
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  protected async approveSelected(): Promise<void> {
    const review = this.selectedReview();
    if (!review || this.isSaving()) {
      return;
    }

    await this.runModerationAction(
      () => this.reviewService.approveReview(review.reviewId),
      'Review approved and published.');
  }

  protected async rejectSelected(): Promise<void> {
    const review = this.selectedReview();
    if (!review || this.reasonForm.invalid || this.isSaving()) {
      this.reasonForm.markAllAsTouched();
      return;
    }

    await this.runModerationAction(
      () => this.reviewService.rejectReview(review.reviewId, this.reasonForm.getRawValue()),
      'Review rejected.');
  }

  protected async removeSelected(): Promise<void> {
    const review = this.selectedReview();
    if (!review || this.reasonForm.invalid || this.isSaving()) {
      this.reasonForm.markAllAsTouched();
      return;
    }

    await this.runModerationAction(
      () => this.reviewService.removeReview(review.reviewId, this.reasonForm.getRawValue()),
      'Review removed.');
  }

  protected reviewTone(status: string): StatusBadgeTone {
    if (status === 'Published') {
      return 'success';
    }

    if (status === 'Rejected' || status === 'Removed') {
      return 'danger';
    }

    return 'warning';
  }

  private async runModerationAction(
    action: () => Promise<AdminProductReviewDetailResponse>,
    successMessage: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await action();
      this.pendingReviews.set(this.pendingReviews().filter(review => review.reviewId !== updated.reviewId));
      this.selectedReview.set(updated);
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private async loadReviews(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const reviews = await this.reviewService.getPendingReviews();
      this.pendingReviews.set(reviews);
      this.selectedReview.set(reviews[0] ?? null);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
