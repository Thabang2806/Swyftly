import { InjectionToken } from '@angular/core';

export type FrontendExperience = 'client' | 'seller' | 'admin';

export const FRONTEND_EXPERIENCE = new InjectionToken<FrontendExperience>('FRONTEND_EXPERIENCE', {
  providedIn: 'root',
  factory: () => 'client'
});

export const FRONTEND_HOSTS: Record<FrontendExperience, string> = {
  client: 'https://mabuntle.com',
  seller: 'https://seller.mabuntle.com',
  admin: 'https://admin.mabuntle.com'
};

