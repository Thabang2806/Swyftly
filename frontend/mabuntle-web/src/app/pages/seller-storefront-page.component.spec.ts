import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { StorefrontAnalyticsService } from '../analytics/storefront-analytics.service';
import { PublicCatalogService } from '../shop/public-catalog.service';
import { SellerStorefrontPageComponent } from './seller-storefront-page.component';
import { createProduct, createSellerPolicy } from './shop-page.component.spec';

describe('SellerStorefrontPageComponent', () => {
  let fixture: ComponentFixture<SellerStorefrontPageComponent>;
  let publicCatalogService: jasmine.SpyObj<PublicCatalogService>;
  let storefrontAnalytics: jasmine.SpyObj<StorefrontAnalyticsService>;

  beforeEach(async () => {
    publicCatalogService = jasmine.createSpyObj<PublicCatalogService>('PublicCatalogService', ['getSellerStorefront']);
    storefrontAnalytics = jasmine.createSpyObj<StorefrontAnalyticsService>('StorefrontAnalyticsService', ['trackStorefrontView']);
    publicCatalogService.getSellerStorefront.and.resolveTo({
      sellerId: 'seller-id',
      storeName: 'Seller Store',
      slug: 'seller-store',
      description: 'Curated dresses.',
      logoUrl: null,
      bannerUrl: 'https://example.test/banner.jpg',
      products: [createProduct()],
      sellerPolicy: createSellerPolicy()
    });

    await TestBed.configureTestingModule({
      imports: [SellerStorefrontPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ storeSlug: 'seller-store' })
            }
          }
        },
        { provide: PublicCatalogService, useValue: publicCatalogService },
        { provide: StorefrontAnalyticsService, useValue: storefrontAnalytics }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerStorefrontPageComponent);
  });

  it('loads storefront products by slug', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(publicCatalogService.getSellerStorefront).toHaveBeenCalledWith('seller-store');
    expect(storefrontAnalytics.trackStorefrontView).toHaveBeenCalledWith('seller-store', jasmine.any(String));
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('Curated dresses.');
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('Store policies');
    expect(compiled.textContent).toContain('Returns are reviewed');
  });

  it('renders storefront trust details and fallback logo initial', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Verified marketplace seller');
    expect(compiled.querySelector('.storefront-trust-grid strong')?.textContent).toContain('1');
    expect(compiled.textContent).toContain('published product');
    expect(compiled.querySelector('.seller-storefront-logo')?.textContent?.trim()).toBe('S');
  });

  it('filters storefront products client-side by search and availability', async () => {
    publicCatalogService.getSellerStorefront.and.resolveTo({
      sellerId: 'seller-id',
      storeName: 'Seller Store',
      slug: 'seller-store',
      description: 'Curated dresses.',
      logoUrl: null,
      bannerUrl: null,
      products: [
        createProduct(),
        {
          ...createProduct(),
          productId: 'bag-id',
          title: 'Leather Tote',
          slug: 'leather-tote',
          categoryPath: 'Bags',
          inStock: false,
          priceMin: 899,
          tags: ['bag']
        }
      ],
      sellerPolicy: createSellerPolicy()
    });
    fixture = TestBed.createComponent(SellerStorefrontPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = (fixture.nativeElement as HTMLElement).querySelector('input[formControlName="query"]') as HTMLInputElement;
    input.value = 'tote';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Leather Tote');
    expect(compiled.textContent).not.toContain('Summer Dress');
  });
});
