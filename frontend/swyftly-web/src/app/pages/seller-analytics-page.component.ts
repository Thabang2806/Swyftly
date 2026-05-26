import { CurrencyPipe, PercentPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { SellerAnalyticsSummaryResponse } from '../seller/seller-analytics.models';
import { SellerAnalyticsService } from '../seller/seller-analytics.service';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';

@Component({
  selector: 'app-seller-analytics-page',
  imports: [CurrencyPipe, MatButtonModule, PercentPipe, RouterLink, SellerWorkspaceNavComponent],
  template: `
    <section class="page seller-ops-page seller-products seller-analytics-page">
      <app-seller-workspace-nav />

      <div class="page-header seller-products-header">
        <div>
          <span class="eyebrow">Seller analytics</span>
          <h1>Dashboard</h1>
          <p>Review aggregate sales, fulfilment, product, ad, and AI activity for your store.</p>
        </div>
        <a mat-stroked-button routerLink="/seller">Seller workspace</a>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading analytics...</div>
      } @else if (summary()) {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        <div class="dashboard-metrics" aria-label="Seller analytics metrics">
          <div class="dashboard-metric-card"><span>Total sales</span><strong>{{ summary()!.totalSales | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Orders</span><strong>{{ summary()!.orderCount }}</strong></div>
          <div class="dashboard-metric-card"><span>Average order value</span><strong>{{ summary()!.averageOrderValue | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Products sold</span><strong>{{ summary()!.productsSold }}</strong></div>
          <div class="dashboard-metric-card"><span>Refund rate</span><strong>{{ summary()!.refundRate | percent:'1.0-2' }}</strong></div>
          <div class="dashboard-metric-card"><span>Return rate</span><strong>{{ summary()!.returnRate | percent:'1.0-2' }}</strong></div>
        </div>

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <article class="route-card admin-detail-card">
              <h2>Top products</h2>
              @if (summary()!.topProducts.length === 0) {
                <p>No paid sales have been recorded yet.</p>
              } @else {
                <div class="admin-table" role="table" aria-label="Top products">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Product</span>
                    <span role="columnheader">Quantity</span>
                    <span role="columnheader">Revenue</span>
                    <span role="columnheader">Product ID</span>
                    <span role="columnheader">Action</span>
                  </div>
                  @for (product of summary()!.topProducts; track product.productId) {
                    <div class="admin-table-row" role="row">
                      <span role="cell"><strong>{{ product.productTitle ?? 'Untitled product' }}</strong></span>
                      <span role="cell">{{ product.quantitySold }}</span>
                      <span role="cell">{{ product.revenue | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell"><small>{{ product.productId }}</small></span>
                      <span role="cell"><a mat-stroked-button [routerLink]="['/seller/products', product.productId, 'edit']">Open</a></span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Low stock</h2>
              @if (summary()!.lowStockProducts.length === 0) {
                <p>No low-stock products need attention.</p>
              } @else {
                <div class="admin-product-variants">
                  @for (product of summary()!.lowStockProducts; track product.productId) {
                    <div>
                      <span class="status-pill">{{ product.status }}</span>
                      <strong>{{ product.title ?? 'Untitled product' }}</strong>
                      <span>{{ product.availableQuantity }} available</span>
                      <small>{{ product.lowStockVariantCount }} low-stock variant{{ product.lowStockVariantCount === 1 ? '' : 's' }}</small>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Ad campaign performance</h2>
              <dl class="admin-facts">
                <div><dt>Campaigns</dt><dd>{{ summary()!.adPerformance.campaignCount }}</dd></div>
                <div><dt>Impressions</dt><dd>{{ summary()!.adPerformance.impressions }}</dd></div>
                <div><dt>Clicks</dt><dd>{{ summary()!.adPerformance.clicks }}</dd></div>
                <div><dt>CTR</dt><dd>{{ summary()!.adPerformance.clickThroughRate | percent:'1.0-2' }}</dd></div>
                <div><dt>Spend</dt><dd>{{ summary()!.adPerformance.spend | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Revenue</dt><dd>{{ summary()!.adPerformance.revenueGenerated | currency:'ZAR':'symbol-narrow' }}</dd></div>
              </dl>
              @if (summary()!.adPerformance.topCampaigns.length > 0) {
                <div class="admin-product-risks">
                  @for (campaign of summary()!.adPerformance.topCampaigns; track campaign.adCampaignId) {
                    <div>
                      <span class="status-pill">{{ campaign.status }}</span>
                      <strong>{{ campaign.name }}</strong>
                      <span>{{ campaign.clicks }} clicks / {{ campaign.revenueGenerated | currency:'ZAR':'symbol-narrow' }} revenue</span>
                      <small>ROAS {{ campaign.returnOnAdSpend }}</small>
                    </div>
                  }
                </div>
              }
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card">
              <h2>AI usage</h2>
              <dl class="admin-facts">
                <div><dt>Requests</dt><dd>{{ summary()!.aiUsage.requests }}</dd></div>
                <div><dt>Successful</dt><dd>{{ summary()!.aiUsage.successfulRequests }}</dd></div>
                <div><dt>Failed</dt><dd>{{ summary()!.aiUsage.failedRequests }}</dd></div>
                <div><dt>Estimated cost</dt><dd>{{ summary()!.aiUsage.estimatedCost | currency:'USD':'symbol-narrow' }}</dd></div>
                <div><dt>Average latency</dt><dd>{{ summary()!.aiUsage.averageLatencyMs }} ms</dd></div>
                <div><dt>Suggestions generated</dt><dd>{{ summary()!.aiUsage.suggestionsGenerated }}</dd></div>
                <div><dt>Suggestions accepted</dt><dd>{{ summary()!.aiUsage.suggestionsAccepted }}</dd></div>
                <div><dt>Acceptance rate</dt><dd>{{ summary()!.aiUsage.suggestionAcceptanceRate | percent:'1.0-2' }}</dd></div>
                <div><dt>Products improved</dt><dd>{{ summary()!.aiUsage.productsImprovedWithAi }}</dd></div>
                <div><dt>Average quality score</dt><dd>{{ summary()!.aiUsage.averageListingQualityScore }}</dd></div>
              </dl>
              <p>{{ summary()!.aiUsage.qualityScoreImprovementNote }}</p>
            </div>

            <div class="route-card admin-action-card">
              <h2>Conversion signals</h2>
              <dl class="admin-facts">
                <div><dt>Conversion rate</dt><dd>{{ summary()!.conversionRatePlaceholder | percent:'1.0-2' }}</dd></div>
                <div><dt>Total refunded</dt><dd>{{ summary()!.totalRefunded | currency:'ZAR':'symbol-narrow' }}</dd></div>
              </dl>
            </div>
          </aside>
        </div>
      } @else {
        <p class="auth-alert error" role="alert">{{ errorMessage() ?? 'Analytics could not be loaded.' }}</p>
      }
    </section>
  `
})
export class SellerAnalyticsPageComponent implements OnInit {
  private readonly analyticsService = inject(SellerAnalyticsService);

  protected readonly summary = signal<SellerAnalyticsSummaryResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.summary.set(await this.analyticsService.getSummary());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
