import { CurrencyPipe } from '@angular/common';
import { Component, signal, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerVisualSearchProductCardResponse, BuyerVisualSearchResponse } from '../buyer/buyer-visual-search.models';
import { BuyerVisualSearchService } from '../buyer/buyer-visual-search.service';
import { LuxuryBuyerStylesComponent } from '../buyer/luxury-buyer-styles.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-visual-search-page',
  imports: [
    CurrencyPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
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
        description="Upload a product image or describe a reference so Swyftly can extract visual attributes and match published products."
      >
        <a mat-stroked-button routerLink="/shop" pageHeaderActions>Browse shop</a>
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
            <input type="file" accept="image/png,image/jpeg,image/webp" (change)="onFileSelected($event)">
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
            </div>
          }

          <mat-form-field class="swyftly-field" appearance="outline" hideRequiredMarker>
            <mat-label>Image reference</mat-label>
            <input matInput formControlName="imageReference" placeholder="black formal maxi dress flatlay">
          </mat-form-field>

          <button mat-flat-button type="submit" [disabled]="isLoading() || isReadingImage()">
            {{ isLoading() ? 'Searching...' : 'Search visually' }}
          </button>
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
            <app-status-badge [label]="confidenceLabel()" tone="success" />
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
            <a mat-stroked-button routerLink="/shop">Search manually</a>
          </div>

          @if (products().length > 0) {
            <div class="ai-result-grid">
              @for (product of products(); track product.productId) {
                <a class="ai-product-result-card hf-ai-product-card" [routerLink]="['/product', product.slug]">
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
                    <span>{{ product.sellerDisplayName ?? 'Swyftly seller' }}</span>
                    <h3>{{ product.title }}</h3>
                    <strong>{{ product.price | currency:product.currency:'symbol':'1.2-2' }}</strong>
                    <ul>
                      @for (reason of product.matchReasons; track reason) {
                        <li>{{ reason }}</li>
                      }
                    </ul>
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
              <a mat-stroked-button routerLink="/shop">Open shop</a>
            </app-empty-state>
          }
        </section>
      }
    </section>
  `
})
export class BuyerVisualSearchPageComponent {
  private static readonly supportedImageTypes = new Set(['image/png', 'image/jpeg', 'image/webp']);
  private static readonly maxImageSizeBytes = 5 * 1024 * 1024;

  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly visualSearchService = inject(BuyerVisualSearchService);

  protected readonly response = signal<BuyerVisualSearchResponse | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isReadingImage = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly selectedFileName = signal<string | null>(null);
  protected readonly imagePreviewDataUrl = signal<string | null>(null);

  protected readonly form = this.formBuilder.group({
    imageReference: ['']
  });

  private imageDataBase64: string | null = null;
  private contentType: string | null = null;

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

  protected confidenceLabel(): string {
    const confidence = this.response()?.attributes.confidence ?? 0;
    return `Confidence ${Math.round(confidence * 100)}%`;
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

    try {
      this.response.set(await this.visualSearchService.search({
        imageReference: rawReference || null,
        imageDataBase64: this.imageDataBase64,
        fileName: this.selectedFileName(),
        contentType: this.contentType
      }));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.response.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private clearSelectedFile(): void {
    this.imageDataBase64 = null;
    this.contentType = null;
    this.selectedFileName.set(null);
    this.imagePreviewDataUrl.set(null);
    this.isReadingImage.set(false);
  }
}
