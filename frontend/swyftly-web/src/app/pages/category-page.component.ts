import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { ProductCardComponent } from '../shop/product-card.component';
import { ProductSearchItemResponse, PublicCategoryResponse } from '../shop/public-catalog.models';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { ProductVisualFallbackComponent } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-category-page',
  imports: [
    EmptyStateComponent,
    LuxuryPublicStylesComponent,
    MatButtonModule,
    PageHeaderComponent,
    ProductCardComponent,
    ProductVisualFallbackComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <app-luxury-public-styles />
    <section class="page shop-surface category-surface">
      <a class="admin-back-link" routerLink="/shop">Back to shop</a>

      <section class="category-hero hf-category-hero">
        <div>
          <app-page-header
            eyebrow="Category edit"
            [heading]="category()?.name ?? 'Category'"
            [description]="categoryHeroCopy()"
          />
          <div class="category-hero-meta">
            <app-status-badge [label]="categoryPath()" tone="accent" />
            <app-status-badge [label]="products().length + ' listed here'" />
          </div>
        </div>
        <div class="category-visual-panel" aria-hidden="true">
          <app-product-visual-fallback
            [label]="categoryPath()"
            [title]="category()?.name ?? 'Category edit'"
            [tone]="categoryTone()"
          />
        </div>
      </section>

      @if (childCategories().length > 0) {
        <section class="subcategory-strip" aria-label="Subcategories">
          <span>Explore within {{ category()?.name }}</span>
          <div>
            @for (child of childCategories(); track child.categoryId) {
              <a mat-stroked-button [routerLink]="['/category', child.slug]">{{ child.name }}</a>
            }
          </div>
        </section>
      }

      @if (isLoading()) {
        <div class="route-card">Loading category products...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (products().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="No products"
            heading="No products in this category"
            message="Published products will appear here when verified sellers list items in this edit."
          >
            <a mat-flat-button routerLink="/shop">Browse all products</a>
          </app-empty-state>
        } @else {
          <div class="category-product-header">
            <div>
              <h2>Products in this edit</h2>
              <p>Listings are shown with seller, stock, and price context.</p>
            </div>
            <a mat-stroked-button routerLink="/shop">Adjust filters</a>
          </div>

          <div class="product-grid">
            @for (product of products(); track product.productId) {
              <app-product-card [product]="product"></app-product-card>
            }
          </div>
        }
      }
    </section>
  `
})
export class CategoryPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly publicCatalogService = inject(PublicCatalogService);

  protected readonly categories = signal<PublicCategoryResponse[]>([]);
  protected readonly products = signal<ProductSearchItemResponse[]>([]);
  protected readonly category = signal<PublicCategoryResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly categoryPath = computed(() => {
    const selected = this.category();
    if (!selected) {
      return 'Published products by category';
    }

    return this.pathForCategory(selected);
  });
  protected readonly childCategories = computed(() => {
    const selected = this.category();
    if (!selected) {
      return [];
    }

    return this.categories()
      .filter(category => category.parentCategoryId === selected.categoryId)
      .sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name));
  });

  async ngOnInit(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get('slug');
    if (!slug) {
      this.errorMessage.set('Category slug is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const categories = await this.publicCatalogService.getCategories();
      this.categories.set(categories);
      this.category.set(categories.find(category => category.slug === slug) ?? null);
      const response = await this.publicCatalogService.searchProducts({
        categorySlug: slug,
        pageSize: 24
      });
      this.products.set(response.items);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.products.set([]);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected categoryHeroCopy(): string {
    const selected = this.category();
    if (!selected) {
      return 'Explore marketplace products grouped by category, seller, price, and stock signals.';
    }

    return `Browse ${selected.name.toLowerCase()} with seller and stock details visible before checkout.`;
  }

  protected categoryTone(): 'dress' | 'jewel' | 'beauty' | 'bag' | 'shoe' | 'neutral' {
    const text = `${this.category()?.name ?? ''} ${this.categoryPath()}`.toLowerCase();
    if (/(jewel|ring|earring|necklace|bracelet)/.test(text)) {
      return 'jewel';
    }

    if (/(beauty|skin|makeup|lip|hair|fragrance)/.test(text)) {
      return 'beauty';
    }

    if (/(bag|accessor|wallet|purse)/.test(text)) {
      return 'bag';
    }

    if (/(shoe|heel|sneaker|boot|sandal)/.test(text)) {
      return 'shoe';
    }

    if (/(dress|fashion|clothing|women|men|apparel)/.test(text)) {
      return 'dress';
    }

    return 'neutral';
  }

  private pathForCategory(selected: PublicCategoryResponse): string {
    const byId = new Map(this.categories().map(category => [category.categoryId, category]));
    const names: string[] = [];
    let current: PublicCategoryResponse | undefined = selected;
    while (current) {
      names.unshift(current.name);
      current = current.parentCategoryId ? byId.get(current.parentCategoryId) : undefined;
    }

    return names.join(' > ');
  }
}
