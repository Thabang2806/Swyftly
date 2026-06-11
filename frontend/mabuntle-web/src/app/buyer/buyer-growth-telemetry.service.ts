import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, catchError, take } from 'rxjs';
import { environment } from '../../environments/environment';
import { BuyerGrowthEventRequest, BuyerGrowthEventResponse } from './buyer-growth-telemetry.models';

@Injectable({ providedIn: 'root' })
export class BuyerGrowthTelemetryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/buyer/growth-events`;

  recordEvent(request: BuyerGrowthEventRequest): void {
    this.http.post<BuyerGrowthEventResponse>(this.baseUrl, request)
      .pipe(
        take(1),
        catchError(() => EMPTY)
      )
      .subscribe();
  }
}
