import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { ShopPageComponent } from './shop-page.component';

describe('ShopPageComponent', () => {
  let fixture: ComponentFixture<ShopPageComponent>;
  let publicCatalogService: jasmine.SpyObj<PublicCatalogService>;
  let queryParams: Record<string, string>;

  beforeEach(async () => {
    queryParams = {};
    publicCatalogService = jasmine.createSpyObj<PublicCatalogService>('PublicCatalogService', ['getCategories', 'searchProducts']);
    publicCatalogService.getCategories.and.resolveTo([
      { categoryId: 'parent-id', parentCategoryId: null, name: 'Women', slug: 'women', displayOrder: 10 },
      { categoryId: 'category-id', parentCategoryId: 'parent-id', name: 'Dresses', slug: 'women-dresses', displayOrder: 20 }
    ]);
    publicCatalogService.searchProducts.and.resolveTo({
      items: [createProduct()],
      page: 1,
      pageSize: 24,
      totalCount: 1,
      sort: 'newest'
    });

    await TestBed.configureTestingModule({
      imports: [ShopPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              get queryParamMap() {
                return convertToParamMap(queryParams);
              }
            }
          }
        },
        { provide: PublicCatalogService, useValue: publicCatalogService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ShopPageComponent);
  });

  it('loads categories and displays published products', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(publicCatalogService.getCategories).toHaveBeenCalled();
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('1 result');
    expect(compiled.textContent).toContain('Dresses');
    expect(compiled.querySelector('.hf-shop-hero')).not.toBeNull();
    expect(compiled.textContent).toContain('Published catalog');
    expect(compiled.textContent).toContain('Buyer tip');
  });

  it('submits filters to product search', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const queryInput = (fixture.nativeElement as HTMLElement).querySelector('input[formControlName="query"]') as HTMLInputElement;
    queryInput.value = 'dress';
    queryInput.dispatchEvent(new Event('input'));

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      query: 'dress',
      page: 1,
      pageSize: 24
    }));
  });

  it('hydrates supported discovery query params into the initial product search', async () => {
    queryParams = {
      query: 'linen',
      colour: 'Rose',
      material: 'Linen',
      categorySlug: 'women-dresses',
      sort: 'relevance'
    };

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      query: 'linen',
      categorySlug: 'women-dresses',
      colour: 'Rose',
      material: 'Linen',
      sort: 'relevance',
      page: 1,
      pageSize: 24
    }));
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Search: linen');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Colour: Rose');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Material: Linen');
  });

  it('falls back to newest when an unsupported shop sort query param is supplied', async () => {
    queryParams = { query: 'linen', sort: 'unsupported' };

    fixture.detectChanges();
    await fixture.whenStable();

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      query: 'linen',
      sort: 'newest'
    }));
  });

  it('sends category and availability filters to product search', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { patchValue: (value: object) => void };
      search: (page: number) => Promise<void>;
    };
    component.filtersForm.patchValue({
      categorySlug: 'women-dresses',
      availability: 'in_stock'
    });
    await component.search(1);

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      categorySlug: 'women-dresses',
      inStock: true,
      page: 1
    }));
  });

  it('renders active filter chips and clears an individual filter', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { patchValue: (value: object) => void };
      search: (page: number) => Promise<void>;
      removeFilter: (key: 'query') => Promise<void>;
    };
    component.filtersForm.patchValue({ query: 'dress' });
    await component.search(1);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Search: dress');

    await component.removeFilter('query');

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      query: '',
      page: 1
    }));
  });
});

export function createProduct() {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerStoreName: 'Seller Store',
    sellerStoreSlug: 'seller-store',
    categoryId: 'category-id',
    categoryPath: 'Women > Dresses',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'Short description',
    primaryImageUrl: 'https://example.test/summer-dress.jpg',
    primaryImageAltText: 'Summer dress',
    priceMin: 499,
    compareAtPriceMin: 599,
    inStock: true,
    tags: ['summer'],
    publishedAtUtc: '2026-05-18T12:00:00Z'
  };
}

export function createSellerPolicy() {
  return {
    returnWindowDays: 14,
    returnPolicy: 'Returns are reviewed for delivered items in original condition.',
    exchangePolicy: 'Exchanges depend on stock availability.',
    fulfilmentPolicy: 'Orders are usually dispatched within 2-3 business days.',
    supportPolicy: 'Message support with order issues and product questions.',
    careInstructions: 'Follow product care notes on each item.',
    productDisclaimer: 'Colour and fit may vary slightly by screen and size.',
    isComplete: true,
    missingFields: [],
    updatedAtUtc: '2026-05-21T10:00:00Z'
  };
}

export function createSellerPolicySnapshot() {
  return {
    returnWindowDays: 14,
    returnPolicy: 'Returns are reviewed for delivered items in original condition.',
    exchangePolicy: 'Exchanges depend on stock availability.',
    fulfilmentPolicy: 'Orders are usually dispatched within 2-3 business days.',
    supportPolicy: 'Message support with order issues and product questions.',
    careInstructions: 'Follow product care notes on each item.',
    productDisclaimer: 'Colour and fit may vary slightly by screen and size.',
    snapshotAtUtc: '2026-05-21T10:00:00Z'
  };
}
