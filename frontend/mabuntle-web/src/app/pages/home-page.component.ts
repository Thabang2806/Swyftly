import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardCardComponent } from '../shared/ui/dashboard-card.component';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';

@Component({
  selector: 'app-home-page',
  imports: [DashboardCardComponent, LuxuryPublicStylesComponent, PageHeaderComponent, ProductVisualFallbackComponent, RouterLink, StatusBadgeComponent],
  template: `
    <app-luxury-public-styles />
    <section class="market-home">
      <section class="market-home-hero" aria-labelledby="home-title">
        <div class="market-home-hero-copy">
          <span class="eyebrow">AI-powered fashion marketplace</span>
          <h1 id="home-title">Shop local style, beauty, and jewellery. Mabuntle.</h1>
          <p>
            Discover curated products from South African boutiques, independent sellers, beauty creators, and jewellery makers with secure checkout and buyer support paths.
          </p>

          <div class="market-home-actions">
            <a data-ui-button="primary" routerLink="/shop">Shop new arrivals</a>
            <a data-ui-button="secondary" routerLink="/sell">Start selling</a>
          </div>

          <a class="market-search-entry" routerLink="/shop" aria-label="Open product search">
            Search dresses, jewellery, skincare, accessories, and more
          </a>

          <div class="market-home-trust-strip" aria-label="Marketplace trust signals">
            <app-status-badge label="Verified sellers" tone="success" />
            <app-status-badge label="Reviewed listings" tone="accent" />
            <app-status-badge label="Buyer support paths" />
          </div>
        </div>

        <div class="market-home-showcase" aria-label="Featured marketplace categories">
          @for (item of showcaseItems; track item.title) {
            <article class="market-showcase-card" [class.market-showcase-card--large]="item.large" [class]="item.toneClass">
              <span>{{ item.kicker }}</span>
              <strong>{{ item.title }}</strong>
              <small>{{ item.detail }}</small>
            </article>
          }
        </div>
      </section>

      <section class="page market-home-section">
        <app-page-header
          eyebrow="Featured today"
          heading="Curated style cues from the marketplace"
          description="These are presentation cues from the high-fidelity direction. Live inventory still comes from the shop APIs."
        />

        <div class="market-featured-grid">
          @for (item of featuredEdits; track item.title) {
            <a class="market-feature-card" routerLink="/shop">
              <div class="market-feature-card-media">
                <app-product-visual-fallback [label]="item.kicker" [title]="item.title" [tone]="item.tone" />
              </div>
              <div class="market-feature-card-copy">
                <span>{{ item.sellerCue }}</span>
                <h2>{{ item.title }}</h2>
                <p>{{ item.description }}</p>
              </div>
            </a>
          }
        </div>
      </section>

      <section class="page market-home-section">
        <app-page-header
          eyebrow="Shop by edit"
          heading="Start with what you need"
          description="Mabuntle keeps discovery simple: find the product, check the seller, then move to cart when the details are right."
        />

        <div class="market-category-grid">
          @for (category of categoryCards; track category.title) {
            <a class="market-category-card" routerLink="/shop">
              <app-status-badge [label]="category.kicker" tone="accent" />
              <h2>{{ category.title }}</h2>
              <p>{{ category.description }}</p>
            </a>
          }
        </div>
      </section>

      <section class="page market-home-section">
        <div class="market-home-seller-band">
          <div>
            <app-status-badge label="Sellers" tone="accent" />
            <h2>List products with guided seller tools.</h2>
            <p>
              Sellers can create product drafts, add variants and stock, review AI suggestions, and submit listings for marketplace review.
            </p>
          </div>
          <div class="market-home-actions">
            <a data-ui-button="primary" routerLink="/sell">Learn about selling</a>
            <a data-ui-button="secondary" routerLink="/login">Seller sign in</a>
          </div>
        </div>
      </section>

      <section class="page market-home-section">
        <app-page-header
          eyebrow="Marketplace trust"
          heading="Built for careful buying and selling"
          description="The product experience should make quality, seller status, stock, checkout, and support paths easy to understand."
        />

        <div class="route-grid">
          @for (item of trustCards; track item.heading) {
            <app-dashboard-card [eyebrow]="item.eyebrow" [heading]="item.heading" [description]="item.description">
              <a data-ui-button="ghost" [routerLink]="item.route">{{ item.action }}</a>
            </app-dashboard-card>
          }
        </div>
      </section>
    </section>
  `
})
export class HomePageComponent {
  protected readonly showcaseItems = [
    { kicker: 'New edit', title: 'Occasionwear', detail: 'Dresses, sets, and finishing pieces', large: true, toneClass: 'market-showcase-card--dress' },
    { kicker: 'Daily', title: 'Accessories', detail: 'Bags, belts, and jewellery', large: false, toneClass: 'market-showcase-card--bag' },
    { kicker: 'Beauty', title: 'Skincare', detail: 'Care routines and essentials', large: false, toneClass: 'market-showcase-card--beauty' }
  ];

  protected readonly featuredEdits: Array<{
    kicker: string;
    title: string;
    tone: ProductVisualTone;
    sellerCue: string;
    description: string;
  }> = [
    {
      kicker: 'Occasion',
      title: 'Eveningwear edit',
      tone: 'dress',
      sellerCue: 'Boutique-led fashion',
      description: 'Look for formal pieces with clear variant, seller, and stock context.'
    },
    {
      kicker: 'Trending',
      title: 'Jewellery finish',
      tone: 'jewel',
      sellerCue: 'Independent makers',
      description: 'Use product detail pages to compare materials, seller storefronts, and reviews.'
    },
    {
      kicker: 'Beauty',
      title: 'Glow routine',
      tone: 'beauty',
      sellerCue: 'Beauty creators',
      description: 'Beauty products surface seller context and listing review signals before checkout.'
    },
    {
      kicker: 'Featured',
      title: 'Accessory pairing',
      tone: 'bag',
      sellerCue: 'Marketplace curation',
      description: 'Browse bags, shoes, and accessories alongside outfit-led search terms.'
    }
  ];

  protected readonly categoryCards = [
    {
      kicker: 'Fashion',
      title: 'Clothing',
      description: 'Browse everyday staples, occasion looks, and seasonal pieces from marketplace sellers.'
    },
    {
      kicker: 'Finish',
      title: 'Jewellery and accessories',
      description: 'Find pieces that complete the outfit, from statement jewellery to practical extras.'
    },
    {
      kicker: 'Care',
      title: 'Beauty',
      description: 'Explore beauty products with clear seller, stock, and product detail context.'
    }
  ];

  protected readonly trustCards = [
    {
      eyebrow: 'Buyers',
      heading: 'Product detail first',
      description: 'Product pages surface variants, seller information, stock state, and checkout actions in one place.',
      action: 'Browse shop',
      route: '/shop'
    },
    {
      eyebrow: 'Sellers',
      heading: 'Guided listing flow',
      description: 'Seller tools support onboarding, product drafts, images, variants, stock, and listing review.',
      action: 'Learn about selling',
      route: '/sell'
    },
    {
      eyebrow: 'Support',
      heading: 'Operational review',
      description: 'Admin workflows review sellers, products, campaigns, reports, audit history, and finance operations.',
      action: 'Sign in',
      route: '/login'
    }
  ];
}
