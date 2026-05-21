import { Injectable, computed, inject, signal } from '@angular/core';
import { CartResponse } from '../cart/cart.models';
import { BuyerWishlistItemResponse, MoveWishlistItemToCartRequest } from './buyer-engagement.models';
import { BuyerEngagementService } from './buyer-engagement.service';

@Injectable({ providedIn: 'root' })
export class BuyerWishlistStateService {
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly savedProductIds = signal<ReadonlySet<string>>(new Set<string>());
  private readonly loaded = signal(false);
  private loadPromise: Promise<void> | null = null;

  readonly isLoaded = computed(() => this.loaded());

  isSaved(productId: string): boolean {
    return this.savedProductIds().has(productId);
  }

  async load(): Promise<void> {
    if (this.loaded()) {
      return;
    }

    const pendingLoad = this.loadPromise ?? this.loadProductIds();
    this.loadPromise = pendingLoad;

    try {
      await pendingLoad;
    } finally {
      if (this.loadPromise === pendingLoad) {
        this.loadPromise = null;
      }
    }
  }

  async save(productId: string): Promise<BuyerWishlistItemResponse> {
    const item = await this.engagementService.addWishlistItem(productId);
    this.markSaved(productId);
    return item;
  }

  async remove(productId: string): Promise<void> {
    await this.engagementService.removeWishlistItem(productId);
    this.markRemoved(productId);
  }

  async moveToCart(productId: string, request: MoveWishlistItemToCartRequest): Promise<CartResponse> {
    const cart = await this.engagementService.moveWishlistItemToCart(productId, request);
    this.markRemoved(productId);
    return cart;
  }

  markSaved(productId: string): void {
    this.savedProductIds.update(existing => new Set([...existing, productId]));
  }

  markRemoved(productId: string): void {
    this.savedProductIds.update(existing => {
      const next = new Set(existing);
      next.delete(productId);
      return next;
    });
  }

  private async loadProductIds(): Promise<void> {
    const response = await this.engagementService.listWishlistProductIds();
    this.savedProductIds.set(new Set(response.productIds));
    this.loaded.set(true);
  }
}
