import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, signal, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerGrowthConfidenceBand, BuyerGrowthFeedbackReason } from '../buyer/buyer-growth-telemetry.models';
import { BuyerGrowthTelemetryService } from '../buyer/buyer-growth-telemetry.service';
import { BuyerVisualSearchProductCardResponse, BuyerVisualSearchResponse } from '../buyer/buyer-visual-search.models';
import { BuyerVisualSearchService } from '../buyer/buyer-visual-search.service';
import { LuxuryBuyerStylesComponent } from '../buyer/luxury-buyer-styles.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

interface RecentVisualReference {
  text: string;
  savedAtUtc: string;
}

interface VisualConfidenceBand {
  label: string;
  band: BuyerGrowthConfidenceBand;
  tone: StatusBadgeTone;
  detail: string;
}

@Component({
  selector: 'app-buyer-visual-search-page',
  imports: [
    CurrencyPipe,
    LuxuryBuyerStylesComponent,
    EmptyStateComponent,
    PageHeaderComponent,
    ProductVisualFallbackComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <app-luxury-buyer-styles />
    <section class="page ai-discovery-page">
      <app-page-header
        eyebrow="Visual search"
        heading="Search from an image"
        description="Upload a product image or describe a reference so Mabuntle can extract visual attributes and match published products."
      >
        <a data-ui-button="secondary" routerLink="/shop" pageHeaderActions>Browse shop</a>
      </app-page-header>

      <section class="ai-discovery-shell">
        <form [formGroup]="form" (ngSubmit)="search()" class="ai-discovery-form" novalidate>
          <div class="ai-discovery-form-copy">
            <app-status-badge label="Buyer tool" tone="accent" />
            <h2>Upload or describe the item</h2>
            <p>Visual search extracts broad product attributes. It does not verify brand, exact material, fit, or condition from an image.</p>
          </div>

          <label class="visual-upload-control">
            <span>Image upload</span>
            <input #imageInput type="file" accept="image/png,image/jpeg,image/webp" (change)="onFileSelected($event)">
            <small>PNG, JPEG, or WebP up to 5 MB.</small>
          </label>

          @if (selectedFileName()) {
            <div class="visual-file-summary">
              @if (imagePreviewDataUrl()) {
                <img [src]="imagePreviewDataUrl()!" alt="Selected visual search preview">
              } @else {
                <div class="visual-file-placeholder">Image selected</div>
              }
              <div>
                <strong>{{ selectedFileName() }}</strong>
                <span>{{ isReadingImage() ? 'Preparing image...' : 'Ready to search' }}</span>
              </div>
              <button data-ui-button="ghost" type="button" (click)="clearSelectedFile(imageInput)">Remove</button>
            </div>
          }

          <label class="ui-field">
            <span>Image reference</span>
            <input formControlName="imageReference" placeholder="black formal maxi dress flatlay">
          </label>

          @if (recentReferences().length > 0) {
            <div class="ai-recent-panel" aria-label="Recent visual references">
              <div>
                <strong>Recent references</strong>
                <button data-ui-button="ghost" type="button" (click)="clearRecentReferences()">Clear</button>
              </div>
              <div class="ai-chip-row">
                @for (recent of recentReferences(); track recent.text) {
                  <button data-ui-button="secondary" type="button" (click)="useRecentReference(recent.text)">
                    {{ recent.text }}
                  </button>
                }
              </div>
              <small>Only text references are stored in this browser. Uploaded images are not saved.</small>
            </div>
          }

          <div class="visual-control-row">
            <button data-ui-button="primary" type="submit" [disabled]="isLoading() || isReadingImage()">
              {{ isLoading() ? 'Searching...' : 'Search visually' }}
            </button>
            <button data-ui-button="secondary" type="button" (click)="resetSearch(imageInput)" [disabled]="isLoading()">
              Reset search
            </button>
          </div>
        </form>

        <aside class="ai-discovery-guide" aria-label="Visual search guidance">
          <strong>Works best with:</strong>
          <span>One clear product in frame</span>
          <span>Visible colour and shape</span>
          <span>Plain background or product photo</span>
          <span>Optional text reference for context</span>
        </aside>
      </section>

      @if (errorMessage()) {
        <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
      }

      @if (isLoading()) {
        <app-ui-alert>Extracting visual attributes and searching published products...</app-ui-alert>
      }

      @if (response()) {
        <section class="ai-result-panel" aria-label="Visual search result summary">
          <div class="ai-result-copy">
            @if (confidenceBand(); as confidence) {
              <app-status-badge [label]="confidence.label" [tone]="confidence.tone" />
              <p class="ai-confidence-copy">{{ confidence.detail }}</p>
            }
            <h2>Visual attributes</h2>
            <p>{{ response()!.summary }}</p>
            <app-ui-alert>{{ response()!.imageRetentionNote }}</app-ui-alert>
            @for (warning of response()!.attributes.warnings; track warning) {
              <app-ui-alert tone="warning">{{ warning }}</app-ui-alert>
            }
          </div>

          <div class="ai-intent-grid" aria-label="Extracted visual attributes">
            @for (attribute of extractedAttributes(); track attribute.label) {
              <span>
                <small>{{ attribute.label }}</small>
                <strong>{{ attribute.value }}</strong>
              </span>
            }
            <span>
              <small>Search text</small>
              <strong>{{ response()!.attributes.searchText }}</strong>
            </span>
          </div>
        </section>

        <section class="ai-results-section" aria-label="Visual search product matches">
          <div class="ai-results-header">
            <div>
              <h2>Product matches</h2>
              <p>{{ products().length }} {{ products().length === 1 ? 'match' : 'matches' }} returned by backend search.</p>
            </div>
            <a data-ui-button="secondary" routerLink="/shop" [queryParams]="visualShopQueryParams()" (click)="trackShopHandoff()">Search manually</a>
          </div>

          @if (products().length > 0) {
            <div class="ai-result-grid">
              @for (product of products(); track product.productId) {
                <a class="ai-product-result-card hf-ai-product-card" [routerLink]="['/product', product.slug]" (click)="trackProductOpen(product)">
                  <div class="ai-product-result-media hf-ai-product-media">
                    @if (product.imageUrl) {
                      <img [src]="product.imageUrl" [alt]="product.title">
                    } @else {
                      <app-product-visual-fallback
                        [title]="product.title"
                        label="Visual match"
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
          } @else {
            <app-empty-state
              eyebrow="No matches"
              heading="No product cards to show"
              message="Try a clearer item photo, a more specific reference, or a broader manual search."
            >
              <a data-ui-button="secondary" routerLink="/shop" [queryParams]="visualShopQueryParams()" (click)="trackShopHandoff()">Open shop</a>
            </app-empty-state>
          }
        </section>

        <section class="ai-feedback-card" aria-label="Visual search usefulness feedback">
          <div>
            <strong>Was this useful?</strong>
            <span>Feedback is stored as a reason code only. Uploaded images are not stored.</span>
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
        </section>
      }
    </section>
  `
})
export class BuyerVisualSearchPageComponent {
  private static readonly supportedImageTypes = new Set(['image/png', 'image/jpeg', 'image/webp']);
  private static readonly maxImageSizeBytes = 5 * 1024 * 1024;
  private static readonly recentReferencesStorageKey = 'mabuntle.buyer.visualSearch.recentReferences';
  private static readonly maxRecentReferenceCount = 6;

  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly visualSearchService = inject(BuyerVisualSearchService);
  private readonly telemetryService = inject(BuyerGrowthTelemetryService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly response = signal<BuyerVisualSearchResponse | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isReadingImage = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly selectedFileName = signal<string | null>(null);
  protected readonly imagePreviewDataUrl = signal<string | null>(null);
  protected readonly recentReferences = signal<RecentVisualReference[]>([]);
  protected readonly feedbackStatus = signal<string | null>(null);
  protected readonly feedbackReasons: Array<{ value: BuyerGrowthFeedbackReason; label: string }> = [
    { value: 'GoodMatches', label: 'Good matches' },
    { value: 'TooBroad', label: 'Too broad' },
    { value: 'WrongStyle', label: 'Wrong style' },
    { value: 'WrongCategory', label: 'Wrong category' },
    { value: 'Unavailable', label: 'Unavailable' },
    { value: 'LowConfidence', label: 'Low confidence' }
  ];

  protected readonly form = this.formBuilder.group({
    imageReference: ['']
  });

  private imageDataBase64: string | null = null;
  private contentType: string | null = null;

  constructor() {
    this.recentReferences.set(this.readRecentReferences());
  }

  protected products(): BuyerVisualSearchProductCardResponse[] {
    return this.response()?.products ?? [];
  }

  protected extractedAttributes(): Array<{ label: string; value: string }> {
    const attributes = this.response()?.attributes;
    if (!attributes) {
      return [];
    }

    return [
      ['Category', attributes.category],
      ['Colour', attributes.colour],
      ['Style', attributes.style],
      ['Shape', attributes.shape],
      ['Pattern', attributes.pattern],
      ['Material guess', attributes.materialGuess]
    ]
      .filter((entry): entry is [string, string] => Boolean(entry[1]))
      .map(([label, value]) => ({ label, value }));
  }

  protected confidenceBand(): VisualConfidenceBand | null {
    const attributes = this.response()?.attributes;
    if (!attributes) {
      return null;
    }

    const percent = Math.round(attributes.confidence * 100);
    if (attributes.confidence >= 0.75) {
      return {
        label: `High visual match (${percent}%)`,
        band: 'High',
        tone: 'success',
        detail: 'The extracted visual signals are strong, but product photos still cannot verify brand, fit, condition, or exact material.'
      };
    }

    if (attributes.confidence >= 0.45) {
      return {
        label: `Medium visual match (${percent}%)`,
        band: 'Medium',
        tone: 'warning',
        detail: 'Use these matches as a starting point. Add a text reference or browse manually if colour, material, or shape matters.'
      };
    }

    return {
      label: `Low visual match (${percent}%)`,
      band: 'Low',
      tone: 'danger',
      detail: 'The image or reference was hard to interpret. A clearer product photo or broader manual search will be more reliable.'
    };
  }

  protected useRecentReference(reference: string): void {
    this.form.controls.imageReference.setValue(reference);
    this.form.controls.imageReference.markAsDirty();
    this.form.controls.imageReference.markAsTouched();
  }

  protected clearRecentReferences(): void {
    this.recentReferences.set([]);
    if (this.isBrowser) {
      localStorage.removeItem(BuyerVisualSearchPageComponent.recentReferencesStorageKey);
    }
  }

  protected visualShopQueryParams(): Record<string, string | null> {
    const attributes = this.response()?.attributes;

    return {
      query: attributes?.searchText || this.form.controls.imageReference.value.trim() || null,
      colour: attributes?.colour ?? null,
      material: attributes?.materialGuess ?? null,
      sort: 'relevance'
    };
  }

  protected productTone(product: BuyerVisualSearchProductCardResponse): ProductVisualTone {
    const text = `${product.title} ${product.sellerDisplayName ?? ''}`.toLowerCase();
    if (/(jewel|ring|earring|necklace|bracelet|gold|silver)/.test(text)) {
      return 'jewel';
    }

    if (/(beauty|skin|makeup|lip|hair|fragrance|serum)/.test(text)) {
      return 'beauty';
    }

    if (/(bag|tote|clutch|purse|wallet)/.test(text)) {
      return 'bag';
    }

    if (/(shoe|heel|sneaker|boot|sandal)/.test(text)) {
      return 'shoe';
    }

    if (/(dress|coat|shirt|denim|fashion|clothing|linen|silk)/.test(text)) {
      return 'dress';
    }

    return 'neutral';
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    this.clearSelectedFile();
    this.errorMessage.set(null);

    if (!file) {
      return;
    }

    if (!BuyerVisualSearchPageComponent.supportedImageTypes.has(file.type)) {
      this.errorMessage.set('Upload a PNG, JPEG, or WebP image.');
      input.value = '';
      return;
    }

    if (file.size > BuyerVisualSearchPageComponent.maxImageSizeBytes) {
      this.errorMessage.set('Upload an image up to 5 MB.');
      input.value = '';
      return;
    }

    this.contentType = file.type;
    this.selectedFileName.set(file.name);
    this.isReadingImage.set(true);

    const reader = new FileReader();
    reader.onload = () => {
      const result = typeof reader.result === 'string' ? reader.result : '';
      this.imageDataBase64 = result.includes(',') ? result.split(',')[1] : result;
      this.imagePreviewDataUrl.set(result || null);
      this.isReadingImage.set(false);
    };
    reader.onerror = () => {
      this.clearSelectedFile();
      this.errorMessage.set('The selected image could not be prepared for search.');
      input.value = '';
      this.isReadingImage.set(false);
    };
    reader.readAsDataURL(file);
  }

  protected async search(): Promise<void> {
    const rawReference = this.form.controls.imageReference.value.trim();
    if (this.isReadingImage()) {
      this.errorMessage.set('Wait for the selected image to finish preparing.');
      return;
    }

    if (!rawReference && !this.imageDataBase64) {
      this.errorMessage.set('Upload an image or enter an image reference.');
      this.response.set(null);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    if (rawReference) {
      this.rememberReference(rawReference);
    }

    try {
      this.response.set(await this.visualSearchService.search({
        imageReference: rawReference || null,
        imageDataBase64: this.imageDataBase64,
        fileName: this.selectedFileName(),
        contentType: this.contentType
      }));
      this.feedbackStatus.set(null);
      this.trackSearchSubmitted();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.response.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected clearSelectedFile(input?: HTMLInputElement): void {
    this.imageDataBase64 = null;
    this.contentType = null;
    this.selectedFileName.set(null);
    this.imagePreviewDataUrl.set(null);
    this.isReadingImage.set(false);
    if (input) {
      input.value = '';
    }
  }

  protected resetSearch(input?: HTMLInputElement): void {
    this.clearSelectedFile(input);
    this.form.reset({ imageReference: '' });
    this.response.set(null);
    this.errorMessage.set(null);
    this.feedbackStatus.set(null);
  }

  protected trackProductOpen(product: BuyerVisualSearchProductCardResponse): void {
    this.telemetryService.recordEvent({
      eventType: 'VisualProductOpened',
      sourceTool: 'VisualSearch',
      productId: product.productId,
      resultCount: this.products().length,
      confidenceBand: this.confidenceBand()?.band ?? null,
      ...this.telemetryContext()
    });
  }

  protected trackShopHandoff(): void {
    this.telemetryService.recordEvent({
      eventType: 'VisualShopHandoff',
      sourceTool: 'VisualSearch',
      resultCount: this.products().length,
      confidenceBand: this.confidenceBand()?.band ?? null,
      ...this.telemetryContext()
    });
  }

  protected submitFeedback(reason: BuyerGrowthFeedbackReason): void {
    this.telemetryService.recordEvent({
      eventType: 'VisualFeedbackSubmitted',
      sourceTool: 'VisualSearch',
      resultCount: this.products().length,
      confidenceBand: this.confidenceBand()?.band ?? null,
      feedbackReason: reason,
      ...this.telemetryContext()
    });
    this.feedbackStatus.set('Thanks. Feedback was saved without storing image data.');
  }

  private trackSearchSubmitted(): void {
    this.telemetryService.recordEvent({
      eventType: 'VisualSearchSubmitted',
      sourceTool: 'VisualSearch',
      resultCount: this.products().length,
      confidenceBand: this.confidenceBand()?.band ?? null,
      ...this.telemetryContext()
    });
  }

  private telemetryContext(): {
    category: string | null;
    colour: string | null;
    material: string | null;
    sourceRoute: string;
  } {
    const attributes = this.response()?.attributes;

    return {
      category: attributes?.category ?? null,
      colour: attributes?.colour ?? null,
      material: attributes?.materialGuess ?? null,
      sourceRoute: '/visual-search'
    };
  }

  private rememberReference(reference: string): void {
    const normalizedReference = reference.trim();
    if (!normalizedReference) {
      return;
    }

    const nextReferences = [
      { text: normalizedReference, savedAtUtc: new Date().toISOString() },
      ...this.recentReferences().filter(item => item.text.toLowerCase() !== normalizedReference.toLowerCase())
    ].slice(0, BuyerVisualSearchPageComponent.maxRecentReferenceCount);

    this.recentReferences.set(nextReferences);
    this.writeRecentReferences(nextReferences);
  }

  private readRecentReferences(): RecentVisualReference[] {
    if (!this.isBrowser) {
      return [];
    }

    try {
      const value = localStorage.getItem(BuyerVisualSearchPageComponent.recentReferencesStorageKey);
      const parsed = value ? JSON.parse(value) : [];
      return Array.isArray(parsed)
        ? parsed
          .filter((item): item is RecentVisualReference =>
            typeof item?.text === 'string' && typeof item?.savedAtUtc === 'string')
          .slice(0, BuyerVisualSearchPageComponent.maxRecentReferenceCount)
        : [];
    } catch {
      return [];
    }
  }

  private writeRecentReferences(references: RecentVisualReference[]): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      localStorage.setItem(BuyerVisualSearchPageComponent.recentReferencesStorageKey, JSON.stringify(references));
    } catch {
      // Recent visual references are optional and must not block visual search.
    }
  }
}
