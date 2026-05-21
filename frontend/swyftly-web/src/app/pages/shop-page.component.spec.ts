import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { ShopPageComponent } from './shop-page.component';

describe('ShopPageComponent', () => {
  let fixture: ComponentFixture<ShopPageComponent>;
  let publicCatalogService: jasmine.SpyObj<PublicCatalogService>;

  beforeEach(async () => {
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
