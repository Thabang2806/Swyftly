import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerAiDiscoveryHistoryResponse } from '../buyer/buyer-ai-discovery.models';
import { BuyerAiDiscoveryService } from '../buyer/buyer-ai-discovery.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-ai-history-page',
  imports: [
    BuyerWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page buyer-ai-history-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="AI discovery history"
        description="Review opt-in assistant and visual-search summaries saved for your convenience."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/assistant">Assistant</a>
          <a data-ui-button="secondary" routerLink="/visual-search">Visual search</a>
          <a data-ui-button="secondary" routerLink="/account/settings">Privacy controls</a>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading AI discovery history...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (!historyEnabled()) {
          <app-empty-state
            eyebrow="Privacy"
            heading="AI history is off"
            message="Enable it in account settings if you want Mabuntle to save safe AI discovery summaries across devices."
          >
            <a data-ui-button="primary" routerLink="/account/settings">Manage AI history</a>
          </app-empty-state>
        } @else if (historyItems().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="AI history"
            heading="No saved AI discoveries yet"
            message="Once enabled, successful assistant and visual-search results can save category, colour, material, confidence, result count, and product ids. Prompts and images are not stored."
          >
            <a data-ui-button="primary" routerLink="/assistant">Try assistant</a>
            <a data-ui-button="secondary" routerLink="/visual-search">Try visual search</a>
          </app-empty-state>
        } @else {
          <section class="route-card">
            <div class="ai-history-toolbar">
              <div>
                <h2>Saved discoveries</h2>
                <p>
                  Showing {{ historyItems().length }} of {{ totalCount() }} saved summar{{ totalCount() === 1 ? 'y' : 'ies' }}.
                  Server history is separate from browser-local recent prompts.
                </p>
              </div>
              <button data-ui-button="secondary" type="button" [disabled]="isSaving() || historyItems().length === 0" (click)="clearAllHistory()">
                {{ isSaving() ? 'Clearing...' : 'Clear all history' }}
              </button>
            </div>
          </section>

          <div class="buyer-ai-history-list">
            @for (item of historyItems(); track item.historyId) {
              <article class="route-card buyer-ai-history-card">
                <div class="buyer-ai-history-card__header">
                  <div>
                    <app-status-badge [label]="toolLabel(item.sourceTool)" tone="accent" />
                    @if (item.confidenceBand) {
                      <app-status-badge [label]="item.confidenceBand + ' confidence'" [tone]="confidenceTone(item.confidenceBand)" />
                    }
                  </div>
                  <small>{{ item.createdAtUtc | date:'medium' }}</small>
                </div>

                <div>
                  <h2>{{ historySummary(item) }}</h2>
                  <p>
                    {{ item.resultCount }} result{{ item.resultCount === 1 ? '' : 's' }} returned.
                    @if (item.sourceRoute) {
                      Source route: {{ item.sourceRoute }}
                    }
                  </p>
                </div>

                @if (item.products.length > 0) {
                  <div class="ai-history-products" aria-label="Saved product result links">
                    @for (product of item.products; track product.productId) {
                      <a [routerLink]="['/product', product.slug]">{{ product.title }}</a>
                    }
                  </div>
                } @else if (item.productIds.length > 0) {
                  <p class="muted-copy">{{ item.productIds.length }} product id{{ item.productIds.length === 1 ? '' : 's' }} saved; product cards may no longer be visible.</p>
                }

                <div class="buyer-action-row">
                  <a data-ui-button="secondary" routerLink="/shop" [queryParams]="shopQueryParams(item)">Repeat in shop</a>
                  <button data-ui-button="secondary" type="button" [disabled]="deletingHistoryId() === item.historyId" (click)="deleteHistoryItem(item)">
                    {{ deletingHistoryId() === item.historyId ? 'Deleting...' : 'Delete' }}
                  </button>
                </div>
              </article>
            }
          </div>

          @if (canLoadMore()) {
            <div class="buyer-action-row">
              <button data-ui-button="secondary" type="button" [disabled]="isLoadingMore()" (click)="loadMore()">
                {{ isLoadingMore() ? 'Loading...' : 'Load more' }}
              </button>
            </div>
          }
        }
      }
    </section>
  `
})
export class BuyerAiHistoryPageComponent implements OnInit {
  private readonly aiDiscoveryService = inject(BuyerAiDiscoveryService);

  protected readonly historyEnabled = signal(false);
  protected readonly historyItems = signal<BuyerAiDiscoveryHistoryResponse[]>([]);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly isLoadingMore = signal(false);
  protected readonly isSaving = signal(false);
  protected readonly deletingHistoryId = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly canLoadMore = computed(() => this.historyItems().length < this.totalCount());

  async ngOnInit(): Promise<void> {
    await this.loadInitialState();
  }

  protected toolLabel(sourceTool: string): string {
    return sourceTool === 'VisualSearch' ? 'Visual search' : 'Assistant';
  }

  protected confidenceTone(confidenceBand: string): StatusBadgeTone {
    if (confidenceBand === 'High') {
      return 'success';
    }

    if (confidenceBand === 'Medium') {
      return 'warning';
    }

    return 'danger';
  }

  protected historySummary(item: BuyerAiDiscoveryHistoryResponse): string {
    const parts = [item.category, item.colour, item.material].filter(Boolean);
    return parts.length > 0 ? parts.join(' / ') : 'General product discovery';
  }

  protected shopQueryParams(item: BuyerAiDiscoveryHistoryResponse): Record<string, string | null> {
    return {
      categorySlug: item.category ? this.toSlug(item.category) : null,
      colour: item.colour,
      material: item.material,
      sort: 'relevance'
    };
  }

  protected async deleteHistoryItem(item: BuyerAiDiscoveryHistoryResponse): Promise<void> {
    if (this.deletingHistoryId()) {
      return;
    }

    this.deletingHistoryId.set(item.historyId);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await this.aiDiscoveryService.deleteHistoryItem(item.historyId);
      this.historyItems.set(this.historyItems().filter(existing => existing.historyId !== item.historyId));
      this.totalCount.set(Math.max(0, this.totalCount() - 1));
      this.successMessage.set('AI history row deleted.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.deletingHistoryId.set(null);
    }
  }

  protected async clearAllHistory(): Promise<void> {
    if (this.isSaving()) {
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await this.aiDiscoveryService.clearHistory();
      this.historyItems.set([]);
      this.totalCount.set(0);
      this.page.set(1);
      this.successMessage.set('AI discovery history cleared.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected async loadMore(): Promise<void> {
    if (this.isLoadingMore() || !this.canLoadMore()) {
      return;
    }

    this.isLoadingMore.set(true);
    this.errorMessage.set(null);

    try {
      const nextPage = this.page() + 1;
      const response = await this.aiDiscoveryService.getHistory({ page: nextPage, pageSize: this.pageSize() });
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);
      this.totalCount.set(response.totalCount);
      this.historyItems.set([...this.historyItems(), ...response.items]);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoadingMore.set(false);
    }
  }

  private async loadInitialState(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const preference = await this.aiDiscoveryService.getPreferences();
      this.historyEnabled.set(preference.historyEnabled);

      if (preference.historyEnabled) {
        const response = await this.aiDiscoveryService.getHistory({ page: 1, pageSize: this.pageSize() });
        this.page.set(response.page);
        this.pageSize.set(response.pageSize);
        this.totalCount.set(response.totalCount);
        this.historyItems.set(response.items);
      } else {
        this.historyItems.set([]);
        this.totalCount.set(0);
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private toSlug(value: string): string {
    return value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '');
  }
}
