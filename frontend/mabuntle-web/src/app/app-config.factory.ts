import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, Routes, withInMemoryScrolling } from '@angular/router';

import { authTokenInterceptor } from './auth/auth.interceptor';
import { FrontendExperience, FRONTEND_EXPERIENCE } from './frontend-experience';

export function createAppConfig(routes: Routes, experience: FrontendExperience): ApplicationConfig {
  return {
    providers: [
      provideZoneChangeDetection({ eventCoalescing: true }),
      provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })),
      provideHttpClient(withFetch(), withInterceptors([authTokenInterceptor])),
      provideClientHydration(withEventReplay()),
      provideAnimationsAsync(),
      { provide: FRONTEND_EXPERIENCE, useValue: experience }
    ]
  };
}
