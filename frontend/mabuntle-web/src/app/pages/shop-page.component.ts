import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { ProductCardComponent } from '../shop/product-card.component';
import { ProductSearchItemResponse, PublicCategoryResponse } from '../shop/public-catalog.models';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { LuxuryPublicStylesComponent } from '../shared/ui/luxury-public-styles.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

type ShopFilterKey = 'query' | 'categorySlug' | 'availability' | 'minPrice' | 'maxPrice' | 'size' | 'colour' | 'material' | 'sort';

@Component({
  selector: 'app-shop-page',
  imports: [
    EmptyStateComponent,
    LuxuryPublicStylesComponent,
    ProductCardComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <app-luxury-public-styles />
    <section class="page shop-surface hf-shop-surface">
      <div class="shop-hero hf-shop-hero">
        <div class="shop-search-title">
          <span class="eyebrow">Search results</span>
          <h1>{{ searchHeading() }}</h1>
          <p>{{ resultSummary() }}</p>
        </div>

        <div class="shop-hero-actions">
          <app-status-badge label="Published catalog" tone="accent" />
          <app-status-badge [label]="sortLabel()" />
          <a data-ui-button="secondary" routerLink="/">Marketplace home</a>
        </div>

        <div class="shop-quick-links" aria-label="Category quick filters">
          <button data-ui-button="secondary" type="button" [disabled]="isLoading()" (click)="applyCategory(null)">All products</button>
          @for (category of quickCategories(); track category.categoryId) {
            <button data-ui-button="secondary" type="button" [disabled]="isLoading()" (click)="applyCategory(category.slug)">
              {{ category.name }}
            </button>
          }
        </div>
      </div>

      <button class="shop-filter-toggle" data-ui-button="secondary" type="button" (click)="filtersExpanded.set(!filtersExpanded())">
        {{ filtersExpanded() ? 'Hide filters' : 'Show filters' }}
      </button>

      <div class="shop-layout">
        <form
          [formGroup]="filtersForm"
          (ngSubmit)="search(1)"
          class="shop-filters"
          [class.shop-filters--open]="filtersExpanded()"
          novalidate
        >
          <div class="shop-filter-heading">
            <strong>Refine results</strong>
            @if (activeFilterChips().length > 0) {
              <span>{{ activeFilterChips().length }} active</span>
            }
          </div>

          <div class="shop-filter-facets" aria-label="Quick refinements">
            <span>Quick refinements</span>
            <div>
              <button data-ui-button="secondary" type="button" [disabled]="isLoading()" (click)="applyAvailability('in_stock')">In stock</button>
              @for (category of quickCategories().slice(0, 3); track category.categoryId) {
                <button data-ui-button="secondary" type="button" [disabled]="isLoading()" (click)="applyCategory(category.slug)">
                  {{ category.name }}
                </button>
              }
            </div>
          </div>

          <label class="ui-field">
            <span>Search</span>
            <input formControlName="query">
          </label>

          <label class="ui-field">
            <span>Category</span>
            <select formControlName="categorySlug">
              <option value="">All categories</option>
              @for (category of categories(); track category.categoryId) {
                <option [ngValue]="category.slug">{{ categoryLabel(category) }}</option>
              }
            </select>
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
            <span>Min price</span>
            <input type="number" min="0" formControlName="minPrice">
          </label>

          <label class="ui-field">
            <span>Max price</span>
            <input type="number" min="0" formControlName="maxPrice">
          </label>

          <label class="ui-field">
            <span>Size</span>
            <input formControlName="size">
          </label>

          <label class="ui-field">
            <span>Colour</span>
            <input formControlName="colour">
          </label>

          <label class="ui-field">
            <span>Material</span>
            <input formControlName="material">
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

          <div class="shop-filter-actions">
            <button data-ui-button="primary" type="submit" [disabled]="isLoading()">Apply</button>
            <button data-ui-button="secondary" type="button" [disabled]="isLoading()" (click)="clearFilters()">Clear</button>
          </div>

          <div class="shop-filter-note">
            <strong>Buyer tip</strong>
            <span>Published products from visible sellers are shown here. Broaden filters if a category has limited inventory.</span>
          </div>
        </form>

        <div class="shop-results">
          @if (isLoading()) {
            <div class="route-card">Loading products...</div>
          } @else {
            @if (errorMessage()) {
              <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
            }

            @if (activeFilterChips().length > 0) {
              <div class="active-filter-row" aria-label="Active filters">
                @for (filter of activeFilterChips(); track filter.key) {
                  <button type="button" class="active-filter-chip" (click)="removeFilter(filter.key)">
                    {{ filter.label }}
                    <span aria-hidden="true">x</span>
                  </button>
                }
                <button data-ui-button="ghost" type="button" (click)="clearFilters()">Clear all</button>
              </div>
            }

            @if (products().length === 0 && !errorMessage()) {
              <app-empty-state
                eyebrow="No matches"
                heading="No products found"
                message="Try a broader search, choose fewer filters, or browse all products."
              >
                <button data-ui-button="primary" type="button" (click)="clearFilters()">Clear filters</button>
              </app-empty-state>
            } @else {
              <div class="shop-result-bar">
                <span>{{ totalCount() }} result{{ totalCount() === 1 ? '' : 's' }}</span>
                <span>Page {{ page() }} - {{ sortLabel() }}</span>
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
        </div>
      </div>
    </section>
  `
})
export class ShopPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly publicCatalogService = inject(PublicCatalogService);
  private readonly route = inject(ActivatedRoute);

  protected readonly categories = signal<PublicCategoryResponse[]>([]);
  protected readonly products = signal<ProductSearchItemResponse[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(24);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly filtersExpanded = signal(false);

  protected readonly filtersForm = this.formBuilder.group({
    query: [''],
    categorySlug: [''],
    availability: [''],
    minPrice: [''],
    maxPrice: [''],
    size: [''],
    colour: [''],
    material: [''],
    sort: ['newest']
  });

  async ngOnInit(): Promise<void> {
    this.hydrateFiltersFromQueryParams();

    await Promise.all([
      this.loadCategories(),
      this.search(1)
    ]);
  }

  protected async search(page: number): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const filters = this.filtersForm.getRawValue();
      const response = await this.publicCatalogService.searchProducts({
        query: filters.query,
        categorySlug: filters.categorySlug,
        inStock: this.toAvailability(filters.availability),
        minPrice: this.toNumber(filters.minPrice),
        maxPrice: this.toNumber(filters.maxPrice),
        size: filters.size,
        colour: filters.colour,
        material: filters.material,
        sort: filters.sort,
        page,
        pageSize: this.pageSize()
      });

      this.products.set(response.items);
      this.totalCount.set(response.totalCount);
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);
      this.filtersExpanded.set(false);
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
      categorySlug: '',
      availability: '',
      minPrice: '',
      maxPrice: '',
      size: '',
      colour: '',
      material: '',
      sort: 'newest'
    });
    await this.search(1);
  }

  protected async applyCategory(categorySlug: string | null): Promise<void> {
    this.filtersForm.patchValue({ categorySlug: categorySlug ?? '' });
    await this.search(1);
  }

  protected async applyAvailability(availability: 'in_stock' | 'out_of_stock' | ''): Promise<void> {
    this.filtersForm.patchValue({ availability });
    await this.search(1);
  }

  protected quickCategories(): PublicCategoryResponse[] {
    return [...this.categories()]
      .sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name))
      .slice(0, 6);
  }

  protected categoryLabel(category: PublicCategoryResponse): string {
    const path = this.categoryPath(category);
    return path || category.name;
  }

  protected activeFilterChips(): { key: ShopFilterKey; label: string }[] {
    const filters = this.filtersForm.getRawValue();
    const active: { key: ShopFilterKey; label: string }[] = [];
    if (filters.query) {
      active.push({ key: 'query', label: `Search: ${filters.query}` });
    }

    if (filters.categorySlug) {
      const category = this.categories().find(item => item.slug === filters.categorySlug);
      active.push({ key: 'categorySlug', label: `Category: ${category ? this.categoryLabel(category) : filters.categorySlug}` });
    }

    if (filters.availability) {
      active.push({ key: 'availability', label: filters.availability === 'in_stock' ? 'In stock' : 'Out of stock' });
    }

    if (filters.minPrice) {
      active.push({ key: 'minPrice', label: `Min: R${filters.minPrice}` });
    }

    if (filters.maxPrice) {
      active.push({ key: 'maxPrice', label: `Max: R${filters.maxPrice}` });
    }

    if (filters.size) {
      active.push({ key: 'size', label: `Size: ${filters.size}` });
    }

    if (filters.colour) {
      active.push({ key: 'colour', label: `Colour: ${filters.colour}` });
    }

    if (filters.material) {
      active.push({ key: 'material', label: `Material: ${filters.material}` });
    }

    return active;
  }

  protected async removeFilter(key: ShopFilterKey): Promise<void> {
    const resetValue = key === 'sort' ? 'newest' : '';
    this.filtersForm.controls[key].setValue(resetValue);
    await this.search(1);
  }

  protected sortLabel(): string {
    const sort = this.filtersForm.controls.sort.value;
    if (sort === 'price_asc') {
      return 'Price low to high';
    }

    if (sort === 'price_desc') {
      return 'Price high to low';
    }

    if (sort === 'relevance') {
      return 'Relevance';
    }

    return 'Newest';
  }

  protected searchHeading(): string {
    const filters = this.filtersForm.getRawValue();
    if (filters.query.trim()) {
      return filters.query.trim();
    }

    if (filters.categorySlug) {
      const category = this.categories().find(item => item.slug === filters.categorySlug);
      return category ? this.categoryLabel(category) : filters.categorySlug;
    }

    return 'Find your next fashion, beauty, or accessory piece';
  }

  protected resultSummary(): string {
    if (this.isLoading()) {
      return 'Loading published marketplace products.';
    }

    const count = this.totalCount();
    return `${count} published product${count === 1 ? '' : 's'} from Mabuntle sellers. Use filters to narrow by category, stock, price, size, colour, and material.`;
  }

  private async loadCategories(): Promise<void> {
    try {
      this.categories.set(await this.publicCatalogService.getCategories());
    } catch {
      this.categories.set([]);
    }
  }

  private categoryPath(category: PublicCategoryResponse): string {
    const byId = new Map(this.categories().map(item => [item.categoryId, item]));
    const names: string[] = [];
    let current: PublicCategoryResponse | undefined = category;
    while (current) {
      names.unshift(current.name);
      current = current.parentCategoryId ? byId.get(current.parentCategoryId) : undefined;
    }

    return names.join(' > ');
  }

  private toNumber(value: string): number | null {
    if (!value) {
      return null;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
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

  private hydrateFiltersFromQueryParams(): void {
    const queryParamMap = this.route.snapshot.queryParamMap;
    const supportedSorts = new Set(['newest', 'price_asc', 'price_desc', 'relevance']);
    const sort = queryParamMap.get('sort')?.trim() ?? '';

    this.filtersForm.patchValue({
      query: queryParamMap.get('query')?.trim() ?? '',
      categorySlug: queryParamMap.get('categorySlug')?.trim() ?? '',
      colour: queryParamMap.get('colour')?.trim() ?? '',
      material: queryParamMap.get('material')?.trim() ?? '',
      sort: supportedSorts.has(sort) ? sort : 'newest'
    });
  }
}
