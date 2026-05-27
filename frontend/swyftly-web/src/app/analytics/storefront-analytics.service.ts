import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

type StorefrontFunnelEventType = 'StorefrontViewed' | 'ProductViewed' | 'ProductAddedToCart' | 'CheckoutStarted';

interface StorefrontFunnelEventRequest {
  eventType: StorefrontFunnelEventType;
  productId?: string | null;
  cartId?: string | null;
  sellerStoreSlug?: string | null;
  anonymousVisitorId: string;
  sourceRoute?: string | null;
  idempotencyKey?: string | null;
  utmSource?: string | null;
  utmMedium?: string | null;
  utmCampaign?: string | null;
  referrerHost?: string | null;
  sourceCategory?: StorefrontSourceCategory | null;
}

type StorefrontSourceCategory = 'Direct' | 'Search' | 'Social' | 'Email' | 'Ads' | 'Referral' | 'Unknown';

@Injectable({ providedIn: 'root' })
export class StorefrontAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/analytics/storefront-events`;
  private readonly storageKey = 'swyftlyVisitorId';

  trackProductView(productId: string, sourceRoute: string): void {
    this.track({ eventType: 'ProductViewed', productId, sourceRoute });
  }

  trackStorefrontView(sellerStoreSlug: string, sourceRoute: string): void {
    this.track({ eventType: 'StorefrontViewed', sellerStoreSlug, sourceRoute });
  }

  trackProductAddedToCart(productId: string, sourceRoute: string): void {
    this.track({ eventType: 'ProductAddedToCart', productId, sourceRoute });
  }

  trackCheckoutStarted(cartId: string, sourceRoute: string): void {
    this.track({ eventType: 'CheckoutStarted', cartId, sourceRoute });
  }

  private track(event: Omit<StorefrontFunnelEventRequest, 'anonymousVisitorId'>): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const anonymousVisitorId = this.getOrCreateVisitorId();
    const attribution = this.getAttribution();
    void firstValueFrom(this.http.post(this.baseUrl, {
      ...event,
      anonymousVisitorId,
      ...attribution
    })).catch(() => {
      // Analytics are best-effort and must not affect browsing, cart, or checkout flows.
    });
  }

  private getOrCreateVisitorId(): string {
    const existing = this.tryGetStoredVisitorId();
    if (existing && /^[A-Za-z0-9_-]{8,128}$/.test(existing)) {
      return existing;
    }

    const generated = this.generateVisitorId();
    this.trySetStoredVisitorId(generated);
    return generated;
  }

  private tryGetStoredVisitorId(): string | null {
    try {
      return globalThis.localStorage.getItem(this.storageKey);
    } catch {
      return null;
    }
  }

  private trySetStoredVisitorId(visitorId: string): void {
    try {
      globalThis.localStorage.setItem(this.storageKey, visitorId);
    } catch {
      // Storage access can be blocked; the generated id is still used for this event.
    }
  }

  private generateVisitorId(): string {
    const crypto = globalThis.crypto;
    if (typeof crypto?.randomUUID === 'function') {
      return crypto.randomUUID().replaceAll('-', '');
    }

    return `visitor_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 14)}`;
  }

  private getAttribution(): Pick<StorefrontFunnelEventRequest, 'utmSource' | 'utmMedium' | 'utmCampaign' | 'referrerHost' | 'sourceCategory'> {
    const params = new URLSearchParams(globalThis.location?.search ?? '');
    const utmSource = this.trimOrNull(params.get('utm_source'));
    const utmMedium = this.trimOrNull(params.get('utm_medium'));
    const utmCampaign = this.trimOrNull(params.get('utm_campaign'));
    const referrerHost = this.getExternalReferrerHost();

    return {
      utmSource,
      utmMedium,
      utmCampaign,
      referrerHost,
      sourceCategory: this.classifySource(utmSource, utmMedium, referrerHost)
    };
  }

  private getExternalReferrerHost(): string | null {
    const referrer = globalThis.document?.referrer;
    if (!referrer) {
      return null;
    }

    try {
      const referrerUrl = new URL(referrer);
      const currentHost = globalThis.location?.host?.toLowerCase();
      const referrerHost = referrerUrl.host.toLowerCase();
      return currentHost && referrerHost === currentHost ? null : referrerHost;
    } catch {
      return null;
    }
  }

  private classifySource(
    utmSource: string | null,
    utmMedium: string | null,
    referrerHost: string | null
  ): StorefrontSourceCategory {
    const source = (utmSource ?? '').toLowerCase();
    const medium = (utmMedium ?? '').toLowerCase();
    const host = (referrerHost ?? '').toLowerCase();

    if (medium.includes('email') || source.includes('email') || source.includes('newsletter')) {
      return 'Email';
    }

    if (['cpc', 'ppc', 'paid', 'paid_social', 'display', 'ads', 'ad'].some(value => medium.includes(value) || source.includes(value))) {
      return 'Ads';
    }

    if (medium.includes('social') || this.isSocialHost(source) || this.isSocialHost(host)) {
      return 'Social';
    }

    if (medium.includes('organic') || medium.includes('search') || medium.includes('seo') || this.isSearchHost(source) || this.isSearchHost(host)) {
      return 'Search';
    }

    if (utmSource || utmMedium || referrerHost) {
      return 'Referral';
    }

    return 'Direct';
  }

  private isSearchHost(value: string): boolean {
    return ['google.', 'bing.', 'yahoo.', 'duckduckgo.', 'ecosia.'].some(host => value.includes(host));
  }

  private isSocialHost(value: string): boolean {
    return ['facebook.', 'instagram.', 'tiktok.', 'pinterest.', 'twitter.', 'x.com', 'linkedin.'].some(host => value.includes(host));
  }

  private trimOrNull(value: string | null): string | null {
    const trimmed = value?.trim();
    return trimmed ? trimmed : null;
  }
}
