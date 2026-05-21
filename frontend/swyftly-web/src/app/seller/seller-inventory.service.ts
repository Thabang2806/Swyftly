import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdjustSellerInventoryRequest,
  BulkAdjustSellerInventoryRequest,
  SellerInventoryBulkAdjustmentResponse,
  SellerInventoryItemResponse
} from './seller-inventory.models';

@Injectable({ providedIn: 'root' })
export class SellerInventoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/inventory`;

  listInventory(): Promise<SellerInventoryItemResponse[]> {
    return firstValueFrom(this.http.get<SellerInventoryItemResponse[]>(this.baseUrl));
  }

  getExportUrl(): string {
    return `${this.baseUrl}/export.csv`;
  }

  getImportTemplateUrl(): string {
    return `${this.baseUrl}/import-template.csv`;
  }

  exportInventoryCsv(): Promise<Blob> {
    return firstValueFrom(this.http.get(`${this.baseUrl}/export.csv`, { responseType: 'blob' }));
  }

  downloadImportTemplate(): Promise<Blob> {
    return firstValueFrom(this.http.get(`${this.baseUrl}/import-template.csv`, { responseType: 'blob' }));
  }

  previewImport(file: File): Promise<SellerInventoryBulkAdjustmentResponse> {
    const formData = new FormData();
    formData.append('file', file);

    return firstValueFrom(
      this.http.post<SellerInventoryBulkAdjustmentResponse>(`${this.baseUrl}/import/preview`, formData)
    );
  }

  bulkAdjust(request: BulkAdjustSellerInventoryRequest): Promise<SellerInventoryBulkAdjustmentResponse> {
    return firstValueFrom(
      this.http.post<SellerInventoryBulkAdjustmentResponse>(`${this.baseUrl}/bulk-adjust`, request)
    );
  }

  adjustInventory(
    variantId: string,
    request: AdjustSellerInventoryRequest
  ): Promise<SellerInventoryItemResponse> {
    return firstValueFrom(
      this.http.post<SellerInventoryItemResponse>(`${this.baseUrl}/${variantId}/adjust`, request)
    );
  }
}
