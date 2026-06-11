import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ProductSearchRequest,
  ProductSearchResponse,
  PublicCategoryResponse,
  PublicProductDetailResponse,
  PublicSellerStorefrontResponse
} from './public-catalog.models';

@Injectable({ providedIn: 'root' })
export class PublicCatalogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  searchProducts(request: ProductSearchRequest = {}): Promise<ProductSearchResponse> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(request)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return firstValueFrom(this.http.get<ProductSearchResponse>(`${this.baseUrl}/api/products/search`, { params }));
  }

  getProduct(slug: string): Promise<PublicProductDetailResponse> {
    return firstValueFrom(this.http.get<PublicProductDetailResponse>(`${this.baseUrl}/api/products/${slug}`));
  }

  getCategories(): Promise<PublicCategoryResponse[]> {
    return firstValueFrom(this.http.get<PublicCategoryResponse[]>(`${this.baseUrl}/api/categories`));
  }

  getSellerStorefront(storeSlug: string): Promise<PublicSellerStorefrontResponse> {
    return firstValueFrom(this.http.get<PublicSellerStorefrontResponse>(`${this.baseUrl}/api/sellers/${storeSlug}`));
  }
}
