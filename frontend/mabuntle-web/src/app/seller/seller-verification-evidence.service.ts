import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SellerVerificationEvidenceResponse,
  SellerVerificationEvidenceType
} from './seller-verification-evidence.models';

@Injectable({ providedIn: 'root' })
export class SellerVerificationEvidenceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/seller/verification-evidence`;

  list(): Promise<SellerVerificationEvidenceResponse[]> {
    return firstValueFrom(this.http.get<SellerVerificationEvidenceResponse[]>(this.baseUrl));
  }

  upload(
    file: File,
    evidenceType: SellerVerificationEvidenceType,
    note: string | null
  ): Promise<SellerVerificationEvidenceResponse> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('evidenceType', evidenceType);
    if (note?.trim()) {
      formData.append('note', note.trim());
    }

    return firstValueFrom(this.http.post<SellerVerificationEvidenceResponse>(`${this.baseUrl}/upload`, formData));
  }

  remove(evidenceId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/${evidenceId}`));
  }

  download(evidenceId: string): Promise<Blob> {
    return firstValueFrom(this.http.get(`${this.baseUrl}/${evidenceId}/download`, { responseType: 'blob' }));
  }
}
