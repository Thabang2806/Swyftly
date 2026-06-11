import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { CategoryPageComponent } from './category-page.component';
import { createProduct } from './shop-page.component.spec';

describe('CategoryPageComponent', () => {
  let fixture: ComponentFixture<CategoryPageComponent>;
  let publicCatalogService: jasmine.SpyObj<PublicCatalogService>;
  let paramMapSubject: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(async () => {
    publicCatalogService = jasmine.createSpyObj<PublicCatalogService>('PublicCatalogService', ['getCategories', 'searchProducts']);
    publicCatalogService.getCategories.and.resolveTo([
      { categoryId: 'parent-id', parentCategoryId: null, name: 'Women', slug: 'women', displayOrder: 10 },
      { categoryId: 'category-id', parentCategoryId: 'parent-id', name: 'Dresses', slug: 'women-dresses', displayOrder: 10 },
      { categoryId: 'child-id', parentCategoryId: 'category-id', name: 'Maxi Dresses', slug: 'maxi-dresses', displayOrder: 10 }
    ]);
    publicCatalogService.searchProducts.and.resolveTo({
      items: [createProduct()],
      page: 1,
      pageSize: 24,
      totalCount: 1,
      sort: 'newest'
    });
    paramMapSubject = new BehaviorSubject(convertToParamMap({ slug: 'women-dresses' }));

    await TestBed.configureTestingModule({
      imports: [CategoryPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: paramMapSubject.asObservable(),
            snapshot: {
              paramMap: convertToParamMap({ slug: 'women-dresses' })
            }
          }
        },
        { provide: PublicCatalogService, useValue: publicCatalogService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CategoryPageComponent);
  });

  it('loads category products by slug', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      categorySlug: 'women-dresses'
    }));
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Women > Dresses');
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.querySelector('.hf-category-hero')).not.toBeNull();
    expect(compiled.querySelector('.category-visual-panel app-product-visual-fallback')).not.toBeNull();
  });

  it('renders child category links when available', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Explore within Dresses');
    expect(compiled.textContent).toContain('Maxi Dresses');
  });

  it('reloads when the category route parameter changes', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    paramMapSubject.next(convertToParamMap({ slug: 'maxi-dresses' }));
    await fixture.whenStable();

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      categorySlug: 'maxi-dresses'
    }));
  });

  it('sends category-scoped filters to product search', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { patchValue: (value: object) => void };
      search: (page: number) => Promise<void>;
    };
    component.filtersForm.patchValue({
      query: 'linen',
      availability: 'in_stock',
      sort: 'price_asc'
    });
    await component.search(1);

    expect(publicCatalogService.searchProducts).toHaveBeenCalledWith(jasmine.objectContaining({
      categorySlug: 'women-dresses',
      query: 'linen',
      inStock: true,
      sort: 'price_asc'
    }));
  });
});
