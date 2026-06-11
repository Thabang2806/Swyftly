import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BuyerAiProductCardResponse, BuyerAiShoppingAssistantResponse } from '../buyer/buyer-ai-assistant.models';
import { BuyerAiAssistantService } from '../buyer/buyer-ai-assistant.service';
import { BuyerGrowthConfidenceBand, BuyerGrowthFeedbackReason } from '../buyer/buyer-growth-telemetry.models';
import { BuyerGrowthTelemetryService } from '../buyer/buyer-growth-telemetry.service';
import { LuxuryBuyerStylesComponent } from '../buyer/luxury-buyer-styles.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

interface RecentAssistantPrompt {
  text: string;
  savedAtUtc: string;
}

interface AssistantConfidenceSummary {
  label: string;
  band: BuyerGrowthConfidenceBand;
  tone: StatusBadgeTone;
  detail: string;
  signals: string[];
}

@Component({
  selector: 'app-buyer-ai-assistant-page',
  imports: [
    CurrencyPipe,
    LuxuryBuyerStylesComponent,
    EmptyStateComponent,
    ProductVisualFallbackComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <app-luxury-buyer-styles />
    <section class="page ai-discovery-page ai-style-page">
      <section class="ai-style-layout">
        <aside class="ai-chat-panel" aria-label="AI Style Finder">
          <div class="ai-chat-title">
            <div>
              <span class="eyebrow">AI Style Finder</span>
              <h1>Tell Mabuntle what you need</h1>
            </div>
            <app-status-badge label="AI" tone="accent" />
          </div>

          <div class="ai-chat-transcript" aria-live="polite">
            <div class="ai-chat-bubble ai-chat-bubble--bot">
              I can find outfits, gifts, jewellery, beauty products, or occasion-ready pieces from published Mabuntle listings.
            </div>
            @if (submittedPrompt()) {
              <div class="ai-chat-bubble ai-chat-bubble--user">{{ submittedPrompt() }}</div>
            }
            @if (response()) {
              <div class="ai-chat-bubble ai-chat-bubble--bot">{{ response()!.summary }}</div>
            }
          </div>

          @if (response()) {
            <div class="ai-intent-card">
              <div class="ai-results-header">
                <div>
                  <strong>Extracted intent</strong>
                  <p>{{ response()!.intent.isVague ? 'The assistant needs a little more detail.' : 'The backend parsed this shopping intent.' }}</p>
                </div>
                <app-status-badge
                  [label]="response()!.intent.isVague ? 'Needs more detail' : 'Ready'"
                  [tone]="response()!.intent.isVague ? 'warning' : 'success'"
                />
              </div>
              <div class="ai-intent-grid" aria-label="Extracted shopping intent">
                @for (item of intentItems(); track item.label) {
                  <span>
                    <small>{{ item.label }}</small>
                    <strong>{{ item.value }}</strong>
                  </span>
                } @empty {
                  <span>
                    <small>Search text</small>
                    <strong>{{ response()!.intent.searchText }}</strong>
                  </span>
                }
              </div>
            </div>
          } @else {
            <div class="ai-discovery-guide" aria-label="Assistant guidance">
              <strong>Better prompts include:</strong>
              <span>Category or item type</span>
              <span>Size, colour, or material</span>
              <span>Budget and occasion</span>
              <span>Beauty skin type or concern where relevant</span>
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="search()" class="ai-discovery-form ai-refine-form" novalidate>
            <label class="ui-field">
              <span>Ask to refine results</span>
              <textarea rows="3" formControlName="message" placeholder="Find a wedding outfit under R1,500 in neutral colours"></textarea>
              @if (form.controls.message.hasError('required')) {
                <span class="ui-field-error">Enter a shopping request.</span>
              }
            </label>

            @if (recentPrompts().length > 0) {
              <div class="ai-recent-panel" aria-label="Recent assistant prompts">
                <div>
                  <strong>Recent prompts</strong>
                  <button data-ui-button="ghost" type="button" (click)="clearRecentPrompts()">Clear</button>
                </div>
                <div class="ai-chip-row">
                  @for (recent of recentPrompts(); track recent.text) {
                    <button data-ui-button="secondary" type="button" (click)="useRecentPrompt(recent.text)">
                      {{ recent.text }}
                    </button>
                  }
                </div>
                <small>Saved in this browser only. Results are not stored.</small>
              </div>
            }

            <div class="ai-example-row" aria-label="Example shopping requests">
              @for (example of examplePrompts; track example) {
                <button data-ui-button="secondary" type="button" (click)="useExamplePrompt(example)">
                  {{ example }}
                </button>
              }
            </div>

            <button data-ui-button="primary" type="submit" [disabled]="form.invalid || isLoading()">
              {{ isLoading() ? 'Searching...' : (response() ? 'Update recommendations' : 'Find products') }}
            </button>
          </form>
        </aside>

        <section class="ai-recommendations-panel" aria-label="Assistant recommendations">
          <div class="ai-results-header">
            <div>
              <span class="eyebrow">Real product recommendations</span>
              <h2>{{ response() ? 'Product matches from the catalog' : 'Recommendations appear here' }}</h2>
              <p>Only products returned by the backend are shown. The assistant does not reserve stock or create orders.</p>
            </div>
            <a data-ui-button="secondary" routerLink="/shop">Browse shop</a>
          </div>

          @if (errorMessage()) {
            <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
          }

          @if (isLoading()) {
            <app-ui-alert>Searching published products and extracting intent...</app-ui-alert>
          }

          @if (response()) {
            @if (response()!.intent.clarificationPrompt) {
              <app-ui-alert tone="warning">{{ response()!.intent.clarificationPrompt }}</app-ui-alert>
            }
            @if (response()!.safetyNote) {
              <app-ui-alert>{{ response()!.safetyNote }}</app-ui-alert>
            }

            @if (matchConfidence(); as confidence) {
              <div class="ai-confidence-card">
                <div class="ai-results-header">
                  <div>
                    <strong>Match confidence</strong>
                    <p>{{ confidence.detail }}</p>
                  </div>
                  <app-status-badge [label]="confidence.label" [tone]="confidence.tone" />
                </div>
                <div class="ai-chip-row">
                  @for (signal of confidence.signals; track signal) {
                    <span>{{ signal }}</span>
                  }
                </div>
              </div>
            }

            @if (products().length > 0) {
              <div class="ai-result-grid hf-ai-result-grid">
                @for (product of products(); track product.productId) {
                  <a class="ai-product-result-card hf-ai-product-card" [routerLink]="['/product', product.slug]" (click)="trackProductOpen(product)">
                    <div class="ai-product-result-media hf-ai-product-media">
                      @if (product.imageUrl) {
                        <img [src]="product.imageUrl" [alt]="product.title">
                      } @else {
                        <app-product-visual-fallback
                          [title]="product.title"
                          label="AI match"
                          [tone]="productTone(product)"
                        />
                      }
                    </div>
                    <div class="ai-product-result-body">
                      <span>{{ product.sellerDisplayName ?? 'Mabuntle seller' }}</span>
                      <h3>{{ product.title }}</h3>
                      <strong>{{ product.price | currency:product.currency:'symbol':'1.2-2' }}</strong>
                      <ul>
                        @for (reason of product.matchReasons; track reason) {
                          <li>{{ reason }}</li>
                        }
                      </ul>
                      @if (product.personalizationApplied && product.personalizationReasons.length > 0) {
                        <div class="ai-personalization-reasons" aria-label="Why this was recommended">
                          <app-status-badge label="Personalized" tone="accent" />
                          @for (reason of product.personalizationReasons; track reason) {
                            <span>{{ reason }}</span>
                          }
                        </div>
                      }
                      <span class="ai-card-action">View product</span>
                    </div>
                  </a>
                }
              </div>

              <div class="ai-why-card">
                <h3>Why this works</h3>
                <p>{{ response()!.summary }}</p>
                <div class="active-filter-row">
                  <app-status-badge [label]="products().length + ' real listings'" tone="success" />
                  <app-status-badge label="Backend matched" />
                  <app-status-badge label="No stock reservation" tone="accent" />
                </div>
                <a data-ui-button="secondary" routerLink="/shop" [queryParams]="assistantShopQueryParams()" (click)="trackShopHandoff()">Refine in shop</a>
              </div>
            } @else {
              <app-empty-state
                eyebrow="No matches"
                heading="No product cards to show"
                message="Try a broader category, colour, size, style, or budget."
              >
                <button data-ui-button="secondary" type="button" (click)="useExamplePrompt(examplePrompts[0])">Use an example</button>
                <a data-ui-button="secondary" routerLink="/shop" [queryParams]="assistantShopQueryParams()" (click)="trackShopHandoff()">Search shop</a>
              </app-empty-state>
            }

            <div class="ai-feedback-card" aria-label="Assistant usefulness feedback">
              <div>
                <strong>Was this useful?</strong>
                <span>Feedback is saved as a reason code only, not your prompt.</span>
              </div>
              <div class="ai-chip-row">
                @for (reason of feedbackReasons; track reason.value) {
                  <button data-ui-button="secondary" type="button" (click)="submitFeedback(reason.value)">
                    {{ reason.label }}
                  </button>
                }
              </div>
              @if (feedbackStatus()) {
                <small>{{ feedbackStatus() }}</small>
              }
            </div>
          } @else {
            <app-empty-state
              eyebrow="Ready when you are"
              heading="Ask for a style, gift, product, or beauty need"
              message="Example prompts can fill the box, but the assistant only searches when you submit."
            >
              <button data-ui-button="secondary" type="button" (click)="useExamplePrompt(examplePrompts[0])">Start with an example</button>
            </app-empty-state>
          }
        </section>
      </section>
    </section>
  `
})
export class BuyerAiAssistantPageComponent {
  private static readonly recentPromptsStorageKey = 'mabuntle.buyer.assistant.recentPrompts';
  private static readonly maxRecentPromptCount = 6;

  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly assistantService = inject(BuyerAiAssistantService);
  private readonly telemetryService = inject(BuyerGrowthTelemetryService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly response = signal<BuyerAiShoppingAssistantResponse | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly submittedPrompt = signal<string | null>(null);
  protected readonly recentPrompts = signal<RecentAssistantPrompt[]>([]);
  protected readonly feedbackStatus = signal<string | null>(null);
  protected readonly feedbackReasons: Array<{ value: BuyerGrowthFeedbackReason; label: string }> = [
    { value: 'GoodMatches', label: 'Good matches' },
    { value: 'TooBroad', label: 'Too broad' },
    { value: 'WrongStyle', label: 'Wrong style' },
    { value: 'WrongCategory', label: 'Wrong category' },
    { value: 'Unavailable', label: 'Unavailable' },
    { value: 'LowConfidence', label: 'Low confidence' }
  ];
  protected readonly examplePrompts = [
    'Wedding outfit under R1,500, neutral colours',
    'Minimal gold earrings for everyday wear',
    'Gentle cleanser for oily skin'
  ];

  protected readonly form = this.formBuilder.group({
    message: ['', Validators.required]
  });

  constructor() {
    this.recentPrompts.set(this.readRecentPrompts());
  }

  protected products(): BuyerAiProductCardResponse[] {
    return this.response()?.products ?? [];
  }

  protected useExamplePrompt(prompt: string): void {
    this.form.controls.message.setValue(prompt);
    this.form.controls.message.markAsDirty();
    this.form.controls.message.markAsTouched();
  }

  protected useRecentPrompt(prompt: string): void {
    this.useExamplePrompt(prompt);
  }

  protected clearRecentPrompts(): void {
    this.recentPrompts.set([]);
    if (this.isBrowser) {
      localStorage.removeItem(BuyerAiAssistantPageComponent.recentPromptsStorageKey);
    }
  }

  protected intentItems(): Array<{ label: string; value: string }> {
    const intent = this.response()?.intent;
    if (!intent) {
      return [];
    }

    return [
      ['Category', intent.category],
      ['Subcategory', intent.subcategory],
      ['Size', intent.size],
      ['Colour', intent.colour],
      ['Occasion', intent.occasion],
      ['Style', intent.style],
      ['Material', intent.material],
      ['Brand', intent.brand],
      ['Budget', this.renderBudget(intent.budgetMin, intent.budgetMax)],
      ['Beauty skin type', intent.beautySkinType],
      ['Beauty concern', intent.beautyConcern],
      ['Search text', intent.searchText]
    ]
      .filter((entry): entry is [string, string] => Boolean(entry[1]))
      .map(([label, value]) => ({ label, value }));
  }

  protected matchConfidence(): AssistantConfidenceSummary | null {
    const response = this.response();
    if (!response) {
      return null;
    }

    const extractedFieldCount = this.intentItems()
      .filter(item => item.label !== 'Search text')
      .length;
    const productCount = response.products.length;
    const hasClarification = Boolean(response.intent.clarificationPrompt);
    const hasSafetyNote = Boolean(response.safetyNote);
    const score = [
      !response.intent.isVague,
      extractedFieldCount >= 3,
      productCount > 0,
      productCount >= 3,
      !hasClarification,
      !hasSafetyNote
    ].filter(Boolean).length;

    if (score >= 5) {
      return {
        label: 'High confidence',
        band: 'High',
        tone: 'success',
        detail: 'The request had clear signals and the backend returned product matches.',
        signals: [`${extractedFieldCount} intent details`, `${productCount} products`, 'Clear prompt']
      };
    }

    if (score >= 3) {
      return {
        label: 'Medium confidence',
        band: 'Medium',
        tone: 'warning',
        detail: 'The match is useful, but adding size, colour, budget, or occasion may improve the results.',
        signals: [`${extractedFieldCount} intent details`, `${productCount} products`, hasClarification ? 'Needs detail' : 'Backend matched']
      };
    }

    return {
      label: 'Low confidence',
      band: 'Low',
      tone: 'danger',
      detail: 'The assistant needs a clearer shopping request before the matches are reliable.',
      signals: [`${extractedFieldCount} intent details`, `${productCount} products`, hasClarification ? 'Clarification suggested' : 'Try more detail']
    };
  }

  protected assistantShopQueryParams(): Record<string, string | null> {
    const intent = this.response()?.intent;
    const query = intent?.searchText || this.submittedPrompt() || this.form.controls.message.value;

    return {
      query: query?.trim() || null,
      colour: intent?.colour ?? null,
      material: intent?.material ?? null,
      sort: 'relevance'
    };
  }

  protected async search(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const request = {
      message: this.form.controls.message.value.trim()
    };
    if (!request.message) {
      this.form.controls.message.setErrors({ required: true });
      this.form.markAllAsTouched();
      return;
    }

    this.submittedPrompt.set(request.message);
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.rememberPrompt(request.message);

    try {
      this.response.set(await this.assistantService.search(request));
      this.feedbackStatus.set(null);
      this.trackSearchSubmitted();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.response.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected trackProductOpen(product: BuyerAiProductCardResponse): void {
    this.telemetryService.recordEvent({
      eventType: 'AssistantProductOpened',
      sourceTool: 'Assistant',
      productId: product.productId,
      resultCount: this.products().length,
      confidenceBand: this.matchConfidence()?.band ?? null,
      ...this.telemetryContext()
    });
  }

  protected trackShopHandoff(): void {
    this.telemetryService.recordEvent({
      eventType: 'AssistantShopHandoff',
      sourceTool: 'Assistant',
      resultCount: this.products().length,
      confidenceBand: this.matchConfidence()?.band ?? null,
      ...this.telemetryContext()
    });
  }

  protected submitFeedback(reason: BuyerGrowthFeedbackReason): void {
    this.telemetryService.recordEvent({
      eventType: 'AssistantFeedbackSubmitted',
      sourceTool: 'Assistant',
      resultCount: this.products().length,
      confidenceBand: this.matchConfidence()?.band ?? null,
      feedbackReason: reason,
      ...this.telemetryContext()
    });
    this.feedbackStatus.set('Thanks. Feedback was saved without storing your prompt.');
  }

  private trackSearchSubmitted(): void {
    this.telemetryService.recordEvent({
      eventType: 'AssistantSearchSubmitted',
      sourceTool: 'Assistant',
      resultCount: this.products().length,
      confidenceBand: this.matchConfidence()?.band ?? null,
      ...this.telemetryContext()
    });
  }

  private telemetryContext(): {
    category: string | null;
    colour: string | null;
    material: string | null;
    sourceRoute: string;
  } {
    const intent = this.response()?.intent;

    return {
      category: intent?.category ?? null,
      colour: intent?.colour ?? null,
      material: intent?.material ?? null,
      sourceRoute: '/assistant'
    };
  }

  private renderBudget(min: number | null, max: number | null): string | null {
    if (min !== null && max !== null) {
      return `R${min.toLocaleString()} - R${max.toLocaleString()}`;
    }

    if (min !== null) {
      return `From R${min.toLocaleString()}`;
    }

    if (max !== null) {
      return `Up to R${max.toLocaleString()}`;
    }

    return null;
  }

  protected productTone(product: BuyerAiProductCardResponse): ProductVisualTone {
    const text = `${product.title} ${product.matchReasons.join(' ')}`.toLowerCase();
    if (text.includes('earring') || text.includes('jewel') || text.includes('ring') || text.includes('gold')) {
      return 'jewel';
    }

    if (text.includes('cleanser') || text.includes('beauty') || text.includes('skin')) {
      return 'beauty';
    }

    if (text.includes('bag') || text.includes('clutch')) {
      return 'bag';
    }

    if (text.includes('shoe') || text.includes('sneaker') || text.includes('heel')) {
      return 'shoe';
    }

    return 'dress';
  }

  private rememberPrompt(prompt: string): void {
    const normalizedPrompt = prompt.trim();
    if (!normalizedPrompt) {
      return;
    }

    const nextPrompts = [
      { text: normalizedPrompt, savedAtUtc: new Date().toISOString() },
      ...this.recentPrompts().filter(item => item.text.toLowerCase() !== normalizedPrompt.toLowerCase())
    ].slice(0, BuyerAiAssistantPageComponent.maxRecentPromptCount);

    this.recentPrompts.set(nextPrompts);
    this.writeRecentPrompts(nextPrompts);
  }

  private readRecentPrompts(): RecentAssistantPrompt[] {
    if (!this.isBrowser) {
      return [];
    }

    try {
      const value = localStorage.getItem(BuyerAiAssistantPageComponent.recentPromptsStorageKey);
      const parsed = value ? JSON.parse(value) : [];
      return Array.isArray(parsed)
        ? parsed
          .filter((item): item is RecentAssistantPrompt =>
            typeof item?.text === 'string' && typeof item?.savedAtUtc === 'string')
          .slice(0, BuyerAiAssistantPageComponent.maxRecentPromptCount)
        : [];
    } catch {
      return [];
    }
  }

  private writeRecentPrompts(prompts: RecentAssistantPrompt[]): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      localStorage.setItem(BuyerAiAssistantPageComponent.recentPromptsStorageKey, JSON.stringify(prompts));
    } catch {
      // Browsers may block localStorage; recent prompts are optional convenience only.
    }
  }
}
