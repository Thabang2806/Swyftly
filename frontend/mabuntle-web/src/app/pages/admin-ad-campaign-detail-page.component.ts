import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminAdCampaignDetailResponse } from '../admin/admin-ad-campaign.models';
import { AdminAdCampaignService } from '../admin/admin-ad-campaign.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-admin-ad-campaign-detail-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    AdminWorkspaceNavComponent,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />

      <a class="admin-back-link" routerLink="/ads">Back to ad queue</a>

      @if (isLoading()) {
        <div class="route-card">Loading campaign review...</div>
      } @else if (campaign()) {
        <div class="page-header">
          <span class="eyebrow">Ad campaign review</span>
          <h1>{{ campaign()?.name }}</h1>
          <p>{{ campaign()?.campaignType }} / {{ campaign()?.startsAtUtc | date:'mediumDate' }} to {{ campaign()?.endsAtUtc | date:'mediumDate' }}</p>
        </div>

        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (successMessage()) {
          <p class="auth-alert success" role="status">{{ successMessage() }}</p>
        }

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <article class="route-card admin-detail-card">
              <span class="status-pill">{{ campaign()?.status }}</span>
              <h2>Campaign</h2>
              <dl class="admin-facts">
                <div><dt>Seller</dt><dd>{{ campaign()?.seller?.displayName ?? 'Unnamed seller' }}</dd></div>
                <div><dt>Seller status</dt><dd>{{ campaign()?.seller?.verificationStatus ?? 'Unknown' }}</dd></div>
                <div><dt>Seller contact</dt><dd>{{ campaign()?.seller?.contactEmail ?? 'Not provided' }}</dd></div>
                <div><dt>Submitted</dt><dd>{{ campaign()?.submittedAtUtc | date:'medium' }}</dd></div>
                <div><dt>Run dates</dt><dd>{{ campaign()?.startsAtUtc | date:'mediumDate' }} to {{ campaign()?.endsAtUtc | date:'mediumDate' }}</dd></div>
                @if (campaign()?.rejectionReason) {
                  <div><dt>Latest reason</dt><dd>{{ campaign()?.rejectionReason }}</dd></div>
                }
              </dl>
            </article>

            <article class="route-card admin-detail-card">
              <h2>Budget</h2>
              @if (campaign()?.budget) {
                <dl class="admin-facts">
                  <div><dt>Daily budget</dt><dd>{{ campaign()?.budget?.dailyBudget | currency:(campaign()?.budget?.currency ?? 'ZAR'):'symbol-narrow' }}</dd></div>
                  <div><dt>Total budget</dt><dd>{{ campaign()?.budget?.totalBudget | currency:(campaign()?.budget?.currency ?? 'ZAR'):'symbol-narrow' }}</dd></div>
                  <div><dt>Max CPC</dt><dd>{{ campaign()?.budget?.maxCostPerClick | currency:(campaign()?.budget?.currency ?? 'ZAR'):'symbol-narrow' }}</dd></div>
                  <div><dt>Spent</dt><dd>{{ campaign()?.budget?.spentAmount | currency:(campaign()?.budget?.currency ?? 'ZAR'):'symbol-narrow' }}</dd></div>
                </dl>
              } @else {
                <p>No budget was recorded for this campaign.</p>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Promoted products</h2>
              @if ((campaign()?.products?.length ?? 0) === 0) {
                <p>No products are attached to this campaign.</p>
              } @else {
                <div class="admin-product-variants">
                  @for (product of campaign()?.products; track product.productId) {
                    <div>
                      <span class="status-pill">{{ product.status }}</span>
                      <strong>{{ product.title ?? 'Untitled product' }}</strong>
                      <span>{{ product.productId }}</span>
                      <span>Published {{ product.publishedAtUtc | date:'mediumDate' }}</span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Eligibility checks</h2>
              <span class="status-pill">{{ campaign()?.eligibility?.isEligible ? 'Eligible' : 'Blocked' }}</span>

              @if (campaign()?.eligibility?.sellerReasons?.length) {
                <div class="admin-product-risks">
                  <div>
                    <strong>Seller checks</strong>
                    @for (reason of campaign()?.eligibility?.sellerReasons; track reason) {
                      <small>{{ reason }}</small>
                    }
                  </div>
                </div>
              }

              @if (productEligibility().length === 0) {
                <p>No product eligibility detail was returned.</p>
              } @else {
                <div class="admin-product-risks">
                  @for (check of productEligibility(); track check.productId) {
                    <div>
                      <span class="status-pill">{{ check.isEligible ? 'Eligible' : 'Blocked' }}</span>
                      <strong>{{ productTitle(check.productId) }}</strong>
                      <span>Quality score {{ check.qualityScore }}</span>
                      @if (check.reasons.length > 0) {
                        @for (reason of check.reasons; track reason) {
                          <small>{{ reason }}</small>
                        }
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
              <button data-ui-button="primary" type="button" [disabled]="isSaving()" (click)="approve()">Approve campaign</button>

              <form [formGroup]="rejectForm" (ngSubmit)="reject()" class="admin-reason-form" novalidate>
                <label class="ui-field">
                  <span>Rejection reason</span>
                  <textarea rows="4" formControlName="reason"></textarea>
                  @if (rejectForm.controls.reason.hasError('required')) {
                    <span class="ui-field-error">Reason is required.</span>
                  }
                </label>
                <button data-ui-button="secondary" type="submit" [disabled]="isSaving()">Reject campaign</button>
              </form>
            </div>

            <div class="route-card admin-action-card">
              <h2>Audit trail</h2>
              @if ((campaign()?.auditTrail?.length ?? 0) === 0) {
                <p>No admin actions have been recorded for this campaign.</p>
              } @else {
                <ol class="audit-list">
                  @for (entry of campaign()?.auditTrail; track entry.id) {
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
        <p class="auth-alert error" role="alert">{{ errorMessage() ?? 'Campaign was not found.' }}</p>
      }
    </section>
  `
})
export class AdminAdCampaignDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly adminAdCampaignService = inject(AdminAdCampaignService);

  protected readonly campaign = signal<AdminAdCampaignDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly productEligibility = computed(() => this.campaign()?.eligibility.products ?? []);

  protected readonly rejectForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadCampaign();
  }

  protected async approve(): Promise<void> {
    const campaign = this.campaign();
    if (!campaign) {
      return;
    }

    await this.runAction(
      () => this.adminAdCampaignService.approveCampaign(campaign.adCampaignId),
      'Campaign approved.');
  }

  protected async reject(): Promise<void> {
    if (this.rejectForm.invalid) {
      this.rejectForm.markAllAsTouched();
      return;
    }

    const campaign = this.campaign();
    if (!campaign) {
      return;
    }

    const reason = this.rejectForm.getRawValue().reason.trim();
    await this.runAction(
      () => this.adminAdCampaignService.rejectCampaign(campaign.adCampaignId, { reason }),
      'Campaign rejected.');
    this.rejectForm.reset();
  }

  protected productTitle(productId: string): string {
    return this.campaign()?.products.find(product => product.productId === productId)?.title ?? productId;
  }

  private async loadCampaign(): Promise<void> {
    const campaignId = this.route.snapshot.paramMap.get('id');
    if (!campaignId) {
      this.errorMessage.set('Campaign id is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.campaign.set(await this.adminAdCampaignService.getCampaign(campaignId));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.campaign.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(
    action: () => Promise<AdminAdCampaignDetailResponse>,
    message: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.campaign.set(await action());
      this.successMessage.set(message);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }
}
