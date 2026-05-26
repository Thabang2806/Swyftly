import { CurrencyPipe, DatePipe, PercentPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import {
  SellerAdCampaignMetricsResponse,
  SellerAdCampaignResponse
} from '../seller/seller-ad-campaign.models';
import { SellerAdCampaignService } from '../seller/seller-ad-campaign.service';
import { SellerProductSummaryResponse } from '../seller/seller-product.models';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-seller-ad-campaign-detail-page',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, PercentPipe, RouterLink, SellerWorkspaceNavComponent],
  template: `
    <section class="page seller-ops-page seller-products hf-seller-ad-detail-page">
      <app-seller-workspace-nav />

      <a class="admin-back-link" routerLink="/seller/ads">Back to campaigns</a>

      @if (isLoading()) {
        <div class="route-card">Loading campaign...</div>
      } @else if (campaign()) {
        <div class="page-header seller-products-header">
          <div>
            <span class="eyebrow">Seller advertising</span>
            <h1>{{ campaign()?.name }}</h1>
            <p>{{ campaign()?.campaignType }} / {{ campaign()?.startsAtUtc | date:'mediumDate' }} to {{ campaign()?.endsAtUtc | date:'mediumDate' }}</p>
          </div>
          <span class="status-pill">{{ campaign()?.status }}</span>
        </div>

        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (successMessage()) {
          <p class="auth-alert success" role="status">{{ successMessage() }}</p>
        }

        @if (metrics(); as metric) {
          <div class="dashboard-metrics" aria-label="Campaign metrics">
            <div class="dashboard-metric-card"><span>Impressions</span><strong>{{ metric.impressions }}</strong></div>
            <div class="dashboard-metric-card"><span>Clicks</span><strong>{{ metric.clicks }}</strong></div>
            <div class="dashboard-metric-card"><span>CTR</span><strong>{{ metric.clickThroughRate | percent:'1.0-2' }}</strong></div>
            <div class="dashboard-metric-card"><span>Spend</span><strong>{{ metric.spend | currency:metric.currency:'symbol-narrow' }}</strong></div>
            <div class="dashboard-metric-card"><span>Orders</span><strong>{{ metric.ordersGenerated }}</strong></div>
            <div class="dashboard-metric-card"><span>Revenue</span><strong>{{ metric.revenueGenerated | currency:metric.currency:'symbol-narrow' }}</strong></div>
          </div>
        }

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <article class="route-card admin-detail-card">
              <h2>Budget</h2>
              @if (campaign()?.budget) {
                <dl class="admin-facts">
                  <div><dt>Daily budget</dt><dd>{{ campaign()?.budget?.dailyBudget | currency:(campaign()?.budget?.currency ?? 'ZAR'):'symbol-narrow' }}</dd></div>
                  <div><dt>Total budget</dt><dd>{{ campaign()?.budget?.totalBudget | currency:(campaign()?.budget?.currency ?? 'ZAR'):'symbol-narrow' }}</dd></div>
                  <div><dt>Max CPC</dt><dd>{{ campaign()?.budget?.maxCostPerClick | currency:(campaign()?.budget?.currency ?? 'ZAR'):'symbol-narrow' }}</dd></div>
                  <div><dt>Spent</dt><dd>{{ campaign()?.budget?.spentAmount | currency:(campaign()?.budget?.currency ?? 'ZAR'):'symbol-narrow' }}</dd></div>
                </dl>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Promoted products</h2>
              <div class="admin-product-variants">
                @for (productId of campaign()?.productIds; track productId) {
                  <div>
                    <span class="status-pill">{{ productStatus(productId) }}</span>
                    <strong>{{ productTitle(productId) }}</strong>
                    <span>{{ productId }}</span>
                  </div>
                }
              </div>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Eligibility</h2>
              <span class="status-pill">{{ campaign()?.eligibility?.isEligible ? 'Eligible' : 'Warnings' }}</span>
              @if (campaign()?.eligibility?.sellerReasons?.length) {
                <p>{{ campaign()?.eligibility?.sellerReasons?.join(', ') }}</p>
              }
              <div class="admin-product-risks">
                @for (check of campaign()?.eligibility?.products; track check.productId) {
                  <div>
                    <span class="status-pill">{{ check.isEligible ? 'Eligible' : 'Blocked' }}</span>
                    <strong>{{ productTitle(check.productId) }}</strong>
                    <span>Quality score {{ check.qualityScore }}</span>
                    @if (check.reasons.length > 0) {
                      <small>{{ check.reasons.join(', ') }}</small>
                    }
                  </div>
                }
              </div>
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card">
              <h2>Campaign actions</h2>
              @if (campaign()?.status === 'Draft' || campaign()?.status === 'Rejected') {
                <button mat-flat-button type="button" [disabled]="isSaving()" (click)="submitForReview()">Submit for review</button>
              }
              @if (campaign()?.status === 'Active') {
                <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="pause()">Pause</button>
              }
              @if (campaign()?.status === 'Paused') {
                <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="resume()">Resume</button>
              }
              @if (campaign()?.status !== 'Cancelled' && campaign()?.status !== 'Completed') {
                <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="cancel()">Cancel</button>
              }
            </div>
          </aside>
        </div>
      } @else {
        <p class="auth-alert error" role="alert">{{ errorMessage() ?? 'Campaign was not found.' }}</p>
      }
    </section>
  `
})
export class SellerAdCampaignDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly adCampaignService = inject(SellerAdCampaignService);
  private readonly productService = inject(SellerProductService);

  protected readonly campaign = signal<SellerAdCampaignResponse | null>(null);
  protected readonly metrics = signal<SellerAdCampaignMetricsResponse | null>(null);
  protected readonly products = signal<SellerProductSummaryResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const campaignId = this.route.snapshot.paramMap.get('id');
    if (!campaignId) {
      this.errorMessage.set('Campaign id is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const [campaign, products] = await Promise.all([
        this.adCampaignService.getCampaign(campaignId),
        this.productService.listProducts()
      ]);
      this.campaign.set(campaign);
      this.products.set(products);
      await this.loadMetrics(campaign.adCampaignId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.campaign.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async submitForReview(): Promise<void> {
    await this.runAction(() => this.adCampaignService.submitForReview(this.requireCampaignId()), 'Campaign submitted for review.');
  }

  protected async pause(): Promise<void> {
    await this.runAction(() => this.adCampaignService.pauseCampaign(this.requireCampaignId()), 'Campaign paused.');
  }

  protected async resume(): Promise<void> {
    await this.runAction(() => this.adCampaignService.resumeCampaign(this.requireCampaignId()), 'Campaign resumed.');
  }

  protected async cancel(): Promise<void> {
    await this.runAction(() => this.adCampaignService.cancelCampaign(this.requireCampaignId()), 'Campaign cancelled.');
  }

  protected productTitle(productId: string): string {
    return this.products().find(product => product.productId === productId)?.title ?? productId;
  }

  protected productStatus(productId: string): string {
    return this.products().find(product => product.productId === productId)?.status ?? 'Unknown';
  }

  private async runAction(
    action: () => Promise<SellerAdCampaignResponse>,
    message: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const campaign = await action();
      this.campaign.set(campaign);
      await this.loadMetrics(campaign.adCampaignId);
      this.successMessage.set(message);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private async loadMetrics(campaignId: string): Promise<void> {
    try {
      this.metrics.set(await this.adCampaignService.getMetrics(campaignId));
    } catch {
      this.metrics.set(null);
    }
  }

  private requireCampaignId(): string {
    const campaign = this.campaign();
    if (!campaign) {
      throw new Error('Campaign is not loaded.');
    }

    return campaign.adCampaignId;
  }
}
