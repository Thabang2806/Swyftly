import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { SellerAdCampaignResponse, UpsertSellerAdCampaignRequest } from '../seller/seller-ad-campaign.models';
import { SellerAdCampaignService } from '../seller/seller-ad-campaign.service';
import { SellerProductSummaryResponse } from '../seller/seller-product.models';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-seller-ad-campaign-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent
  ],
  template: `
    <section class="page seller-ops-page seller-products hf-seller-ad-form-page">
      <app-seller-workspace-nav />

      <a class="admin-back-link" routerLink="/ads">Back to campaigns</a>

      <div class="page-header">
        <span class="eyebrow">Seller advertising</span>
        <h1>New ad campaign</h1>
        <p>Select published products, set campaign dates, and submit the draft for review when ready.</p>
      </div>

      <div class="route-card">
        <span class="status-pill">Eligibility gate</span>
        <h2>Campaigns promote live products only</h2>
        <p>Choose products that are published, in stock, and free of blocking moderation issues. Admin approval is required before any promoted placement becomes active.</p>
      </div>

      @if (errorMessage()) {
        <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
      }

      @if (createdCampaign()) {
        <p class="auth-alert success" role="status">Campaign draft created.</p>
      }

      @if (isLoading()) {
        <div class="route-card">Loading products...</div>
      } @else {
        <form [formGroup]="campaignForm" (ngSubmit)="saveDraft()" class="wizard-form route-card" novalidate>
          <h2>Campaign setup</h2>

          <label class="ui-field">
            <span>Name</span>
            <input formControlName="name" />
            @if (campaignForm.controls.name.hasError('required')) {
              <span class="ui-field-error">Name is required.</span>
            }
          </label>

          <label class="ui-field">
            <span>Campaign type</span>
            <select formControlName="campaignType">
              @for (type of campaignTypes; track type) {
                <option [value]="type">{{ type }}</option>
              }
            </select>
          </label>

          <label class="ui-field">
            <span>Products to promote</span>
            <select formControlName="productIds" multiple>
              @for (product of products(); track product.productId) {
                <option [value]="product.productId">
                  {{ product.title ?? 'Untitled product' }} / {{ product.status }}
                </option>
              }
            </select>
            @if (campaignForm.controls.productIds.hasError('required')) {
              <span class="ui-field-error">Select at least one product.</span>
            }
          </label>

          @if (selectionWarnings().length > 0) {
            <div class="auth-alert error" role="alert">
              @for (warning of selectionWarnings(); track warning) {
                <span>{{ warning }}</span>
              }
            </div>
          }

          <div class="form-grid">
            <label class="ui-field">
              <span>Start date</span>
              <input type="datetime-local" formControlName="startsAtLocal" />
            </label>

            <label class="ui-field">
              <span>End date</span>
              <input type="datetime-local" formControlName="endsAtLocal" />
            </label>
          </div>

          <div class="form-grid">
            <label class="ui-field">
              <span>Currency</span>
              <input maxlength="3" formControlName="currency" />
            </label>

            <label class="ui-field">
              <span>Daily budget</span>
              <input type="number" min="1" formControlName="dailyBudget" />
            </label>
          </div>

          <div class="form-grid">
            <label class="ui-field">
              <span>Total budget</span>
              <input type="number" min="1" formControlName="totalBudget" />
            </label>

            <label class="ui-field">
              <span>Max CPC</span>
              <input type="number" min="1" formControlName="maxCostPerClick" />
            </label>
          </div>

          <div class="auth-actions">
            <button data-ui-button="primary" type="submit" [disabled]="isSaving()">Save draft</button>
            @if (createdCampaign()) {
              <button data-ui-button="secondary" type="button" [disabled]="isSaving()" (click)="submitForReview()">Submit for review</button>
              <a data-ui-button="secondary" [routerLink]="['/ads', createdCampaign()?.adCampaignId]">Open campaign</a>
            }
          </div>
        </form>

        @if (createdCampaign()?.eligibility; as eligibility) {
          <article class="route-card admin-detail-card">
            <span class="status-pill">{{ eligibility.isEligible ? 'Eligible' : 'Warnings' }}</span>
            <h2>Eligibility</h2>
            @if (eligibility.sellerReasons.length > 0) {
              <p>{{ eligibility.sellerReasons.join(', ') }}</p>
            }
            <div class="admin-product-risks">
              @for (check of eligibility.products; track check.productId) {
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
        }
      }
    </section>
  `
})
export class SellerAdCampaignFormPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly productService = inject(SellerProductService);
  private readonly adCampaignService = inject(SellerAdCampaignService);

  protected readonly campaignTypes = ['FeaturedProduct', 'SponsoredSearch', 'FeaturedStorefront', 'CategorySpotlight'];
  protected readonly products = signal<SellerProductSummaryResponse[]>([]);
  protected readonly createdCampaign = signal<SellerAdCampaignResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly selectionWarnings = computed(() => {
    const productIds = this.campaignForm.controls.productIds.value;
    return this.products()
      .filter(product => productIds.includes(product.productId) && product.status !== 'Published')
      .map(product => `${product.title ?? 'Selected product'} is ${product.status}; ad campaigns require published products.`);
  });

  protected readonly campaignForm = this.formBuilder.group({
    name: ['', [Validators.required]],
    campaignType: ['FeaturedProduct', [Validators.required]],
    productIds: this.formBuilder.control<string[]>([], [Validators.required]),
    startsAtLocal: [toLocalInputValue(new Date(Date.now() + 24 * 60 * 60 * 1000)), [Validators.required]],
    endsAtLocal: [toLocalInputValue(new Date(Date.now() + 15 * 24 * 60 * 60 * 1000)), [Validators.required]],
    currency: ['ZAR', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
    dailyBudget: [100, [Validators.required, Validators.min(1)]],
    totalBudget: [1000, [Validators.required, Validators.min(1)]],
    maxCostPerClick: [5, [Validators.required, Validators.min(1)]]
  });

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.products.set(await this.productService.listProducts());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async saveDraft(): Promise<void> {
    if (this.campaignForm.invalid || this.isSaving()) {
      this.campaignForm.markAllAsTouched();
      return;
    }

    await this.runAction(async () => {
      const request = this.createRequest();
      const existing = this.createdCampaign();
      const campaign = existing
        ? await this.adCampaignService.updateCampaign(existing.adCampaignId, request)
        : await this.adCampaignService.createCampaign(request);
      this.createdCampaign.set(campaign);
    });
  }

  protected async submitForReview(): Promise<void> {
    const campaign = this.createdCampaign();
    if (!campaign) {
      return;
    }

    await this.runAction(async () => {
      const submitted = await this.adCampaignService.submitForReview(campaign.adCampaignId);
      this.createdCampaign.set(submitted);
      await this.router.navigate(['/ads', submitted.adCampaignId]);
    });
  }

  protected productTitle(productId: string): string {
    return this.products().find(product => product.productId === productId)?.title ?? productId;
  }

  private async runAction(action: () => Promise<void>): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);

    try {
      await action();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private createRequest(): UpsertSellerAdCampaignRequest {
    const value = this.campaignForm.getRawValue();
    return {
      name: value.name,
      campaignType: value.campaignType,
      startsAtUtc: new Date(value.startsAtLocal).toISOString(),
      endsAtUtc: new Date(value.endsAtLocal).toISOString(),
      productIds: value.productIds,
      budget: {
        currency: value.currency.toUpperCase(),
        dailyBudget: Number(value.dailyBudget),
        totalBudget: Number(value.totalBudget),
        maxCostPerClick: Number(value.maxCostPerClick)
      }
    };
  }
}

function toLocalInputValue(date: Date): string {
  const offsetDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return offsetDate.toISOString().slice(0, 16);
}
