import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { SellerAdCampaignResponse } from '../seller/seller-ad-campaign.models';
import { SellerAdCampaignService } from '../seller/seller-ad-campaign.service';
import { getApiErrorMessage } from '../auth/api-error';
import { MetricTileComponent } from '../shared/ui/metric-tile.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';

@Component({
  selector: 'app-seller-ad-campaigns-page',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, MetricTileComponent, RouterLink, StatusBadgeComponent],
  template: `
    <section class="page seller-products hf-seller-ads-page">
      <div class="page-header seller-products-header hf-ads-header">
        <div>
          <span class="eyebrow">Seller advertising</span>
          <h1>Campaigns and promoted listings</h1>
          <p>Create promoted listing campaigns, monitor budget posture, and keep campaign review state visible.</p>
        </div>
        <a mat-flat-button routerLink="/seller/ads/new">New campaign</a>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading campaigns...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (campaigns().length === 0 && !errorMessage()) {
          <div class="route-card hf-empty-ad-card">
            <span class="status-pill">Ads</span>
            <h2>No campaigns yet</h2>
            <p>Create a draft campaign once you have published products ready to promote.</p>
            <a mat-flat-button routerLink="/seller/ads/new">Create campaign</a>
          </div>
        } @else {
          <section class="hf-metric-grid" aria-label="Seller advertising summary">
            @for (metric of adMetrics(); track metric.label) {
              <app-metric-tile
                [label]="metric.label"
                [value]="metric.value"
                [badge]="metric.badge"
                [badgeTone]="metric.tone"
              />
            }
          </section>

          <section class="hf-ads-layout">
            <div class="hf-ad-campaign-list">
              <div class="seller-products-header">
                <div>
                  <h2>Campaign queue</h2>
                  <p>{{ campaigns().length }} campaign{{ campaigns().length === 1 ? '' : 's' }} loaded from the seller ads API.</p>
                </div>
                <app-status-badge [label]="activeCount() + ' active/live'" tone="success" />
              </div>

              @for (campaign of campaigns(); track campaign.adCampaignId) {
                <a class="hf-ad-campaign-row" [routerLink]="['/seller/ads', campaign.adCampaignId]">
                  <div class="hf-ad-thumb" aria-hidden="true">{{ campaign.name.charAt(0) }}</div>
                  <div class="hf-ad-row-main">
                    <strong>{{ campaign.name }}</strong>
                    <span>{{ campaign.campaignType }} - {{ campaign.productIds.length }} product{{ campaign.productIds.length === 1 ? '' : 's' }}</span>
                    <div class="hf-budget-bar" aria-label="Budget used">
                      <span [style.width.%]="budgetProgress(campaign)"></span>
                    </div>
                  </div>
                  <div class="hf-ad-row-status">
                    <app-status-badge [label]="campaign.status" [tone]="campaignTone(campaign.status)" />
                    @if (campaign.budget) {
                      <strong>{{ campaign.budget.spentAmount | currency:campaign.budget.currency:'symbol-narrow' }} spent</strong>
                    } @else {
                      <strong>No budget</strong>
                    }
                    <span>{{ campaign.startsAtUtc | date:'mediumDate' }} to {{ campaign.endsAtUtc | date:'mediumDate' }}</span>
                  </div>
                </a>
              }
            </div>

            <aside class="hf-ad-suggestion-card">
              <span class="eyebrow">Campaign suggestion</span>
              <h2>Promote only listings that are already clean</h2>
              <p>Use the listing assistant and campaign eligibility checks before adding budget. This avoids sending traffic to low-quality or blocked products.</p>
              <div class="hf-ad-suggestion-facts">
                <span><strong>{{ reviewCount() }}</strong> awaiting review</span>
                <span><strong>{{ warningCount() }}</strong> with warnings</span>
              </div>
              <a mat-flat-button routerLink="/seller/ads/new">Create campaign</a>
            </aside>
          </section>
        }
      }
    </section>
  `
})
export class SellerAdCampaignsPageComponent implements OnInit {
  private readonly adCampaignService = inject(SellerAdCampaignService);

  protected readonly campaigns = signal<SellerAdCampaignResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly activeCount = computed(() =>
    this.campaigns().filter(campaign => ['Active', 'Approved'].includes(campaign.status)).length);

  protected readonly reviewCount = computed(() =>
    this.campaigns().filter(campaign => ['Draft', 'PendingReview', 'Rejected'].includes(campaign.status)).length);

  protected readonly warningCount = computed(() =>
    this.campaigns().filter(campaign => !campaign.eligibility.isEligible).length);

  protected readonly adMetrics = computed<readonly {
    label: string;
    value: string;
    badge: string;
    tone: StatusBadgeTone;
  }[]>(() => {
    const campaigns = this.campaigns();
    const spent = campaigns.reduce((total, campaign) => total + (campaign.budget?.spentAmount ?? 0), 0);
    const promotedProducts = new Set(campaigns.flatMap(campaign => campaign.productIds)).size;

    return [
      { label: 'Ad spend', value: formatZar(spent), badge: 'Loaded campaigns', tone: 'accent' },
      { label: 'Campaigns', value: String(campaigns.length), badge: `${this.activeCount()} active`, tone: 'success' },
      { label: 'Products', value: String(promotedProducts), badge: 'Promoted listings', tone: 'neutral' },
      { label: 'Review queue', value: String(this.reviewCount()), badge: 'Drafts and review', tone: this.reviewCount() > 0 ? 'warning' : 'success' }
    ];
  });

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.campaigns.set(await this.adCampaignService.listCampaigns());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  protected budgetProgress(campaign: SellerAdCampaignResponse): number {
    if (!campaign.budget || campaign.budget.totalBudget <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((campaign.budget.spentAmount / campaign.budget.totalBudget) * 100));
  }

  protected campaignTone(status: string): StatusBadgeTone {
    if (['Active', 'Approved'].includes(status)) {
      return 'success';
    }

    if (['Draft', 'PendingReview', 'Paused'].includes(status)) {
      return 'warning';
    }

    if (['Rejected', 'Cancelled'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }
}

function formatZar(value: number): string {
  return `R${Math.round(value).toLocaleString('en-ZA')}`;
}
