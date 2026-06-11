import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { PublicCatalogService } from './public-catalog.service';

describe('PublicCatalogService', () => {
  let service: PublicCatalogService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(PublicCatalogService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('searches products with filters', async () => {
    const promise = service.searchProducts({
      query: 'dress',
      minPrice: 100,
      maxPrice: 500,
      sort: 'price_asc'
    });

    const request = httpTestingController.expectOne(match =>
      match.url === `${environment.apiBaseUrl}/api/products/search` &&
      match.params.get('query') === 'dress' &&
      match.params.get('minPrice') === '100' &&
      match.params.get('maxPrice') === '500' &&
      match.params.get('sort') === 'price_asc');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], page: 1, pageSize: 24, totalCount: 0, sort: 'price_asc' });

    const response = await promise;
    expect(response.sort).toBe('price_asc');
  });

  it('loads product detail and storefronts', async () => {
    const productPromise = service.getProduct('summer-dress');
    const productRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/products/summer-dress`);
    expect(productRequest.request.method).toBe('GET');
    productRequest.flush({ product: createProduct(), fullDescription: 'Full', attributes: {}, images: [], variants: [] });

    const sellerPromise = service.getSellerStorefront('seller-store');
    const sellerRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/sellers/seller-store`);
    expect(sellerRequest.request.method).toBe('GET');
    sellerRequest.flush({ sellerId: 'seller-id', storeName: 'Seller Store', slug: 'seller-store', description: null, logoUrl: null, bannerUrl: null, products: [] });

    await expectAsync(productPromise).toBeResolved();
    await expectAsync(sellerPromise).toBeResolved();
  });
});

function createProduct() {
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
    shortDescription: 'Short',
    primaryImageUrl: null,
    primaryImageAltText: null,
    priceMin: 499,
    compareAtPriceMin: 599,
    inStock: true,
    tags: [],
    publishedAtUtc: '2026-05-18T12:00:00Z'
  };
}
