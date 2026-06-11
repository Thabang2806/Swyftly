import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerVisualSearchRequest,
  BuyerVisualSearchResponse
} from './buyer-visual-search.models';

@Injectable({ providedIn: 'root' })
export class BuyerVisualSearchService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer/ai/visual-search`;

  search(request: BuyerVisualSearchRequest): Promise<BuyerVisualSearchResponse> {
    return firstValueFrom(this.http.post<BuyerVisualSearchResponse>(this.baseUrl, request));
  }
}
