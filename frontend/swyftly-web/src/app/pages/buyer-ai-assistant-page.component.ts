import { CurrencyPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { BuyerAiProductCardResponse, BuyerAiShoppingAssistantResponse } from '../buyer/buyer-ai-assistant.models';
import { BuyerAiAssistantService } from '../buyer/buyer-ai-assistant.service';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { ProductVisualFallbackComponent, ProductVisualTone } from '../shared/ui/product-visual-fallback.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-ai-assistant-page',
  imports: [
    CurrencyPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    EmptyStateComponent,
    ProductVisualFallbackComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page ai-discovery-page ai-style-page">
      <section class="ai-style-layout">
        <aside class="ai-chat-panel" aria-label="AI Style Finder">
          <div class="ai-chat-title">
            <div>
              <span class="eyebrow">AI Style Finder</span>
              <h1>Tell Swyftly what you need</h1>
            </div>
            <app-status-badge label="AI" tone="accent" />
          </div>

          <div class="ai-chat-transcript" aria-live="polite">
            <div class="ai-chat-bubble ai-chat-bubble--bot">
              I can find outfits, gifts, jewellery, beauty products, or occasion-ready pieces from published Swyftly listings.
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
            <mat-form-field appearance="outline">
              <mat-label>Ask to refine results</mat-label>
              <textarea matInput rows="3" formControlName="message" placeholder="Find a wedding outfit under R1,500 in neutral colours"></textarea>
              @if (form.controls.message.hasError('required')) {
                <mat-error>Enter a shopping request.</mat-error>
              }
            </mat-form-field>

            <div class="ai-example-row" aria-label="Example shopping requests">
              @for (example of examplePrompts; track example) {
                <button mat-stroked-button type="button" (click)="useExamplePrompt(example)">
                  {{ example }}
                </button>
              }
            </div>

            <button mat-flat-button type="submit" [disabled]="form.invalid || isLoading()">
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
            <a mat-stroked-button routerLink="/shop">Browse shop</a>
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

            @if (products().length > 0) {
              <div class="ai-result-grid hf-ai-result-grid">
                @for (product of products(); track product.productId) {
                  <a class="ai-product-result-card hf-ai-product-card" [routerLink]="['/product', product.slug]">
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

              <div class="ai-why-card">
                <h3>Why this works</h3>
                <p>{{ response()!.summary }}</p>
                <div class="active-filter-row">
                  <app-status-badge [label]="products().length + ' real listings'" tone="success" />
                  <app-status-badge label="Backend matched" />
                  <app-status-badge label="No stock reservation" tone="accent" />
                </div>
              </div>
            } @else {
              <app-empty-state
                eyebrow="No matches"
                heading="No product cards to show"
                message="Try a broader category, colour, size, style, or budget."
              >
                <button mat-stroked-button type="button" (click)="useExamplePrompt(examplePrompts[0])">Use an example</button>
              </app-empty-state>
            }
          } @else {
            <app-empty-state
              eyebrow="Ready when you are"
              heading="Ask for a style, gift, product, or beauty need"
              message="Example prompts can fill the box, but the assistant only searches when you submit."
            >
              <button mat-stroked-button type="button" (click)="useExamplePrompt(examplePrompts[0])">Start with an example</button>
            </app-empty-state>
          }
        </section>
      </section>
    </section>
  `
})
export class BuyerAiAssistantPageComponent {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly assistantService = inject(BuyerAiAssistantService);

  protected readonly response = signal<BuyerAiShoppingAssistantResponse | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly submittedPrompt = signal<string | null>(null);
  protected readonly examplePrompts = [
    'Wedding outfit under R1,500, neutral colours',
    'Minimal gold earrings for everyday wear',
    'Gentle cleanser for oily skin'
  ];

  protected readonly form = this.formBuilder.group({
    message: ['', Validators.required]
  });

  protected products(): BuyerAiProductCardResponse[] {
    return this.response()?.products ?? [];
  }

  protected useExamplePrompt(prompt: string): void {
    this.form.controls.message.setValue(prompt);
    this.form.controls.message.markAsDirty();
    this.form.controls.message.markAsTouched();
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
      ['Budget', this.formatBudget(intent.budgetMin, intent.budgetMax)],
      ['Beauty skin type', intent.beautySkinType],
      ['Beauty concern', intent.beautyConcern],
      ['Search text', intent.searchText]
    ]
      .filter((entry): entry is [string, string] => Boolean(entry[1]))
      .map(([label, value]) => ({ label, value }));
  }

  protected async search(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const request = this.form.getRawValue();
    this.submittedPrompt.set(request.message);
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.response.set(await this.assistantService.search(request));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.response.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private formatBudget(min: number | null, max: number | null): string | null {
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
}
