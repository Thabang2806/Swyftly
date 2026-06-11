import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FRONTEND_HOSTS } from '../frontend-experience';
import { DashboardCardComponent } from '../shared/ui/dashboard-card.component';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { ProductVisualFallbackComponent } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';

@Component({
  selector: 'app-sell-on-mabuntle-page',
  imports: [
    DashboardCardComponent,
    LuxuryPublicStylesComponent,
    PageHeaderComponent,
    ProductVisualFallbackComponent,
    RouterLink,
    StatusBadgeComponent
  ],
  template: `
    <app-luxury-public-styles />
    <section class="sell-page">
      <section class="sell-hero" aria-labelledby="sell-title">
        <div class="sell-hero-copy">
          <span class="eyebrow">Sell on Mabuntle</span>
          <h1 id="sell-title">Build a reviewed fashion, beauty, or jewellery storefront.</h1>
          <p>
            Mabuntle gives sellers a guided path from registration to verification, reviewed listings, stock control,
            fulfilment, support, ads, and payout operations.
          </p>

          <div class="market-home-actions">
            <a class="ui-button ui-button--primary" [href]="sellerRegisterUrl">Create seller account</a>
            <a class="ui-button" [href]="sellerLoginUrl">Seller sign in</a>
          </div>

          <div class="market-home-trust-strip" aria-label="Seller trust signals">
            <app-status-badge label="Verification required" tone="accent" />
            <app-status-badge label="Listings reviewed" />
            <app-status-badge label="Inventory tools included" tone="success" />
          </div>
        </div>

        <div class="sell-hero-visual" aria-label="Seller workspace preview">
          <app-product-visual-fallback label="Seller edit" title="Boutique catalog" tone="dress" />
          <div class="sell-hero-note">
            <strong>Operational workspace</strong>
            <span>Products, stock, orders, returns, ads, support, and notifications in one seller dashboard.</span>
          </div>
        </div>
      </section>

      <section class="page sell-section">
        <app-page-header
          eyebrow="How selling works"
          heading="A review-led path from account to storefront"
          description="The public application starts with registration, then seller onboarding and marketplace review happen inside the seller workspace."
        />

        <div class="sell-steps-grid">
          @for (step of sellingSteps; track step.title; let index = $index) {
            <article class="sell-step-card">
              <span>{{ index + 1 }}</span>
              <h2>{{ step.title }}</h2>
              <p>{{ step.description }}</p>
            </article>
          }
        </div>
      </section>

      <section class="page sell-section">
        <div class="sell-readiness-layout">
          <div>
            <app-page-header
              eyebrow="Before you apply"
              heading="Prepare the details reviewers and buyers need"
              description="A complete seller profile makes admin review, product approval, fulfilment, and customer support easier to operate."
            />
            <div class="sell-checklist">
              @for (item of readinessChecklist; track item) {
                <span>{{ item }}</span>
              }
            </div>
          </div>

          <aside class="sell-policy-card" aria-label="Seller review policy">
            <app-status-badge label="Review policy" tone="accent" />
            <h2>Quality and trust come before publishing.</h2>
            <p>
              Sellers can prepare drafts before verification, but storefront eligibility, product publishing, and ad
              campaign activation depend on marketplace review outcomes.
            </p>
            <a class="ui-button" [href]="sellerRegisterUrl">Start registration</a>
          </aside>
        </div>
      </section>

      <section class="page sell-section">
        <app-page-header
          eyebrow="Seller operations"
          heading="Tools for running the store after approval"
          description="These capabilities are already handled through the seller workspace and existing backend workflows."
        />

        <div class="route-grid">
          @for (item of operationalCards; track item.heading) {
            <app-dashboard-card [eyebrow]="item.eyebrow" [heading]="item.heading" [description]="item.description">
              <span class="sell-card-footnote">{{ item.note }}</span>
            </app-dashboard-card>
          }
        </div>
      </section>

      <section class="page sell-section">
        <div class="sell-deferred-panel">
          <div>
            <app-status-badge label="Honest roadmap" />
            <h2>Some production integrations are still future work.</h2>
            <p>
              Mabuntle currently keeps seller workflows provider-neutral where real external integrations are not yet
              selected. Real carrier adapters, sensitive bank-detail storage, and external payout-provider verification
              remain planned hardening work.
            </p>
          </div>
          <div class="market-home-actions">
            <a class="ui-button ui-button--primary" [href]="sellerRegisterUrl">Create seller account</a>
            <a class="ui-button" routerLink="/shop">Browse marketplace</a>
          </div>
        </div>
      </section>
    </section>
  `
})
export class SellOnMabuntlePageComponent {
  protected readonly sellerRegisterUrl = `${FRONTEND_HOSTS.seller}/register/seller`;
  protected readonly sellerLoginUrl = `${FRONTEND_HOSTS.seller}/login`;

