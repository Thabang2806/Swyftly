import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerProductReviewResponse } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-reviews-page',
  imports: [
    BuyerWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Product reviews"
        description="Manage verified-purchase reviews you have left for delivered order items."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/account/orders">Review delivered orders</a>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading reviews...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (reviews().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Reviews"
            heading="No reviews yet"
            message="Open a delivered order to leave a verified-purchase review for an item."
          >
            <a data-ui-button="primary" routerLink="/account/orders">View orders</a>
          </app-empty-state>
        } @else {
          <div class="buyer-review-list">
            @for (review of reviews(); track review.reviewId) {
              <article class="buyer-review-card">
                <div class="buyer-review-product">
                  @if (review.product?.primaryImageUrl) {
                    <img [src]="review.product!.primaryImageUrl" [alt]="review.product!.primaryImageAltText ?? review.product!.title ?? 'Reviewed product'">
                  } @else {
                    <div class="buyer-review-fallback">{{ review.product?.title ?? 'Reviewed product' }}</div>
                  }

                  <span>
                    <strong>{{ review.product?.title ?? 'Reviewed product' }}</strong>
                    <small>{{ review.updatedAtUtc | date:'mediumDate' }}</small>
                    @if (review.product?.slug) {
                      <a [routerLink]="['/product', review.product!.slug]">View product</a>
                    }
                  </span>
                </div>

                <div class="buyer-review-content">
                  <div class="buyer-review-heading">
                    <strong>{{ stars(review.rating) }}</strong>
                    <app-status-badge [label]="review.status" [tone]="reviewTone(review.status)" />
                  </div>
                  @if (review.title) {
                    <h2>{{ review.title }}</h2>
                  }
                  @if (review.body) {
                    <p>{{ review.body }}</p>
                  }
                  @if (review.status === 'PendingReview') {
                    <app-ui-alert tone="info">This review is waiting for moderation before it appears publicly.</app-ui-alert>
                  }
                  @if (review.status === 'Rejected' && review.moderationReason) {
                    <app-ui-alert tone="warning">Not published: {{ review.moderationReason }}</app-ui-alert>
                  }
                </div>

                @if (editingReviewId() === review.reviewId) {
                  <form [formGroup]="reviewForm" (ngSubmit)="saveReview(review)" class="buyer-form-grid" novalidate>
                    <label class="ui-field">
                      <span>Rating</span>
                      <select formControlName="rating">
                        @for (rating of ratings; track rating) {
                          <option [ngValue]="rating">{{ rating }} star{{ rating === 1 ? '' : 's' }}</option>
                        }
                      </select>
                    </label>

                    <label class="ui-field">
                      <span>Title</span>
                      <input formControlName="title" />
                    </label>

                    <label class="ui-field">
                      <span>Review</span>
                      <textarea rows="4" formControlName="body"></textarea>
                    </label>

                    <div class="buyer-action-row">
                      <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Save review</button>
                      <button data-ui-button="secondary" type="button" (click)="cancelEdit()">Cancel</button>
                    </div>
                  </form>
                } @else {
                  <div class="buyer-action-row">
                    <button data-ui-button="secondary" type="button" (click)="startEdit(review)">Edit</button>
                    <button data-ui-button="secondary" type="button" [disabled]="isSaving()" (click)="deleteReview(review)">Delete</button>
                  </div>
                }
              </article>
            }
          </div>
        }
      }
    </section>
  `
})
export class BuyerReviewsPageComponent implements OnInit {
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly ratings = [5, 4, 3, 2, 1] as const;
  protected readonly reviews = signal<BuyerProductReviewResponse[]>([]);
  protected readonly editingReviewId = signal<string | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly reviewForm = this.formBuilder.group({
    rating: [5, [Validators.required, Validators.min(1), Validators.max(5)]],
    title: [''],
    body: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadReviews();
  }

  protected startEdit(review: BuyerProductReviewResponse): void {
    this.editingReviewId.set(review.reviewId);
    this.reviewForm.setValue({
      rating: review.rating,
      title: review.title ?? '',
      body: review.body ?? ''
    });
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  protected cancelEdit(): void {
    this.editingReviewId.set(null);
  }

  protected async saveReview(review: BuyerProductReviewResponse): Promise<void> {
    if (this.reviewForm.invalid || this.isSaving()) {
      this.reviewForm.markAllAsTouched();
      return;
    }

    const value = this.reviewForm.getRawValue();
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.engagementService.updateReview(review.reviewId, {
        rating: value.rating,
        title: emptyToNull(value.title),
        body: emptyToNull(value.body)
      });
      this.reviews.set(this.reviews().map(existing => existing.reviewId === updated.reviewId ? updated : existing));
      this.editingReviewId.set(null);
      this.successMessage.set('Review updated and sent for moderation.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected async deleteReview(review: BuyerProductReviewResponse): Promise<void> {
    if (this.isSaving()) {
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await this.engagementService.deleteReview(review.reviewId);
      this.reviews.set(this.reviews().filter(existing => existing.reviewId !== review.reviewId));
      this.successMessage.set('Review deleted.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected stars(rating: number): string {
    return `${rating}/5`;
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

  private async loadReviews(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.reviews.set(await this.engagementService.listBuyerReviews());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
