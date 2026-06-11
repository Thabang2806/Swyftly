import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminCategoryResponse,
  UpsertAdminCategoryAttributeRequest,
  UpsertAdminCategoryRequest
} from './admin-category.models';

@Injectable({ providedIn: 'root' })
export class AdminCategoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/categories`;

  listCategories(): Promise<AdminCategoryResponse[]> {
    return firstValueFrom(this.http.get<AdminCategoryResponse[]>(this.baseUrl));
  }

  createCategory(request: UpsertAdminCategoryRequest): Promise<AdminCategoryResponse> {
    return firstValueFrom(this.http.post<AdminCategoryResponse>(this.baseUrl, request));
  }

  updateCategory(categoryId: string, request: UpsertAdminCategoryRequest): Promise<AdminCategoryResponse> {
    return firstValueFrom(this.http.put<AdminCategoryResponse>(`${this.baseUrl}/${categoryId}`, request));
  }

  activateCategory(categoryId: string): Promise<AdminCategoryResponse> {
    return firstValueFrom(this.http.post<AdminCategoryResponse>(`${this.baseUrl}/${categoryId}/activate`, {}));
  }

  deactivateCategory(categoryId: string): Promise<AdminCategoryResponse> {
    return firstValueFrom(this.http.post<AdminCategoryResponse>(`${this.baseUrl}/${categoryId}/deactivate`, {}));
  }

  createAttribute(categoryId: string, request: UpsertAdminCategoryAttributeRequest): Promise<AdminCategoryResponse> {
    return firstValueFrom(this.http.post<AdminCategoryResponse>(`${this.baseUrl}/${categoryId}/attributes`, request));
  }

  updateAttribute(
    categoryId: string,
    attributeId: string,
    request: UpsertAdminCategoryAttributeRequest
  ): Promise<AdminCategoryResponse> {
    return firstValueFrom(this.http.put<AdminCategoryResponse>(`${this.baseUrl}/${categoryId}/attributes/${attributeId}`, request));
  }

  activateAttribute(categoryId: string, attributeId: string): Promise<AdminCategoryResponse> {
    return firstValueFrom(this.http.post<AdminCategoryResponse>(`${this.baseUrl}/${categoryId}/attributes/${attributeId}/activate`, {}));
  }

  deactivateAttribute(categoryId: string, attributeId: string): Promise<AdminCategoryResponse> {
    return firstValueFrom(this.http.post<AdminCategoryResponse>(`${this.baseUrl}/${categoryId}/attributes/${attributeId}/deactivate`, {}));
  }
}
