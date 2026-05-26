import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AdminAdCampaignSummaryResponse } from '../admin/admin-ad-campaign.models';
import { AdminAdCampaignService } from '../admin/admin-ad-campaign.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-admin-ad-campaigns-page',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, AdminWorkspaceNavComponent, RouterLink],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />

      <a class="admin-back-link" routerLink="/admin">Back to admin</a>

      <div class="page-header">
        <span class="eyebrow">Admin review</span>
        <h1>Ad campaign review queue</h1>
        <p>Review seller campaign submissions before they can run in marketplace surfaces.</p>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading ad campaign reviews...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (pendingCampaigns().length === 0 && !errorMessage()) {
          <div class="route-card">
            <span class="status-pill">Clear</span>
            <h2>No campaigns pending review</h2>
            <p>Seller ad campaign submissions will appear here.</p>
          </div>
        } @else {
          <div class="admin-table" role="table" aria-label="Pending ad campaign reviews">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Campaign</span>
              <span role="columnheader">Seller</span>
              <span role="columnheader">Budget</span>
              <span role="columnheader">Submitted</span>
              <span role="columnheader">Action</span>
            </div>

            @for (campaign of pendingCampaigns(); track campaign.adCampaignId) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ campaign.name }}</strong>
                  <small>{{ campaign.campaignType }} / {{ campaign.productCount }} product{{ campaign.productCount === 1 ? '' : 's' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ campaign.sellerDisplayName ?? 'Unnamed seller' }}</strong>
                  <small>{{ campaign.sellerId }}</small>
                </span>
                <span role="cell">
                  @if (campaign.totalBudget !== null) {
                    {{ campaign.totalBudget | currency:(campaign.currency ?? 'ZAR'):'symbol-narrow' }}
                  } @else {
                    Not set
                  }
                </span>
                <span role="cell">{{ campaign.submittedAtUtc | date:'medium' }}</span>
                <span role="cell">
                  <a mat-stroked-button [routerLink]="['/admin/ads', campaign.adCampaignId]">Review</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class AdminAdCampaignsPageComponent implements OnInit {
  private readonly adminAdCampaignService = inject(AdminAdCampaignService);

  protected readonly pendingCampaigns = signal<AdminAdCampaignSummaryResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadPendingCampaigns();
  }

  private async loadPendingCampaigns(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.pendingCampaigns.set(await this.adminAdCampaignService.getPendingCampaigns());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
