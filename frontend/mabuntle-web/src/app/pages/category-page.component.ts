import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
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
    PageHeaderComponent,
    ProductCardComponent,
    ProductVisualFallbackComponent,
    ReactiveFormsModule,
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
              <a data-ui-button="secondary" [routerLink]="['/category', child.slug]">{{ child.name }}</a>
            }
          </div>
        </section>
      }

      <form [formGroup]="filtersForm" (ngSubmit)="search(1)" class="category-filter-bar" novalidate>
        <label class="ui-field">
          <span>Search in category</span>
          <input formControlName="query">
        </label>

        <label class="ui-field">
          <span>Availability</span>
          <select formControlName="availability">
            <option value="">All products</option>
            <option value="in_stock">In stock</option>
            <option value="out_of_stock">Out of stock</option>
          </select>
        </label>

        <label class="ui-field">
          <span>Sort</span>
          <select formControlName="sort">
            <option value="newest">Newest</option>
            <option value="price_asc">Price low to high</option>
            <option value="price_desc">Price high to low</option>
            <option value="relevance">Relevance</option>
          </select>
        </label>

        <div class="category-filter-actions">
          <button data-ui-button="primary" type="submit" [disabled]="isLoading()">Apply</button>
          <button data-ui-button="secondary" type="button" [disabled]="isLoading()" (click)="clearFilters()">Clear</button>
        </div>
      </form>

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
            <a data-ui-button="primary" routerLink="/shop">Browse all products</a>
          </app-empty-state>
        } @else {
          <div class="category-product-header">
            <div>
              <h2>Products in this edit</h2>
              <p>{{ totalCount() }} matching published product{{ totalCount() === 1 ? '' : 's' }} shown with seller, stock, and price context.</p>
            </div>
            <a data-ui-button="secondary" routerLink="/shop">Adjust filters</a>
          </div>

          <div class="product-grid">
            @for (product of products(); track product.productId) {
              <app-product-card [product]="product"></app-product-card>
            }
          </div>

          <div class="shop-pagination">
            <button data-ui-button="secondary" type="button" [disabled]="page() <= 1 || isLoading()" (click)="search(page() - 1)">Previous</button>
            <button data-ui-button="secondary" type="button" [disabled]="page() * pageSize() >= totalCount() || isLoading()" (click)="search(page() + 1)">Next</button>
          </div>
        }
      }
    </section>
  `
})
export class CategoryPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly publicCatalogService = inject(PublicCatalogService);
  private routeSubscription: Subscription | null = null;

  protected readonly categories = signal<PublicCategoryResponse[]>([]);
  protected readonly products = signal<ProductSearchItemResponse[]>([]);
  protected readonly category = signal<PublicCategoryResponse | null>(null);
  protected readonly currentSlug = signal('');
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(24);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly filtersForm = this.formBuilder.group({
    query: [''],
    availability: [''],
    sort: ['newest']
  });
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

  ngOnInit(): void {
    this.routeSubscription = this.route.paramMap.subscribe(paramMap => {
      void this.loadCategory(paramMap.get('slug'));
    });
  }

  ngOnDestroy(): void {
    this.routeSubscription?.unsubscribe();
  }

  protected async search(page: number): Promise<void> {
    const slug = this.currentSlug();
    if (!slug) {
      this.errorMessage.set('Category slug is missing.');
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const filters = this.filtersForm.getRawValue();
      const response = await this.publicCatalogService.searchProducts({
        categorySlug: slug,
        query: filters.query,
        inStock: this.toAvailability(filters.availability),
        sort: filters.sort,
        page,
        pageSize: 24
      });
      this.products.set(response.items);
      this.totalCount.set(response.totalCount);
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.products.set([]);
      this.totalCount.set(0);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset({
      query: '',
      availability: '',
      sort: 'newest'
    });
    await this.search(1);
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

  private async loadCategory(slug: string | null): Promise<void> {
    if (!slug) {
      this.currentSlug.set('');
      this.errorMessage.set('Category slug is missing.');
      this.isLoading.set(false);
      return;
    }

    this.currentSlug.set(slug);
    this.filtersForm.reset({
      query: '',
      availability: '',
      sort: 'newest'
    });
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const categories = await this.publicCatalogService.getCategories();
      this.categories.set(categories);
      this.category.set(categories.find(category => category.slug === slug) ?? null);
      await this.search(1);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.products.set([]);
      this.totalCount.set(0);
    } finally {
      this.isLoading.set(false);
    }
  }

  private toAvailability(value: string): boolean | null {
    if (value === 'in_stock') {
      return true;
    }

    if (value === 'out_of_stock') {
      return false;
    }

    return null;
  }
}