  protected readonly sellingSteps = [
    {
      title: 'Create a seller account',
      description: 'Register with seller access, then sign in to begin the onboarding checklist.'
    },
    {
      title: 'Complete onboarding',
      description: 'Add profile, storefront, fulfilment address, delivery methods, and payout reference details.'
    },
    {
      title: 'Wait for verification',
      description: 'Admins review seller details before storefronts, published products, and ads become eligible.'
    },
    {
      title: 'Prepare product drafts',
      description: 'Add images, attributes, variants, pricing, stock, and optional AI-assisted listing suggestions.'
    },
    {
      title: 'Submit listings for review',
      description: 'Product, listing revision, variant/pricing revision, and ad approvals keep buyer-facing catalog changes controlled.'
    },
    {
      title: 'Fulfil and support orders',
      description: 'Use seller orders, inventory, returns, support, notifications, payouts, ads, and analytics from the workspace.'
    }
  ];

  protected readonly readinessChecklist = [
    'Store name, slug, logo, and buyer-facing description',
    'Business contact details and fulfilment address',
    'Delivery methods, rates, and supported regions',
    'Clear product images and alt text',
    'Variants with SKU, size, colour, price, barcode, and stock',
    'Product attributes, tags, and honest safety notes where relevant',
    'Payout provider reference for finance review'
  ];

  protected readonly operationalCards = [
    {
      eyebrow: 'Verification',
      heading: 'Seller and listing review',
      description: 'Admin review controls seller approval, product publishing, listing revisions, variant/pricing revisions, and ad activation.',
      note: 'Review outcomes appear in seller notifications.'
    },
    {
      eyebrow: 'Inventory',
      heading: 'Stock and barcode control',
      description: 'Sellers can adjust stock, bulk import CSV updates, export inventory, and inspect seller-driven movement history.',
      note: 'Reservations and order stock-ledger expansion remain future work.'
    },
    {
      eyebrow: 'Fulfilment',
      heading: 'Manual and provider-neutral delivery',
      description: 'Orders support ready-to-ship, tracking, delivery confirmation, exceptions, pickup points, and carrier-provider foundations.',
      note: 'Real carrier adapters are not enabled yet.'
    },
    {
      eyebrow: 'Finance',
      heading: 'Payout visibility and controls',
      description: 'Sellers can view balances and payout history, while finance/admin workflows control payout processing and profile changes.',
      note: 'Sensitive bank-detail storage is deferred.'
    },
    {
      eyebrow: 'Growth',
      heading: 'Ads and analytics',
      description: 'Verified sellers can create ad campaigns for eligible products and review campaign and sales analytics.',
      note: 'Campaigns require admin review before activation.'
    },
    {
      eyebrow: 'Support',
      heading: 'Operational help and notifications',
      description: 'Seller support tickets, moderation history, dashboard alerts, and transactional notifications keep review outcomes visible.',
      note: 'Seller notification preferences and realtime delivery remain future work.'
    }
  ];
}
