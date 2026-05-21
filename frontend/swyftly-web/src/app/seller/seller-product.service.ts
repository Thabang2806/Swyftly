import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ApplySellerAiSuggestionRequest,
  AttachSellerProductImageRequest,
  GenerateSellerAiSuggestionRequest,
  SellerAiSuggestionResponse,
  SellerCatalogCategoryResponse,
  SellerProductDetailResponse,
  SellerProductRevisionResponse,
  SellerProductSummaryResponse,
  UpdateSellerProductImageRequest,
  UpsertSellerProductRequest,
  UpsertSellerProductRevisionRequest,
  UpsertSellerProductVariantRequest
} from './seller-product.models';

@Injectable({ providedIn: 'root' })
export class SellerProductService {
  private readonly http = inject(HttpClient);
  private readonly productBaseUrl = `${environment.apiBaseUrl}/api/seller/products`;
  private readonly catalogBaseUrl = `${environment.apiBaseUrl}/api/seller/catalog`;

  getCategories(): Promise<SellerCatalogCategoryResponse[]> {
    return firstValueFrom(this.http.get<SellerCatalogCategoryResponse[]>(`${this.catalogBaseUrl}/categories`));
  }

  listProducts(): Promise<SellerProductSummaryResponse[]> {
    return firstValueFrom(this.http.get<SellerProductSummaryResponse[]>(this.productBaseUrl));
  }

  getProduct(productId: string): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.get<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}`));
  }

  createProduct(request: UpsertSellerProductRequest): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.post<SellerProductDetailResponse>(this.productBaseUrl, request));
  }

  updateProduct(productId: string, request: UpsertSellerProductRequest): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.put<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}`, request));
  }

  addVariant(productId: string, request: UpsertSellerProductVariantRequest): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.post<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}/variants`, request));
  }

  updateVariant(
    productId: string,
    variantId: string,
    request: UpsertSellerProductVariantRequest): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.put<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}/variants/${variantId}`, request));
  }

  deleteVariant(productId: string, variantId: string): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.delete<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}/variants/${variantId}`));
  }

  addImage(productId: string, request: AttachSellerProductImageRequest): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.post<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}/images`, request));
  }

  uploadImage(
    productId: string,
    file: File,
    altText: string | null,
    sortOrder: number,
    isPrimary: boolean): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.post<SellerProductDetailResponse>(
      `${this.productBaseUrl}/${productId}/images/upload`,
      createImageUploadFormData(file, altText, sortOrder, isPrimary)));
  }

  updateImage(
    productId: string,
    imageId: string,
    request: UpdateSellerProductImageRequest): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.put<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}/images/${imageId}`, request));
  }

  deleteImage(productId: string, imageId: string): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.delete<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}/images/${imageId}`));
  }

  submitForReview(productId: string): Promise<SellerProductDetailResponse> {
    return firstValueFrom(this.http.post<SellerProductDetailResponse>(`${this.productBaseUrl}/${productId}/submit-review`, {}));
  }

  getRevision(productId: string): Promise<SellerProductRevisionResponse> {
    return firstValueFrom(this.http.get<SellerProductRevisionResponse>(`${this.productBaseUrl}/${productId}/revision`));
  }

  updateRevision(productId: string, request: UpsertSellerProductRevisionRequest): Promise<SellerProductRevisionResponse> {
    return firstValueFrom(this.http.put<SellerProductRevisionResponse>(`${this.productBaseUrl}/${productId}/revision`, request));
  }

  uploadRevisionImage(
    productId: string,
    file: File,
    altText: string | null,
    sortOrder: number,
    isPrimary: boolean): Promise<SellerProductRevisionResponse> {
    return firstValueFrom(this.http.post<SellerProductRevisionResponse>(
      `${this.productBaseUrl}/${productId}/revision/images/upload`,
      createImageUploadFormData(file, altText, sortOrder, isPrimary)));
  }

  updateRevisionImage(
    productId: string,
    revisionImageId: string,
    request: UpdateSellerProductImageRequest): Promise<SellerProductRevisionResponse> {
    return firstValueFrom(this.http.put<SellerProductRevisionResponse>(
      `${this.productBaseUrl}/${productId}/revision/images/${revisionImageId}`,
      request));
  }

  deleteRevisionImage(productId: string, revisionImageId: string): Promise<SellerProductRevisionResponse> {
    return firstValueFrom(this.http.delete<SellerProductRevisionResponse>(
      `${this.productBaseUrl}/${productId}/revision/images/${revisionImageId}`));
  }

  submitRevisionForReview(productId: string): Promise<SellerProductRevisionResponse> {
    return firstValueFrom(this.http.post<SellerProductRevisionResponse>(`${this.productBaseUrl}/${productId}/revision/submit-review`, {}));
  }

  cancelRevision(productId: string): Promise<SellerProductRevisionResponse> {
    return firstValueFrom(this.http.post<SellerProductRevisionResponse>(`${this.productBaseUrl}/${productId}/revision/cancel`, {}));
  }

  generateAiSuggestion(
    productId: string,
    request: GenerateSellerAiSuggestionRequest): Promise<SellerAiSuggestionResponse> {
    return firstValueFrom(this.http.post<SellerAiSuggestionResponse>(`${this.productBaseUrl}/${productId}/ai-suggestions`, request));
  }

  applyAiSuggestion(
    productId: string,
    suggestionId: string,
    request: ApplySellerAiSuggestionRequest): Promise<SellerProductDetailResponse> {
    return firstValueFrom(
      this.http.post<SellerProductDetailResponse>(
        `${this.productBaseUrl}/${productId}/ai-suggestions/${suggestionId}/apply`,
        request));
  }
}

function createImageUploadFormData(
  file: File,
  altText: string | null,
  sortOrder: number,
  isPrimary: boolean): FormData {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('altText', altText ?? '');
  formData.append('sortOrder', sortOrder.toString());
  formData.append('isPrimary', isPrimary.toString());
  return formData;
}
