import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  BuyerAiShoppingAssistantRequest,
  BuyerAiShoppingAssistantResponse
} from './buyer-ai-assistant.models';

@Injectable({ providedIn: 'root' })
export class BuyerAiAssistantService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer/ai/shopping-assistant`;

  search(request: BuyerAiShoppingAssistantRequest): Promise<BuyerAiShoppingAssistantResponse> {
    return firstValueFrom(this.http.post<BuyerAiShoppingAssistantResponse>(this.baseUrl, request));
  }
}
