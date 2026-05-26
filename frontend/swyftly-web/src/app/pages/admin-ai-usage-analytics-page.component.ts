import { CurrencyPipe, DatePipe, PercentPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminAiUsageAnalyticsResponse } from '../admin/admin-ai-usage-analytics.models';
import { AdminAiUsageAnalyticsService } from '../admin/admin-ai-usage-analytics.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-admin-ai-usage-analytics-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    AdminWorkspaceNavComponent,
    PercentPipe,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />

      <a class="admin-back-link" routerLink="/admin">Back to admin</a>

      <div class="page-header">
        <span class="eyebrow">Admin analytics</span>
        <h1>AI usage dashboard</h1>
        <p>Review aggregate AI usage, failures, latency, estimated cost, listing suggestions, and moderation flags.</p>
      </div>

      <form [formGroup]="filtersForm" (ngSubmit)="loadAnalytics()" class="route-card admin-audit-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>From</mat-label>
          <input matInput type="datetime-local" formControlName="from">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>To</mat-label>
          <input matInput type="datetime-local" formControlName="to">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Feature</mat-label>
          <input matInput formControlName="featureName" placeholder="ListingAssistant">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Seller ID</mat-label>
          <input matInput formControlName="sellerId" placeholder="Optional seller GUID">
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit" [disabled]="isLoading()">Apply filters</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading AI usage analytics...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (analytics() && !errorMessage()) {
          <div class="route-card compact-card">
            <span class="status-pill">Generated {{ analytics()!.generatedAtUtc | date:'medium' }}</span>
            <p>{{ analytics()!.fromUtc | date:'medium' }} to {{ analytics()!.toUtc | date:'medium' }}</p>
          </div>

          <div class="dashboard-metrics" aria-label="AI usage metrics">
            <div class="dashboard-metric-card"><span>Requests</span><strong>{{ analytics()!.totals.requests }}</strong></div>
            <div class="dashboard-metric-card"><span>Successful</span><strong>{{ analytics()!.totals.successfulRequests }}</strong></div>
            <div class="dashboard-metric-card"><span>Failed</span><strong>{{ analytics()!.totals.failedRequests }}</strong></div>
            <div class="dashboard-metric-card"><span>Failure rate</span><strong>{{ analytics()!.totals.failureRate | percent:'1.0-2' }}</strong></div>
            <div class="dashboard-metric-card"><span>Estimated cost</span><strong>{{ analytics()!.totals.estimatedCost | currency:'USD':'symbol-narrow':'1.4-4' }}</strong></div>
            <div class="dashboard-metric-card"><span>Average latency</span><strong>{{ analytics()!.totals.averageLatencyMs }} ms</strong></div>
          </div>

          <div class="dashboard-metrics" aria-label="AI suggestion metrics">
            <div class="dashboard-metric-card"><span>Suggestions generated</span><strong>{{ analytics()!.suggestions.productSuggestionsGenerated }}</strong></div>
            <div class="dashboard-metric-card"><span>Suggestions accepted</span><strong>{{ analytics()!.suggestions.productSuggestionsAccepted }}</strong></div>
            <div class="dashboard-metric-card"><span>Acceptance rate</span><strong>{{ analytics()!.suggestions.suggestionAcceptanceRate | percent:'1.0-2' }}</strong></div>
            <div class="dashboard-metric-card"><span>Products improved</span><strong>{{ analytics()!.suggestions.productsImprovedWithAi }}</strong></div>
            <div class="dashboard-metric-card"><span>Average quality score</span><strong>{{ analytics()!.suggestions.averageListingQualityScore }}</strong></div>
            <div class="dashboard-metric-card"><span>Admin review flags</span><strong>{{ analytics()!.moderation.adminReviewFlags }}</strong></div>
          </div>

          <div class="route-card compact-card">
            <span class="status-pill">Quality improvement</span>
            <p>{{ analytics()!.suggestions.qualityScoreImprovementNote }}</p>
          </div>

          <div class="admin-table audit-table" role="table" aria-label="AI usage by feature">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Feature</span>
              <span role="columnheader">Requests</span>
              <span role="columnheader">Failures</span>
              <span role="columnheader">Cost</span>
              <span role="columnheader">Latency</span>
            </div>

            @for (feature of analytics()!.featureUsage; track feature.featureName) {
              <div class="admin-table-row" role="row">
                <span role="cell"><strong>{{ feature.featureName }}</strong></span>
                <span role="cell">{{ feature.requests }}</span>
                <span role="cell">{{ feature.failedRequests }}</span>
                <span role="cell">{{ feature.estimatedCost | currency:'USD':'symbol-narrow':'1.4-4' }}</span>
                <span role="cell">{{ feature.averageLatencyMs }} ms</span>
              </div>
            } @empty {
              <div class="admin-table-row" role="row">
                <span role="cell">No AI usage for this range.</span>
              </div>
            }
          </div>

          <div class="admin-table audit-table" role="table" aria-label="AI usage by seller">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Seller</span>
              <span role="columnheader">Requests</span>
              <span role="columnheader">Failures</span>
              <span role="columnheader">Cost</span>
              <span role="columnheader">Latency</span>
            </div>

            @for (seller of analytics()!.topSellers; track seller.sellerId) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ seller.sellerDisplayName ?? 'Unnamed seller' }}</strong>
                  <small>{{ seller.sellerId }}</small>
                </span>
                <span role="cell">{{ seller.requests }}</span>
                <span role="cell">{{ seller.failedRequests }}</span>
                <span role="cell">{{ seller.estimatedCost | currency:'USD':'symbol-narrow':'1.4-4' }}</span>
                <span role="cell">{{ seller.averageLatencyMs }} ms</span>
              </div>
            } @empty {
              <div class="admin-table-row" role="row">
                <span role="cell">No seller AI usage for this range.</span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class AdminAiUsageAnalyticsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly analyticsService = inject(AdminAiUsageAnalyticsService);

  protected readonly analytics = signal<AdminAiUsageAnalyticsResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    from: [this.toDateTimeLocalInput(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000))],
    to: [this.toDateTimeLocalInput(new Date())],
    featureName: [''],
    sellerId: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadAnalytics();
  }

  protected async loadAnalytics(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const request = this.getFilterRequest();
    if (request.fromUtc && request.toUtc && request.fromUtc > request.toUtc) {
      this.errorMessage.set('From must be earlier than or equal to To.');
      this.analytics.set(null);
      this.isLoading.set(false);
      return;
    }

    try {
      this.analytics.set(await this.analyticsService.getAnalytics(request));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.analytics.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private getFilterRequest(): { fromUtc?: string; toUtc?: string; featureName?: string; sellerId?: string } {
    const filters = this.filtersForm.getRawValue();
    return {
      fromUtc: this.toIsoStringOrUndefined(filters.from),
      toUtc: this.toIsoStringOrUndefined(filters.to),
      featureName: this.trimOrUndefined(filters.featureName),
      sellerId: this.trimOrUndefined(filters.sellerId)
    };
  }

  private toIsoStringOrUndefined(value: string): string | undefined {
    return value ? new Date(value).toISOString() : undefined;
  }

  private trimOrUndefined(value: string): string | undefined {
    const trimmed = value.trim();
    return trimmed.length === 0 ? undefined : trimmed;
  }

  private toDateTimeLocalInput(value: Date): string {
    const localTime = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
    return localTime.toISOString().slice(0, 16);
  }
}
