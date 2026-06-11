import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  FormRecord,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import {
  ApplySellerAiSuggestionRequest,
  GenerateSellerAiSuggestionRequest,
  SellerAiSuggestionResponse,
  SellerCatalogCategoryAttributeResponse,
  SellerCatalogCategoryResponse,
  SellerProductDetailResponse,
  SellerProductImageResponse,
  SellerProductRevisionImageResponse,
  SellerProductRevisionResponse,
  SellerProductVariantRevisionBulkImportResponse,
  SellerProductVariantRevisionItemResponse,
  SellerProductVariantRevisionResponse,
  SellerProductVariantResponse,
  UpsertSellerProductRequest,
  UpsertSellerProductRevisionRequest,
  UpsertSellerProductVariantRevisionItemRequest,
  UpsertSellerProductVariantRequest
} from '../seller/seller-product.models';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { ProductVisualFallbackComponent } from '../shared/ui/product-visual-fallback.component';

type ProductStep = 0 | 1 | 2 | 3 | 4 | 5;
type ProductEditorImage = (SellerProductImageResponse | SellerProductRevisionImageResponse) & { imageId?: string };

@Component({
  selector: 'app-seller-product-form-page',
  imports: [
    DatePipe,
    FormsModule,
    ProductVisualFallbackComponent,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent
  ],
  template: `
    <section class="page seller-ops-page product-editor">
      <app-seller-workspace-nav />

      <a class="admin-back-link" routerLink="/products">Back to products</a>

      <div class="page-header product-editor-hero">
        <div>
          <span class="eyebrow">Product editor</span>
          <h1>{{ product()?.title ?? 'New product' }}</h1>
          <p>{{ editorModeMessage() }}</p>
        </div>
        <div class="product-editor-status">
          <span class="status-pill">{{ product()?.status ?? 'Draft' }}</span>
          @if (publicPreviewUrl()) {
            <a data-ui-button="secondary" [routerLink]="publicPreviewUrl()">Public preview</a>
          }
        </div>
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading product editor...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (successMessage()) {
          <p class="auth-alert success" role="status">{{ successMessage() }}</p>
        }

        @if (!isListingEditable()) {
          <p class="auth-alert" role="status">
            {{ isRevisionMode() ? 'This listing has a revision in review. Buyers continue to see the current approved listing until admin approval.' : 'This listing is not editable in its current status. Use Inventory for stock changes, or wait for moderation before editing listing content again.' }}
          </p>
        }

        @if (revision()?.rejectionReason) {
          <p class="auth-alert error" role="status">{{ revision()?.rejectionReason }}</p>
        }

        @if (product()?.rejectionReason) {
          <p class="auth-alert error" role="status">{{ product()?.rejectionReason }}</p>
        }

        <article class="route-card admin-detail-card seller-moderation-card">
          <div class="seller-products-header">
            <div>
              <span class="eyebrow">Moderation</span>
              <h2>Review history</h2>
              <p>Admin decisions and seller-facing reasons for this listing.</p>
            </div>
          </div>

          @if (moderationTimeline().length === 0) {
            <p class="form-helper">No admin review decisions have been recorded yet.</p>
          } @else {
            <div class="admin-audit-list">
              @for (event of moderationTimeline(); track event.auditLogId) {
                <div>
                  <span class="status-pill">{{ moderationLabel(event.actionType) }}</span>
                  <strong>{{ event.createdAtUtc | date:'medium' }}</strong>
                  @if (event.reason) {
                    <p>{{ event.reason }}</p>
                  } @else {
                    <p>No seller action is required from this decision.</p>
                  }
                </div>
              }
            </div>
          }
        </article>

        <section class="ai-assistant-panel hf-ai-listing-assistant" aria-labelledby="ai-product-assistant-title">
          <div class="hf-ai-listing-layout">
            <div class="hf-ai-listing-input">
              <div class="ai-assistant-header">
                <div>
                  <span class="eyebrow">Create product</span>
                  <h2 id="ai-product-assistant-title">AI Fashion Product Listing Assistant</h2>
                </div>
                <span class="ai-quality">{{ aiSuggestion()?.qualityScore ?? 0 }} / 100</span>
              </div>

              <div class="hf-ai-listing-context">
                <div class="hf-ai-listing-preview" aria-label="Product draft visual">
                  @if (primaryImage(); as image) {
                    <img [src]="image.url" [alt]="image.altText ?? product()?.title ?? 'Product image'" />
                  } @else {
                    <app-product-visual-fallback [title]="product()?.title ?? 'Draft listing'" label="Seller draft" tone="dress" />
                  }
                </div>

                <div class="hf-ai-listing-notes">
                  <strong>Seller input</strong>
                  <span>{{ product()?.title ?? 'Save the product draft before generating suggestions.' }}</span>
                  <span>{{ selectedCategory()?.name ?? 'Choose a category to improve attribute suggestions.' }}</span>
                  <span>{{ (product()?.images?.length ?? 0) }} image{{ (product()?.images?.length ?? 0) === 1 ? '' : 's' }} attached</span>
                  <span>{{ (product()?.variants?.length ?? 0) }} variant{{ (product()?.variants?.length ?? 0) === 1 ? '' : 's' }} configured</span>
                </div>
              </div>

              <form [formGroup]="aiForm" (ngSubmit)="generateAiSuggestion()" class="ai-form hf-ai-listing-form" novalidate>
                <label class="ui-field">
                  <span>Seller notes</span>
                  <textarea rows="3" formControlName="sellerNotes"></textarea>
                </label>
                <label class="ui-field">
                  <span>Product type hint</span>
                  <input formControlName="productTypeHint" />
                </label>
                <button data-ui-button="primary" type="submit" [disabled]="!isProductEditable() || isAiGenerating() || isSaving()">
                  {{ isAiGenerating() ? 'Generating...' : 'Generate AI suggestion' }}
                </button>
              </form>

              <p class="ai-disclaimer">AI suggestions are drafts. Review and confirm every field before publishing.</p>
            </div>

            <div class="hf-ai-listing-suggestion">
              <span class="eyebrow">AI suggestion</span>
              <h2>Quality score: {{ aiSuggestion()?.qualityScore ?? 0 }}%</h2>
              <p>Use AI for title, description, category, attributes, tags, missing fields, and alt text. It never publishes a product for you.</p>
              <div class="hf-progress-ring" aria-label="AI suggestion quality"><strong>{{ aiSuggestion()?.qualityScore ?? 0 }}</strong></div>
            </div>
          </div>

          @if (aiErrorMessage()) {
            <p class="auth-alert error" role="alert">{{ aiErrorMessage() }}</p>
          }

          @if (aiSuggestion(); as suggestion) {
            <div class="ai-suggestion-grid hf-ai-suggestion-grid">
              <article class="ai-suggestion-card">
                <span class="status-pill">Title</span>
                <h2>{{ suggestion.recommendedTitle ?? 'No title suggested' }}</h2>
                @if (suggestion.titleSuggestions.length > 1) {
                  <p>{{ suggestion.titleSuggestions.join(' / ') }}</p>
                }
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Descriptions</span>
                <p>{{ suggestion.shortDescription ?? 'No short description suggested' }}</p>
                <p>{{ suggestion.fullDescription ?? 'No full description suggested' }}</p>
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Category</span>
                <h2>{{ suggestion.suggestedCategoryPath ?? categoryName(suggestion.suggestedCategoryId) ?? 'No category suggested' }}</h2>
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Attributes</span>
                @for (attribute of aiAttributeEntries(); track attribute.key) {
                  <p><strong>{{ attribute.key }}</strong>: {{ attribute.value }}</p>
                } @empty {
                  <p>No attributes suggested.</p>
                }
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Tags</span>
                <p>{{ suggestion.tags.length ? suggestion.tags.join(', ') : 'No tags suggested.' }}</p>
              </article>
              <article class="ai-suggestion-card">
                <span class="status-pill">Missing fields</span>
                @for (field of suggestion.missingFields; track field) {
                  <p>{{ field }}</p>
                } @empty {
                  <p>No missing fields returned.</p>
                }
              </article>
            </div>

            @if (suggestion.riskFlags.length > 0) {
              <div class="ai-risk-box" role="alert">
                <strong>Risk flags</strong>
                @for (flag of suggestion.riskFlags; track flag) {
                  <p>{{ flag }}</p>
                }
              </div>
            }

            <form [formGroup]="aiApplyForm" (ngSubmit)="applyAiSuggestion()" class="ai-apply-form" novalidate>
              <div class="ai-field-grid">
                <label class="ui-checkbox"><input type="checkbox" formControlName="title"><span>Title</span></label>
                <label class="ui-checkbox"><input type="checkbox" formControlName="shortDescription"><span>Short description</span></label>
                <label class="ui-checkbox"><input type="checkbox" formControlName="fullDescription"><span>Full description</span></label>
                <label class="ui-checkbox"><input type="checkbox" formControlName="category"><span>Category</span></label>
                <label class="ui-checkbox"><input type="checkbox" formControlName="attributes"><span>Attributes</span></label>
                <label class="ui-checkbox"><input type="checkbox" formControlName="tags"><span>Tags</span></label>
                <label class="ui-checkbox"><input type="checkbox" formControlName="imageAltText"><span>Image alt text</span></label>
              </div>

              <div [formGroup]="aiEditForm" class="ai-edit-grid">
                <label class="ui-field">
                  <span>Reviewed title</span>
                  <input formControlName="title" />
                </label>
                <label class="ui-field">
                  <span>Reviewed short description</span>
                  <textarea rows="2" formControlName="shortDescription"></textarea>
                </label>
                <label class="ui-field">
                  <span>Reviewed full description</span>
                  <textarea rows="4" formControlName="fullDescription"></textarea>
                </label>
                <label class="ui-field">
                  <span>Reviewed category</span>
                  <select formControlName="suggestedCategoryId">
                    @for (category of categories(); track category.categoryId) {
                      <option [value]="category.categoryId">{{ categoryLabel(category) }}</option>
                    }
                  </select>
                </label>
                <label class="ui-field">
                  <span>Reviewed tags</span>
                  <input formControlName="tags" />
                </label>
              </div>

              @if (aiAttributeEntries().length > 0) {
                <div [formGroup]="aiAttributeEditForm" class="dynamic-attributes">
                  @for (attribute of aiAttributeEntries(); track attribute.key) {
                    <label class="ui-field">
                      <span>{{ attribute.key }}</span>
                      <input [formControlName]="attribute.key" />
                    </label>
                  }
                </div>
              }

              @if ((product()?.images?.length ?? 0) > 0) {
                <div [formGroup]="aiImageAltTextForm" class="dynamic-attributes">
                  @for (image of product()?.images ?? []; track image.imageId) {
                    <label class="ui-field">
                      <span>Alt text for {{ image.storageKey }}</span>
                      <input [formControlName]="image.imageId" />
                    </label>
                  }
                </div>
              }

              @if (suggestion.riskFlags.length > 0) {
                <label class="ui-checkbox"><input type="checkbox" formControlName="confirmRiskFlags"><span>Confirm risk flags</span></label>
              }

              <button data-ui-button="primary" type="submit" [disabled]="!isProductEditable() || isAiApplying()">
                {{ isAiApplying() ? 'Applying...' : 'Apply selected suggestions' }}
              </button>
            </form>
          }
        </section>

        <div class="wizard-layout">
          <nav class="wizard-steps" aria-label="Product form sections">
            @for (step of steps; track step.index) {
              <button
                type="button"
                [class.active]="currentStep() === step.index"
                [class.complete]="isStepComplete(step.index)"
                (click)="currentStep.set(step.index)"
              >
                <span>{{ step.index + 1 }}</span>
                {{ step.label }}
              </button>
            }
          </nav>

          <div class="wizard-panel">
            @switch (currentStep()) {
              @case (0) {
                <form [formGroup]="basicForm" (ngSubmit)="saveDraft()" class="wizard-form" novalidate>
                  <h2>Basic details</h2>
                  <p class="form-helper">These fields define how buyers see and find the listing. Save drafts before adding images, variants, or AI suggestions.</p>
                  <label class="ui-field">
                    <span>Title</span>
                    <input formControlName="title" />
                    @if (basicForm.controls.title.hasError('required')) {
                      <span class="ui-field-error">Title is required.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Slug</span>
                    <input formControlName="slug" />
                    <span class="ui-field-hint">Lowercase letters, numbers, and hyphens only. Example: black-evening-dress.</span>
                    @if (basicForm.controls.slug.hasError('required')) {
                      <span class="ui-field-error">Slug is required.</span>
                    } @else if (basicForm.controls.slug.hasError('pattern')) {
                      <span class="ui-field-error">Use lowercase letters, numbers, and hyphens.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Short description</span>
                    <textarea rows="3" formControlName="shortDescription"></textarea>
                    @if (basicForm.controls.shortDescription.hasError('required')) {
                      <span class="ui-field-error">Short description is required.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Full description</span>
                    <textarea rows="5" formControlName="fullDescription"></textarea>
                    @if (basicForm.controls.fullDescription.hasError('required')) {
                      <span class="ui-field-error">Full description is required.</span>
                    }
                  </label>

                  <div class="product-editor-context">
                    <strong>Merchandising and SEO</strong>
                    <span>These buyer-facing fields support presentation and metadata. They do not guarantee search ranking.</span>
                  </div>

                  <label class="ui-field">
                    <span>Merchandising label</span>
                    <input formControlName="merchandisingLabel" maxlength="60" />
                    <span class="ui-field-hint">Optional short badge such as New season, Limited edit, or Seller pick.</span>
                    @if (basicForm.controls.merchandisingLabel.hasError('maxlength')) {
                      <span class="ui-field-error">Use 60 characters or fewer.</span>
                    }
                  </label>

                  <div class="form-grid">
                    <label class="ui-field">
                      <span>SEO title</span>
                      <input formControlName="seoTitle" maxlength="70" />
                      <span class="ui-field-hint">{{ basicForm.controls.seoTitle.value.length }}/70</span>
                      @if (basicForm.controls.seoTitle.hasError('maxlength')) {
                        <span class="ui-field-error">Use 70 characters or fewer.</span>
                      }
                    </label>

                    <label class="ui-field">
                      <span>SEO description</span>
                      <textarea rows="3" formControlName="seoDescription" maxlength="170"></textarea>
                      <span class="ui-field-hint">{{ basicForm.controls.seoDescription.value.length }}/170</span>
                      @if (basicForm.controls.seoDescription.hasError('maxlength')) {
                        <span class="ui-field-error">Use 170 characters or fewer.</span>
                      }
                    </label>
                  </div>

                  <div class="product-editor-preview-card product-editor-search-preview">
                    <div>
                      <span class="status-pill">Search preview</span>
                      <h3>{{ seoPreviewTitle() }}</h3>
                      <p>{{ seoPreviewDescription() }}</p>
                    </div>
                  </div>

                  <div class="form-grid">
                    <label class="ui-field">
                      <span>Product care instructions</span>
                      <textarea rows="3" formControlName="careInstructions" maxlength="1000"></textarea>
                      <span class="ui-field-hint">Optional product-specific care. Store policies remain in Store settings.</span>
                      @if (basicForm.controls.careInstructions.hasError('maxlength')) {
                        <span class="ui-field-error">Use 1000 characters or fewer.</span>
                      }
                    </label>

                    <label class="ui-field">
                      <span>Product disclaimer</span>
                      <textarea rows="3" formControlName="productDisclaimer" maxlength="1000"></textarea>
                      <span class="ui-field-hint">Optional buyer-facing context such as colour, sizing, or material variation.</span>
                      @if (basicForm.controls.productDisclaimer.hasError('maxlength')) {
                        <span class="ui-field-error">Use 1000 characters or fewer.</span>
                      }
                    </label>
                  </div>

                  <button data-ui-button="primary" type="submit" [disabled]="!isListingEditable() || isSaving()">{{ isRevisionMode() ? 'Save revision' : 'Save draft' }}</button>
                </form>
              }
              @case (1) {
                <form [formGroup]="basicForm" (ngSubmit)="saveDraft()" class="wizard-form" novalidate>
                  <h2>Category</h2>
                  <p class="form-helper">Choose the closest active catalog category. Required attributes are marked and must be completed before review submission.</p>
                  <label class="ui-field">
                    <span>Category</span>
                    <select formControlName="categoryId" (change)="onCategoryChanged($any($event.target).value)">
                      @for (category of categories(); track category.categoryId) {
                        <option [value]="category.categoryId">{{ categoryLabel(category) }}</option>
                      }
                    </select>
                    @if (basicForm.controls.categoryId.hasError('required')) {
                      <span class="ui-field-error">Category is required.</span>
                    }
                  </label>

                  @if (selectedCategory(); as category) {
                    <div class="product-editor-context">
                      <strong>{{ category.name }}</strong>
                      <span>{{ category.attributes.length }} attribute{{ category.attributes.length === 1 ? '' : 's' }} configured for this category.</span>
                    </div>
                  } @else {
                    <div class="product-editor-context">
                      <strong>No category selected</strong>
                      <span>Select a category to show the attributes buyers and reviewers expect.</span>
                    </div>
                  }

                  <div [formGroup]="attributeForm" class="dynamic-attributes">
                    @for (attribute of selectedCategory()?.attributes ?? []; track attribute.attributeId) {
                      <label class="ui-field">
                        <span>{{ attribute.name }}{{ attribute.isRequired ? ' *' : '' }}</span>
                        @switch (attribute.dataType) {
                          @case ('Select') {
                            <select [formControlName]="attribute.key">
                              @for (value of attribute.allowedValues; track value) {
                                <option [value]="value">{{ value }}</option>
                              }
                            </select>
                          }
                          @case ('MultiSelect') {
                            <select multiple [formControlName]="attribute.key">
                              @for (value of attribute.allowedValues; track value) {
                                <option [value]="value">{{ value }}</option>
                              }
                            </select>
                          }
                          @case ('Boolean') {
                            <select [formControlName]="attribute.key">
                              <option [ngValue]="true">Yes</option>
                              <option [ngValue]="false">No</option>
                            </select>
                          }
                          @case ('Number') {
                            <input type="number" step="1" [formControlName]="attribute.key" />
                          }
                          @case ('Decimal') {
                            <input type="number" step="0.01" [formControlName]="attribute.key" />
                          }
                          @case ('Date') {
                            <input type="date" [formControlName]="attribute.key" />
                          }
                          @default {
                            <input [formControlName]="attribute.key" />
                          }
                        }
                        @if (attributeForm.controls[attribute.key].hasError('required')) {
                          <span class="ui-field-error">{{ attribute.name }} is required.</span>
                        }
                        <span class="ui-field-hint">{{ attribute.isRequired ? 'Required' : 'Optional' }} - {{ attribute.dataType }}</span>
                      </label>
                    } @empty {
                      <p class="form-helper">This category does not currently require additional attributes.</p>
                    }
                  </div>

                  <div class="product-editor-context product-editor-context--stacked">
                    <strong>Buyer-facing attribute preview</strong>
                    @if (attributePreviewEntries().length > 0) {
                      <div class="settings-summary-list">
                        @for (entry of attributePreviewEntries(); track entry.label) {
                          <div>
                            <span>{{ entry.label }}</span>
                            <strong>{{ entry.value }}</strong>
                          </div>
                        }
                      </div>
                    } @else {
                      <span>Complete category attributes to preview the details buyers and reviewers will see.</span>
                    }
                  </div>

                  <button data-ui-button="primary" type="submit" [disabled]="!isListingEditable() || isSaving()">{{ isRevisionMode() ? 'Save revision category' : 'Save category' }}</button>
                </form>
              }
              @case (2) {
                <form [formGroup]="imageForm" (ngSubmit)="addImage()" class="wizard-form" novalidate>
                  <h2>Images</h2>
                  <p class="form-helper">{{ isRevisionMode() ? 'Upload proposed revision images. Buyers will keep seeing the current published gallery until admin approval.' : 'Upload product images before review. The first image buyers see should be marked primary, with clear alt text for accessibility and moderation.' }}</p>
                  <label class="upload-dropzone">
                    <span>{{ selectedImageFile()?.name ?? 'Choose JPEG, PNG, or WebP image' }}</span>
                    <small>Maximum 5 MB. Local development storage is served by the API.</small>
                    <input type="file" accept="image/jpeg,image/png,image/webp" (change)="onImageFileSelected($event)" />
                  </label>
                  <label class="ui-field">
                    <span>Alt text</span>
                    <input formControlName="altText" />
                  </label>
                  <div class="form-grid">
                    <label class="ui-field">
                      <span>Sort order</span>
                      <input type="number" min="0" formControlName="sortOrder" />
                    </label>
                    <label class="ui-field">
                      <span>Primary image</span>
                      <select formControlName="isPrimary">
                        <option [ngValue]="true">Yes</option>
                        <option [ngValue]="false">No</option>
                      </select>
                    </label>
                  </div>
                  <button data-ui-button="primary" type="submit" [disabled]="!isListingEditable() || !selectedImageFile() || isSaving()">Upload image</button>
                </form>

                <div class="product-image-gallery">
                  @for (image of displayImages(); track imageKey(image)) {
                    <article class="product-image-card" [class.primary]="image.isPrimary">
                      <div class="product-image-thumb">
                        @if (image.url) {
                          <img [src]="image.url" [alt]="image.altText ?? product()?.title ?? 'Product image'" />
                        } @else {
                          <app-product-visual-fallback [title]="image.storageKey" label="Image" tone="dress" />
                        }
                      </div>
                      <div>
                        <span class="status-pill">{{ image.isPrimary ? 'Primary' : 'Image' }}</span>
                        <h2>{{ image.altText ?? image.storageKey }}</h2>
                        <p>{{ image.storageKey }}</p>
                        <small>{{ image.url }}</small>
                      </div>
                      <div class="buyer-action-row">
                        <button data-ui-button="secondary" type="button" [disabled]="!isListingEditable()" (click)="editImage(image)">Edit</button>
                        @if (!image.isPrimary) {
                          <button data-ui-button="secondary" type="button" [disabled]="!isListingEditable()" (click)="makeImagePrimary(image)">Make primary</button>
                        }
                        <button data-ui-button="secondary" type="button" [disabled]="!isListingEditable()" (click)="removeImage(imageKey(image))">Remove</button>
                      </div>
                    </article>
                  } @empty {
                    <div class="product-editor-context">
                      <strong>No images attached</strong>
                      <span>Add at least one product image before submitting for review.</span>
                    </div>
                  }
                </div>

                @if (editingImage(); as image) {
                  <form [formGroup]="imageEditForm" (ngSubmit)="saveImageMetadata()" class="wizard-form product-editor-edit-panel" novalidate>
                    <h3>Edit image metadata</h3>
                    <p class="form-helper">{{ image.storageKey }} - URL and storage key are managed outside this editor.</p>
                    <label class="ui-field">
                      <span>Alt text</span>
                      <input formControlName="altText" />
                    </label>
                    <div class="form-grid">
                      <label class="ui-field">
                        <span>Sort order</span>
                        <input type="number" min="0" formControlName="sortOrder" />
                      </label>
                      <label class="ui-field">
                        <span>Primary image</span>
                        <select formControlName="isPrimary">
                          <option [ngValue]="true">Yes</option>
                          <option [ngValue]="false">No</option>
                        </select>
                      </label>
                    </div>
                    <div class="buyer-action-row">
                      <button data-ui-button="primary" type="submit" [disabled]="!isListingEditable() || isSaving()">Save image</button>
                      <button data-ui-button="secondary" type="button" (click)="clearImageEdit()">Cancel</button>
                    </div>
                  </form>
                }
              }
              @case (3) {
                <form [formGroup]="variantForm" (ngSubmit)="saveVariant()" class="wizard-form" novalidate>
                  <div class="admin-section-heading">
                    <div>
                      <h2>{{ editingVariantId() ? 'Edit variant' : 'Variants and stock' }}</h2>
                      <p class="form-helper">Use variants for sellable size, colour, price, and stock combinations. Published stock changes belong in Inventory.</p>
                    </div>
                    @if (editingVariantId()) {
                      <button data-ui-button="secondary" type="button" (click)="startNewVariant()">New variant</button>
                    }
                  </div>
                  <div class="form-grid">
                    <label class="ui-field">
                      <span>SKU</span>
                      <input formControlName="sku" />
                    </label>
                    <label class="ui-field">
                      <span>Status</span>
                      <select formControlName="status">
                        <option value="Active">Active</option>
                        <option value="Inactive">Inactive</option>
                        <option value="OutOfStock">Out of stock</option>
                      </select>
                    </label>
                  </div>
                  <div class="form-grid">
                    <label class="ui-field">
                      <span>Size</span>
                      <input formControlName="size" />
                    </label>
                    <label class="ui-field">
                      <span>Colour</span>
                      <input formControlName="colour" />
                    </label>
                  </div>
                  <div class="form-grid">
                    <label class="ui-field">
                      <span>Price</span>
                      <input type="number" step="0.01" formControlName="price" />
                    </label>
                    <label class="ui-field">
                      <span>Compare-at price</span>
                      <input type="number" step="0.01" formControlName="compareAtPrice" />
                    </label>
                  </div>
                  <div class="form-grid">
                    <label class="ui-field">
                      <span>Stock quantity</span>
                      <input type="number" min="0" formControlName="stockQuantity" />
                    </label>
                    <label class="ui-field">
                      <span>Reserved quantity</span>
                      <input type="number" min="0" formControlName="reservedQuantity" />
                    </label>
                  </div>
                  <label class="ui-field">
                    <span>Barcode</span>
                    <input formControlName="barcode" />
                    <span class="ui-field-hint">Optional. Use the printed barcode value sellers scan in Inventory.</span>
                  </label>
                  <button data-ui-button="primary" type="submit" [disabled]="!isProductEditable() || isSaving()">
                    {{ editingVariantId() ? 'Save variant' : 'Add variant' }}
                  </button>
                </form>

                <div class="product-variant-editor-grid">
                  @for (variant of product()?.variants ?? []; track variant.variantId) {
                    <article class="product-variant-editor-card">
                      <div class="admin-section-heading">
                        <div>
                          <span class="status-pill">{{ variant.status }}</span>
                          <h2>{{ variant.sku }}</h2>
                        </div>
                        <strong>{{ renderCurrency(variant.price) }}</strong>
                      </div>
                      <dl class="seller-facts">
                        <div><dt>Size</dt><dd>{{ variant.size }}</dd></div>
                        <div><dt>Colour</dt><dd>{{ variant.colour }}</dd></div>
                        <div><dt>Stock</dt><dd>{{ variant.stockQuantity }}</dd></div>
                        <div><dt>Reserved</dt><dd>{{ variant.reservedQuantity }}</dd></div>
                        <div><dt>Available</dt><dd>{{ variant.availableQuantity }}</dd></div>
                      </dl>
                      @if (variant.availableQuantity <= 0) {
                        <p class="auth-alert error">Out of stock for buyers.</p>
                      } @else if (variant.availableQuantity <= 5) {
                        <p class="auth-alert">Low stock. Check inventory before promoting this listing.</p>
                      }
                      <div class="buyer-action-row">
                        <button data-ui-button="secondary" type="button" [disabled]="!isProductEditable()" (click)="editVariant(variant)">Edit</button>
                        <button data-ui-button="secondary" type="button" [disabled]="!isProductEditable()" (click)="removeVariant(variant.variantId)">Remove</button>
                        @if (isRevisionMode()) {
                          <button data-ui-button="secondary" type="button" [disabled]="!variantRevision()?.canEdit" (click)="stageVariantRevisionUpdate(variant)">Stage update</button>
                          <button data-ui-button="secondary" type="button" [disabled]="!variantRevision()?.canEdit" (click)="stageVariantRevisionDeactivation(variant)">Stage deactivation</button>
                        }
                      </div>
                    </article>
                  } @empty {
                    <div class="product-editor-context">
                      <strong>No variants configured</strong>
                      <span>Add at least one active variant with available stock before review submission.</span>
                    </div>
                  }
                </div>

                @if (isRevisionMode() && variantRevision()) {
                  <article class="route-card admin-detail-card">
                    <div class="admin-section-heading">
                      <div>
                        <span class="eyebrow">Published listing workflow</span>
                        <h2>Variant and pricing revision</h2>
                        <p class="form-helper">Stage SKU, size, colour, price, compare-at price, barcode, additions, and deactivations without changing the live listing until admin approval. Existing stock stays in Inventory.</p>
                      </div>
                      <span class="status-pill">{{ variantRevision()!.status }}</span>
                    </div>

                    @if (variantRevision()!.rejectionReason) {
                      <p class="auth-alert error">Rejected: {{ variantRevision()!.rejectionReason }}</p>
                    }

                    <div class="product-editor-context variant-revision-import-panel">
                      <div class="admin-section-heading">
                        <div>
                          <span class="eyebrow">CSV staging</span>
                          <h3>Bulk variant import</h3>
                          <p class="form-helper">Export the current variants, edit the template, preview validation, then replace the current staged rows. This does not submit the revision for admin review.</p>
                        </div>
                      </div>
                      <div class="buyer-action-row">
                        <button data-ui-button="secondary" type="button" [disabled]="isVariantRevisionDownloading()" (click)="downloadVariantRevisionExport()">Export current variants</button>
                        <button data-ui-button="secondary" type="button" [disabled]="isVariantRevisionDownloading()" (click)="downloadVariantRevisionTemplate()">Download template</button>
                      </div>
                      <div class="form-grid">
                        <label class="support-upload-control">
                          <span>Variant revision CSV</span>
                          <input type="file" accept=".csv,text/csv" [disabled]="!variantRevision()!.canEdit || isVariantRevisionImporting()" (change)="onVariantRevisionImportFileSelected($event)" />
                        </label>
                        <div class="product-editor-context">
                          <strong>{{ selectedVariantRevisionImportFile()?.name ?? 'No file selected' }}</strong>
                          <span>Columns: operation, sourceVariantId, sku, size, colour, price, compareAtPrice, initialStockQuantity, barcode.</span>
                        </div>
                      </div>
                      <div class="buyer-action-row">
                        <button data-ui-button="primary" type="button" [disabled]="!variantRevision()!.canEdit || !selectedVariantRevisionImportFile() || isVariantRevisionImporting()" (click)="previewVariantRevisionImport()">Preview CSV</button>
                        <button data-ui-button="secondary" type="button" [disabled]="!variantRevision()!.canEdit || !variantRevisionImportPreview() || variantRevisionImportPreview()!.errorRows > 0 || variantRevisionImportPreview()!.changedRows === 0 || isVariantRevisionImporting()" (click)="bulkStageVariantRevisionImport()">Bulk-stage changed rows</button>
                      </div>

                      @if (variantRevisionImportPreview(); as preview) {
                        <div class="seller-metric-grid">
                          <article><span>Total rows</span><strong>{{ preview.totalRows }}</strong></article>
                          <article><span>Changed</span><strong>{{ preview.changedRows }}</strong></article>
                          <article><span>Unchanged</span><strong>{{ preview.unchangedRows }}</strong></article>
                          <article><span>Errors</span><strong>{{ preview.errorRows }}</strong></article>
                        </div>

                        <div class="admin-table" role="table" aria-label="Variant revision import preview">
                          <div class="admin-table-row heading" role="row">
                            <span role="columnheader">Row</span>
                            <span role="columnheader">Operation</span>
                            <span role="columnheader">Current</span>
                            <span role="columnheader">Proposed</span>
                            <span role="columnheader">Status</span>
                          </div>
                          @for (row of preview.rows; track row.rowNumber) {
                            <div class="admin-table-row" role="row">
                              <span role="cell"><strong>#{{ row.rowNumber }}</strong></span>
                              <span role="cell"><strong>{{ row.operation }}</strong><small>{{ row.sourceVariantId || 'New variant' }}</small></span>
                              <span role="cell">
                                <strong>{{ row.currentSku || 'No current variant' }}</strong>
                                <small>{{ row.currentSize || '-' }} / {{ row.currentColour || '-' }} / {{ row.currentPrice !== null ? renderCurrency(row.currentPrice) : '-' }}</small>
                              </span>
                              <span role="cell">
                                <strong>{{ row.proposedSku || '-' }}</strong>
                                <small>{{ row.proposedSize || '-' }} / {{ row.proposedColour || '-' }} / {{ row.proposedPrice !== null ? renderCurrency(row.proposedPrice) : '-' }}</small>
                              </span>
                              <span role="cell">
                                <span class="status-pill">{{ row.rowStatus }}</span>
                                @if (row.validationMessages.length > 0) {
                                  <small>{{ row.validationMessages.join(' ') }}</small>
                                } @else if (row.proposedBarcode) {
                                  <small>Barcode {{ row.proposedBarcode }}</small>
                                }
                              </span>
                            </div>
                          }
                        </div>

                        @if (preview.proposedFinalVariants.length > 0) {
                          <div class="admin-table" role="table" aria-label="Proposed final variant set">
                            <div class="admin-table-row heading" role="row">
                              <span role="columnheader">Final variant</span>
                              <span role="columnheader">Price</span>
                              <span role="columnheader">Stock context</span>
                              <span role="columnheader">Change</span>
                            </div>
                            @for (variant of preview.proposedFinalVariants; track variant.sourceVariantId || variant.sku) {
                              <div class="admin-table-row" role="row">
                                <span role="cell"><strong>{{ variant.sku }}</strong><small>{{ variant.size }} / {{ variant.colour }}</small></span>
                                <span role="cell"><strong>{{ renderCurrency(variant.price) }}</strong><small>{{ variant.compareAtPrice ? 'Was ' + renderCurrency(variant.compareAtPrice) : 'No compare price' }}</small></span>
                                <span role="cell"><strong>{{ variant.status }}</strong><small>{{ variant.availableQuantity }} available, {{ variant.reservedQuantity }} reserved</small></span>
                                <span role="cell"><span class="status-pill">{{ variant.changeType }}</span></span>
                              </div>
                            }
                          </div>
                        }
                      }
                    </div>

                    <form [formGroup]="variantRevisionForm" (ngSubmit)="addVariantRevisionItem()" class="wizard-form" novalidate>
                      <label class="ui-field">
                        <span>Seller reason</span>
                        <textarea rows="2" formControlName="sellerReason" placeholder="Explain why this variant or pricing change is needed."></textarea>
                      </label>

                      <div class="form-grid">
                        <label class="ui-field">
                          <span>Revision action</span>
                          <select formControlName="operation">
                            <option value="Add">Add new variant</option>
                            <option value="Update">Update live variant</option>
                          </select>
                        </label>
                        <label class="ui-field">
                          <span>Source variant</span>
                          <select formControlName="sourceVariantId">
                            <option [ngValue]="null">New variant</option>
                            @for (variant of product()?.variants ?? []; track variant.variantId) {
                              <option [value]="variant.variantId">{{ variant.sku }} / {{ variant.size }} / {{ variant.colour }}</option>
                            }
                          </select>
                        </label>
                      </div>

                      <div class="form-grid">
                        <label class="ui-field">
                          <span>SKU</span>
                          <input formControlName="sku" />
                        </label>
                        <label class="ui-field">
                          <span>Barcode</span>
                          <input formControlName="barcode" />
                          <span class="ui-field-hint">Optional scanner value for SKU/barcode lookup after approval.</span>
                        </label>
                      </div>
                      <div class="form-grid">
                        <label class="ui-field">
                          <span>Size</span>
                          <input formControlName="size" />
                        </label>
                        <label class="ui-field">
                          <span>Colour</span>
                          <input formControlName="colour" />
                        </label>
                      </div>
                      <div class="form-grid">
                        <label class="ui-field">
                          <span>Price</span>
                          <input type="number" step="0.01" formControlName="price" />
                        </label>
                        <label class="ui-field">
                          <span>Compare-at price</span>
                          <input type="number" step="0.01" formControlName="compareAtPrice" />
                        </label>
                      </div>
                      <label class="ui-field">
                        <span>Initial stock for new variant</span>
                        <input type="number" min="0" formControlName="initialStockQuantity" />
                      </label>

                      <div class="buyer-action-row">
                        <button data-ui-button="primary" type="submit" [disabled]="!variantRevision()!.canEdit || isSaving()">Stage change</button>
                        <button data-ui-button="secondary" type="button" (click)="startVariantRevisionAdd()">New variant</button>
                        <button data-ui-button="secondary" type="button" [disabled]="!variantRevision()!.canEdit || variantRevision()!.items.length === 0 || isSaving()" (click)="submitVariantRevision()">Submit variant revision</button>
                        <button data-ui-button="secondary" type="button" [disabled]="isSaving()" (click)="cancelVariantRevision()">Cancel revision</button>
                      </div>
                    </form>

                    <div class="admin-table" role="table" aria-label="Staged variant changes">
                      <div class="admin-table-row heading" role="row">
                        <span role="columnheader">Change</span>
                        <span role="columnheader">Variant</span>
                        <span role="columnheader">Price</span>
                        <span role="columnheader">Action</span>
                      </div>
                      @for (item of variantRevision()!.items; track item.revisionItemId) {
                        <div class="admin-table-row" role="row">
                          <span role="cell"><strong>{{ item.operation }}</strong><small>{{ item.proposedStatus }}</small></span>
                          <span role="cell"><strong>{{ item.sku }}</strong><small>{{ item.size }} / {{ item.colour }}</small></span>
                          <span role="cell"><strong>{{ renderCurrency(item.price) }}</strong><small>{{ item.compareAtPrice ? 'Was ' + renderCurrency(item.compareAtPrice) : 'No compare price' }}</small></span>
                          <span role="cell"><button data-ui-button="secondary" type="button" [disabled]="!variantRevision()!.canEdit || isSaving()" (click)="removeVariantRevisionItem(item)">Remove</button></span>
                        </div>
                      } @empty {
                        <div class="product-editor-context">
                          <strong>No staged variant changes</strong>
                          <span>Use Stage update or add a new variant before submitting this revision.</span>
                        </div>
                      }
                    </div>
                  </article>
                }
              }
              @case (4) {
                <form [formGroup]="shippingForm" class="wizard-form" novalidate>
                  <h2>Shipping and returns</h2>
                  <p class="form-helper">Product-level shipping notes remain internal planning context. Buyer-facing return, exchange, fulfilment, support, care, and disclaimer policies are managed in Store settings.</p>
                  <label class="ui-field">
                    <span>Shipping notes</span>
                    <textarea rows="4" formControlName="shippingNotes" placeholder="Internal planning notes only"></textarea>
                  </label>
                  <label class="ui-field">
                    <span>Return notes</span>
                    <textarea rows="4" formControlName="returnNotes" placeholder="Internal planning notes only"></textarea>
                  </label>
                </form>
              }
              @case (5) {
                <div class="review-panel">
                  <h2>Review and submit</h2>
                  <p class="form-helper">Check the buyer-facing preview and readiness list before sending the product to marketplace review.</p>
                  <div class="product-editor-review-layout">
                    <article class="product-editor-preview-card">
                      <div class="product-editor-preview-visual">
                        @if (primaryImage(); as image) {
                          <img [src]="image.url" [alt]="image.altText ?? product()?.title ?? 'Product image'" />
                        } @else {
                          <app-product-visual-fallback [title]="basicForm.controls.title.value || 'Draft listing'" label="Preview" tone="dress" />
                        }
                      </div>
                      <div>
                        <span class="status-pill">{{ product()?.status ?? 'Draft' }}</span>
                        <h3>{{ basicForm.controls.title.value || 'Untitled product' }}</h3>
                        <p>{{ basicForm.controls.shortDescription.value || 'Short description will appear here.' }}</p>
                        <strong>{{ previewPriceLabel() }}</strong>
                      </div>
                    </article>
                    <div class="review-grid">
                      @for (item of reviewItems(); track item.label) {
                        <article class="route-card compact-card">
                          <span class="status-pill">{{ item.complete ? 'Complete' : 'Missing' }}</span>
                          <h2>{{ item.label }}</h2>
                          <p>{{ item.summary }}</p>
                        </article>
                      }
                    </div>
                  </div>
                  @if (!canSubmitReview()) {
                    <p class="auth-alert">Complete details, category attributes, one image, and one active in-stock variant before review submission.</p>
                  }
                  <button data-ui-button="primary" type="button" [disabled]="!isListingEditable() || !canSubmitReview() || isSaving()" (click)="submitForReview()">
                    {{ isRevisionMode() ? 'Submit revision for review' : 'Submit for review' }}
                  </button>
                </div>
              }
            }
          </div>
        </div>
      }
    </section>
  `
})
export class SellerProductFormPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly productService = inject(SellerProductService);

  protected readonly categories = signal<SellerCatalogCategoryResponse[]>([]);
  protected readonly product = signal<SellerProductDetailResponse | null>(null);
  protected readonly revision = signal<SellerProductRevisionResponse | null>(null);
  protected readonly variantRevision = signal<SellerProductVariantRevisionResponse | null>(null);
  protected readonly variantRevisionImportPreview = signal<SellerProductVariantRevisionBulkImportResponse | null>(null);
  protected readonly selectedVariantRevisionImportFile = signal<File | null>(null);
  protected readonly selectedCategoryId = signal<string | null>(null);
  protected readonly editingImageId = signal<string | null>(null);
  protected readonly editingVariantId = signal<string | null>(null);
  protected readonly selectedImageFile = signal<File | null>(null);
  protected readonly currentStep = signal<ProductStep>(0);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly isAiGenerating = signal(false);
  protected readonly isAiApplying = signal(false);
  protected readonly isVariantRevisionImporting = signal(false);
  protected readonly isVariantRevisionDownloading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly aiErrorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly aiSuggestion = signal<SellerAiSuggestionResponse | null>(null);

  protected readonly steps: readonly { index: ProductStep; label: string }[] = [
    { index: 0, label: 'Details' },
    { index: 1, label: 'Category' },
    { index: 2, label: 'Images' },
    { index: 3, label: 'Variants' },
    { index: 4, label: 'Shipping' },
    { index: 5, label: 'Review' }
  ];

  protected readonly selectedCategory = computed(() =>
    this.categories().find(category => category.categoryId === this.selectedCategoryId()) ?? null);

  protected readonly primaryImage = computed(() => {
    const images = this.displayImages();
    return images.find(image => image.isPrimary) ?? images[0] ?? null;
  });

  protected readonly editingImage = computed(() =>
    this.displayImages().find(image => this.imageKey(image) === this.editingImageId()) ?? null);

  protected readonly publicPreviewUrl = computed(() => {
    const product = this.product();
    return product?.status === 'Published' && product.slug
      ? `/product/${product.slug}`
      : null;
  });

  protected readonly moderationTimeline = computed(() => [
    ...(this.product()?.moderationEvents ?? []),
    ...(this.revision()?.moderationEvents ?? []),
    ...(this.variantRevision()?.moderationEvents ?? [])
  ].sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc)));

  protected readonly basicForm = new FormGroup({
    categoryId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    title: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    slug: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)] }),
    shortDescription: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    fullDescription: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    seoTitle: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(70)] }),
    seoDescription: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(170)] }),
    merchandisingLabel: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(60)] }),
    careInstructions: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    productDisclaimer: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
  });

  protected readonly attributeForm = new FormRecord<FormControl<unknown>>({});

  protected readonly imageForm = new FormGroup({
    altText: new FormControl('', { nonNullable: true }),
    sortOrder: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    isPrimary: new FormControl(false, { nonNullable: true })
  });

  protected readonly imageEditForm = new FormGroup({
    altText: new FormControl('', { nonNullable: true }),
    sortOrder: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    isPrimary: new FormControl(false, { nonNullable: true })
  });

  protected readonly variantForm = new FormGroup({
    sku: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    size: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    colour: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    price: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0.01)] }),
    compareAtPrice: new FormControl<number | null>(null),
    stockQuantity: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    reservedQuantity: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    status: new FormControl<'Active' | 'Inactive' | 'OutOfStock'>('Active', { nonNullable: true }),
    barcode: new FormControl('', { nonNullable: true })
  });

  protected readonly variantRevisionForm = new FormGroup({
    sellerReason: new FormControl('', { nonNullable: true }),
    operation: new FormControl<'Add' | 'Update'>('Add', { nonNullable: true }),
    sourceVariantId: new FormControl<string | null>(null),
    sku: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    size: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    colour: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    price: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0.01)] }),
    compareAtPrice: new FormControl<number | null>(null),
    initialStockQuantity: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    barcode: new FormControl('', { nonNullable: true })
  });

  protected readonly shippingForm = new FormGroup({
    shippingNotes: new FormControl('', { nonNullable: true }),
    returnNotes: new FormControl('', { nonNullable: true })
  });

  protected readonly aiForm = new FormGroup({
    sellerNotes: new FormControl('', { nonNullable: true }),
    productTypeHint: new FormControl('', { nonNullable: true })
  });

  protected readonly aiApplyForm = new FormGroup({
    title: new FormControl(false, { nonNullable: true }),
    shortDescription: new FormControl(false, { nonNullable: true }),
    fullDescription: new FormControl(false, { nonNullable: true }),
    category: new FormControl(false, { nonNullable: true }),
    attributes: new FormControl(false, { nonNullable: true }),
    tags: new FormControl(false, { nonNullable: true }),
    imageAltText: new FormControl(false, { nonNullable: true }),
    confirmRiskFlags: new FormControl(false, { nonNullable: true })
  });

  protected readonly aiEditForm = new FormGroup({
    title: new FormControl('', { nonNullable: true }),
    shortDescription: new FormControl('', { nonNullable: true }),
    fullDescription: new FormControl('', { nonNullable: true }),
    suggestedCategoryId: new FormControl('', { nonNullable: true }),
    tags: new FormControl('', { nonNullable: true })
  });

  protected readonly aiAttributeEditForm = new FormRecord<FormControl<string>>({});
  protected readonly aiImageAltTextForm = new FormRecord<FormControl<string>>({});

  async ngOnInit(): Promise<void> {
    await this.loadEditor();
  }

  protected onCategoryChanged(categoryId: string): void {
    this.selectedCategoryId.set(categoryId);
    this.rebuildAttributeControls({});
  }

  protected categoryLabel(category: SellerCatalogCategoryResponse): string {
    const parent = this.categories().find(item => item.categoryId === category.parentCategoryId);
    return parent ? `${parent.name} / ${category.name}` : category.name;
  }

  protected async saveDraft(): Promise<SellerProductDetailResponse | null> {
    if (!this.isListingEditable()) {
      return null;
    }

    if (!this.ensureValid(this.basicForm) || !this.ensureValid(this.attributeForm)) {
      return null;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const request = this.createProductRequest();
      const existing = this.product();
      if (existing && this.isRevisionMode()) {
        const revision = await this.productService.updateRevision(existing.productId, {
          ...request,
          tags: this.revision()?.tags ?? existing.tags
        } satisfies UpsertSellerProductRevisionRequest);
        this.setRevision(revision);
        this.successMessage.set('Listing revision saved.');
        return existing;
      }

      const saved = existing
        ? await this.productService.updateProduct(existing.productId, request)
        : await this.productService.createProduct(request);

      this.setProduct(saved);
      this.successMessage.set('Product draft saved.');

      if (!existing) {
        await this.router.navigate(['/products', saved.productId, 'edit'], { replaceUrl: true });
      }

      return saved;
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      return null;
    } finally {
      this.isSaving.set(false);
    }
  }

  protected async addImage(): Promise<void> {
    if (!this.isListingEditable()) {
      return;
    }

    const file = this.selectedImageFile();
    if (!file) {
      this.errorMessage.set('Choose an image file to upload.');
      return;
    }

    if (!this.ensureValid(this.imageForm)) {
      return;
    }

    const saved = await this.ensureProductSaved();
    if (!saved) {
      return;
    }

    const value = this.imageForm.getRawValue();
    if (this.isRevisionMode()) {
      await this.runRevisionAction(
        () => this.productService.uploadRevisionImage(
          saved.productId,
          file,
          emptyToNull(value.altText),
          Number(value.sortOrder),
          value.isPrimary),
        'Revision image uploaded.');
    } else {
      await this.runProductAction(
        () => this.productService.uploadImage(
          saved.productId,
          file,
          emptyToNull(value.altText),
          Number(value.sortOrder),
          value.isPrimary),
        'Image uploaded.');
    }

    this.selectedImageFile.set(null);
    this.imageForm.reset({ altText: '', sortOrder: 0, isPrimary: false });
  }

  protected editImage(image: ProductEditorImage): void {
    this.editingImageId.set(this.imageKey(image));
    this.imageEditForm.reset({
      altText: image.altText ?? '',
      sortOrder: image.sortOrder,
      isPrimary: image.isPrimary
    });
  }

  protected clearImageEdit(): void {
    this.editingImageId.set(null);
    this.imageEditForm.reset({ altText: '', sortOrder: 0, isPrimary: false });
  }

  protected async makeImagePrimary(image: ProductEditorImage): Promise<void> {
    this.editImage(image);
    this.imageEditForm.patchValue({ isPrimary: true });
    await this.saveImageMetadata();
  }

  protected async saveImageMetadata(): Promise<void> {
    const product = this.product();
    const image = this.editingImage();
    if (!product || !image || !this.isListingEditable() || !this.ensureValid(this.imageEditForm)) {
      return;
    }

    const value = this.imageEditForm.getRawValue();
    const request = {
      altText: emptyToNull(value.altText),
      sortOrder: Number(value.sortOrder),
      isPrimary: value.isPrimary
    };
    if (this.isRevisionMode()) {
      await this.runRevisionAction(
        () => this.productService.updateRevisionImage(product.productId, this.imageKey(image), request),
        'Revision image updated.');
    } else {
      await this.runProductAction(
        () => this.productService.updateImage(product.productId, this.imageKey(image), request),
        'Image updated.');
    }
    this.clearImageEdit();
  }

  protected async removeImage(imageId: string): Promise<void> {
    const product = this.product();
    if (!product || !this.isListingEditable()) {
      return;
    }

    if (this.isRevisionMode()) {
      await this.runRevisionAction(
        () => this.productService.deleteRevisionImage(product.productId, imageId),
        'Revision image removed.');
    } else {
      await this.runProductAction(
        () => this.productService.deleteImage(product.productId, imageId),
        'Image removed.');
    }
  }

  protected async saveVariant(): Promise<void> {
    if (this.editingVariantId()) {
      await this.updateVariant();
      return;
    }

    await this.addVariant();
  }

  protected async addVariant(): Promise<void> {
    if (!this.isProductEditable()) {
      return;
    }

    if (!this.ensureValid(this.variantForm)) {
      return;
    }

    const saved = await this.ensureProductSaved();
    if (!saved) {
      return;
    }

    await this.runProductAction(
      () => this.productService.addVariant(saved.productId, this.createVariantRequest()),
      'Variant added.');
    this.startNewVariant();
  }

  protected editVariant(variant: SellerProductVariantResponse): void {
    this.editingVariantId.set(variant.variantId);
    this.variantForm.reset({
      sku: variant.sku,
      size: variant.size,
      colour: variant.colour,
      price: variant.price,
      compareAtPrice: variant.compareAtPrice,
      stockQuantity: variant.stockQuantity,
      reservedQuantity: variant.reservedQuantity,
      status: variant.status,
      barcode: variant.barcode ?? ''
    });
  }

  protected startNewVariant(): void {
    this.editingVariantId.set(null);
    this.variantForm.reset({
      sku: '',
      size: '',
      colour: '',
      price: 0,
      compareAtPrice: null,
      stockQuantity: 0,
      reservedQuantity: 0,
      status: 'Active',
      barcode: ''
    });
  }

  protected async updateVariant(): Promise<void> {
    const product = this.product();
    const variantId = this.editingVariantId();
    if (!product || !variantId || !this.isProductEditable() || !this.ensureValid(this.variantForm)) {
      return;
    }

    await this.runProductAction(
      () => this.productService.updateVariant(product.productId, variantId, this.createVariantRequest()),
      'Variant updated.');
    this.startNewVariant();
  }

  protected async removeVariant(variantId: string): Promise<void> {
    const product = this.product();
    if (!product || !this.isProductEditable()) {
      return;
    }

    await this.runProductAction(
      () => this.productService.deleteVariant(product.productId, variantId),
      'Variant removed.');
  }

  protected startVariantRevisionAdd(): void {
    this.variantRevisionForm.patchValue({
      operation: 'Add',
      sourceVariantId: null,
      sku: '',
      size: '',
      colour: '',
      price: 0,
      compareAtPrice: null,
      initialStockQuantity: 0,
      barcode: ''
    });
  }

  protected stageVariantRevisionUpdate(variant: SellerProductVariantResponse): void {
    this.variantRevisionForm.patchValue({
      operation: 'Update',
      sourceVariantId: variant.variantId,
      sku: variant.sku,
      size: variant.size,
      colour: variant.colour,
      price: variant.price,
      compareAtPrice: variant.compareAtPrice,
      initialStockQuantity: 0,
      barcode: variant.barcode ?? ''
    });
  }

  protected async addVariantRevisionItem(): Promise<void> {
    const product = this.product();
    const revision = this.variantRevision();
    if (!product || !revision?.canEdit || !this.ensureValid(this.variantRevisionForm)) {
      return;
    }

    const formValue = this.variantRevisionForm.getRawValue();
    const nextItem: UpsertSellerProductVariantRevisionItemRequest = {
      operation: formValue.operation,
      sourceVariantId: formValue.operation === 'Add' ? null : formValue.sourceVariantId,
      sku: formValue.sku,
      size: formValue.size,
      colour: formValue.colour,
      price: formValue.price,
      compareAtPrice: formValue.compareAtPrice,
      initialStockQuantity: formValue.operation === 'Add' ? formValue.initialStockQuantity : null,
      barcode: formValue.barcode || null
    };
    const existingItems = this.variantRevisionItemsAsRequests()
      .filter(item => !sameVariantRevisionTarget(item, nextItem));

    await this.runVariantRevisionAction(
      () => this.productService.updateVariantRevision(product.productId, {
        sellerReason: formValue.sellerReason || null,
        items: [...existingItems, nextItem]
      }),
      'Variant revision staged.');
    this.variantRevisionImportPreview.set(null);
    this.startVariantRevisionAdd();
  }

  protected async stageVariantRevisionDeactivation(variant: SellerProductVariantResponse): Promise<void> {
    const product = this.product();
    const revision = this.variantRevision();
    if (!product || !revision?.canEdit) {
      return;
    }

    const existingItems = this.variantRevisionItemsAsRequests()
      .filter(item => item.sourceVariantId !== variant.variantId);
    await this.runVariantRevisionAction(
      () => this.productService.updateVariantRevision(product.productId, {
        sellerReason: this.variantRevisionForm.controls.sellerReason.value || null,
        items: [
          ...existingItems,
          {
            operation: 'Deactivate',
            sourceVariantId: variant.variantId,
            sku: null,
            size: null,
            colour: null,
            price: null,
            compareAtPrice: null,
            initialStockQuantity: null,
            barcode: null
          }
        ]
      }),
      'Variant deactivation staged.');
    this.variantRevisionImportPreview.set(null);
  }

  protected async removeVariantRevisionItem(item: SellerProductVariantRevisionItemResponse): Promise<void> {
    const product = this.product();
    const revision = this.variantRevision();
    if (!product || !revision?.canEdit) {
      return;
    }

    const target = revisionItemResponseAsRequest(item);
    await this.runVariantRevisionAction(
      () => this.productService.updateVariantRevision(product.productId, {
        sellerReason: this.variantRevisionForm.controls.sellerReason.value || null,
        items: this.variantRevisionItemsAsRequests()
          .filter(existingItem => !sameVariantRevisionTarget(existingItem, target))
      }),
      'Staged variant change removed.');
    this.variantRevisionImportPreview.set(null);
  }

  protected async submitVariantRevision(): Promise<void> {
    const product = this.product();
    if (!product || !this.variantRevision()?.canEdit) {
      return;
    }

    await this.runVariantRevisionAction(
      () => this.productService.submitVariantRevisionForReview(product.productId),
      'Variant revision submitted for review.');
  }

  protected async cancelVariantRevision(): Promise<void> {
    const product = this.product();
    if (!product) {
      return;
    }

    await this.runVariantRevisionAction(
      () => this.productService.cancelVariantRevision(product.productId),
      'Variant revision cancelled.');
    this.variantRevisionImportPreview.set(null);
  }

  protected onVariantRevisionImportFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedVariantRevisionImportFile.set(input.files?.[0] ?? null);
    this.variantRevisionImportPreview.set(null);
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  protected async downloadVariantRevisionExport(): Promise<void> {
    const product = this.product();
    if (!product) {
      return;
    }

    await this.downloadVariantRevisionCsv(
      () => this.productService.exportVariantRevisionCsv(product.productId),
      `${product.slug ?? product.productId}-variant-revision-export.csv`);
  }

  protected async downloadVariantRevisionTemplate(): Promise<void> {
    const product = this.product();
    if (!product) {
      return;
    }

    await this.downloadVariantRevisionCsv(
      () => this.productService.downloadVariantRevisionTemplate(product.productId),
      `${product.slug ?? product.productId}-variant-revision-template.csv`);
  }

  protected async previewVariantRevisionImport(): Promise<void> {
    const product = this.product();
    const file = this.selectedVariantRevisionImportFile();
    if (!product || !file || this.isVariantRevisionImporting()) {
      return;
    }

    this.isVariantRevisionImporting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const preview = await this.productService.previewVariantRevisionImport(product.productId, file);
      this.variantRevisionImportPreview.set(preview);
      this.successMessage.set(`Preview loaded: ${preview.changedRows} changed, ${preview.errorRows} errors.`);
    } catch (error) {
      this.variantRevisionImportPreview.set(null);
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isVariantRevisionImporting.set(false);
    }
  }

  protected async bulkStageVariantRevisionImport(): Promise<void> {
    const product = this.product();
    const preview = this.variantRevisionImportPreview();
    const revision = this.variantRevision();
    if (!product || !preview || !revision?.canEdit || preview.errorRows > 0 || preview.changedRows === 0 || this.isVariantRevisionImporting()) {
      return;
    }

    this.isVariantRevisionImporting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const response = await this.productService.bulkStageVariantRevision(product.productId, {
        sellerReason: this.variantRevisionForm.controls.sellerReason.value || null,
        items: preview.rows
          .filter(row => row.rowStatus === 'Changed')
          .map(row => ({
            operation: row.operation as 'Add' | 'Update' | 'Deactivate',
            sourceVariantId: row.operation === 'Add' ? null : row.sourceVariantId,
            sku: row.proposedSku,
            size: row.proposedSize,
            colour: row.proposedColour,
            price: row.proposedPrice,
            compareAtPrice: row.proposedCompareAtPrice,
            initialStockQuantity: row.operation === 'Add' ? row.proposedInitialStockQuantity : null,
            barcode: row.proposedBarcode
          }))
      });
      this.variantRevisionImportPreview.set(response);
      const refreshed = await this.productService.getVariantRevision(product.productId);
      this.variantRevision.set(refreshed);
      this.successMessage.set(`Bulk staged ${response.changedRows} variant changes. Submit the revision when ready.`);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isVariantRevisionImporting.set(false);
    }
  }

  protected async submitForReview(): Promise<void> {
    const saved = await this.ensureProductSaved();
    if (!saved || !this.isListingEditable() || !this.canSubmitReview()) {
      return;
    }

    if (this.isRevisionMode()) {
      await this.runRevisionAction(
        () => this.productService.submitRevisionForReview(saved.productId),
        'Listing revision submitted for review.');
    } else {
      await this.runProductAction(
        () => this.productService.submitForReview(saved.productId),
        'Product submitted for review.');
    }
  }

  protected async generateAiSuggestion(): Promise<void> {
    if (!this.isProductEditable()) {
      return;
    }

    const saved = await this.ensureProductSaved();
    if (!saved) {
      return;
    }

    this.isAiGenerating.set(true);
    this.aiErrorMessage.set(null);
    this.successMessage.set(null);

    try {
      const value = this.aiForm.getRawValue();
      const request: GenerateSellerAiSuggestionRequest = {
        sellerNotes: emptyToNull(value.sellerNotes),
        productTypeHint: emptyToNull(value.productTypeHint),
        selectedCategoryId: this.basicForm.controls.categoryId.value || null,
        knownAttributes: this.createAttributesRequest(),
        imageIds: saved.images.map(image => image.imageId)
      };
      const suggestion = await this.productService.generateAiSuggestion(saved.productId, request);
      this.aiSuggestion.set(suggestion);
      this.populateAiEditForms(suggestion);
      this.successMessage.set('AI suggestion generated.');
    } catch (error) {
      this.aiErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isAiGenerating.set(false);
    }
  }

  protected async applyAiSuggestion(): Promise<void> {
    const product = this.product();
    const suggestion = this.aiSuggestion();
    if (!product || !suggestion || !this.isProductEditable()) {
      return;
    }

    const fieldsToApply = this.selectedAiFields();
    if (fieldsToApply.length === 0) {
      this.aiErrorMessage.set('Select at least one AI field to apply.');
      return;
    }

    this.isAiApplying.set(true);
    this.aiErrorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.productService.applyAiSuggestion(
        product.productId,
        suggestion.suggestionId,
        {
          fieldsToApply,
          editedValues: this.createAiEditedValues(fieldsToApply),
          confirmRiskFlags: this.aiApplyForm.controls.confirmRiskFlags.value
        } satisfies ApplySellerAiSuggestionRequest);

      this.setProduct(updated);
      this.successMessage.set('AI suggestion applied.');
    } catch (error) {
      this.aiErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isAiApplying.set(false);
    }
  }

  protected aiAttributeEntries(): readonly { key: string; value: string }[] {
    const suggestion = this.aiSuggestion();
    if (!suggestion) {
      return [];
    }

    return Object.entries(suggestion.attributes)
      .map(([key, value]) => ({ key, value: renderAiValue(value) }));
  }

  protected categoryName(categoryId: string | null): string | null {
    return this.categories().find(category => category.categoryId === categoryId)?.name ?? null;
  }

  protected displayImages(): ProductEditorImage[] {
    return this.revision()?.images ?? this.product()?.images ?? [];
  }

  protected imageKey(image: ProductEditorImage): string {
    return 'revisionImageId' in image ? image.revisionImageId : image.imageId!;
  }

  protected isRevisionMode(): boolean {
    return this.product()?.status === 'Published';
  }

  protected isProductEditable(): boolean {
    const status = this.product()?.status;
    return status === undefined || status === 'Draft' || status === 'Rejected' || status === 'ChangesRequested';
  }

  protected isListingEditable(): boolean {
    if (this.isRevisionMode()) {
      return this.revision()?.canEdit ?? false;
    }

    return this.isProductEditable();
  }

  protected onImageFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (!file) {
      this.selectedImageFile.set(null);
      return;
    }

    const supportedTypes = ['image/jpeg', 'image/png', 'image/webp'];
    if (!supportedTypes.includes(file.type)) {
      this.errorMessage.set('Choose a JPEG, PNG, or WebP image.');
      this.selectedImageFile.set(null);
      input.value = '';
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.errorMessage.set('Image file cannot exceed 5 MB.');
      this.selectedImageFile.set(null);
      input.value = '';
      return;
    }

    this.errorMessage.set(null);
    this.selectedImageFile.set(file);
  }

  protected editorModeMessage(): string {
    const status = this.product()?.status ?? 'Draft';
    if (this.isProductEditable()) {
      return status === 'Draft'
        ? 'Build the listing, save changes, and submit it when every section is ready.'
        : 'Update the requested listing content and resubmit for marketplace review.';
    }

    if (status === 'Published') {
      const revisionStatus = this.revision()?.status ?? 'Draft';
      return revisionStatus === 'PendingReview'
        ? 'A proposed listing revision is waiting for admin review. The live listing remains visible to buyers.'
        : 'Stage listing and image changes as a revision. Buyers keep seeing the current approved listing until admin approval.';
    }

    if (status === 'OutOfStock') {
      return 'Listing content is locked. Manage stock in Inventory until the product is published again.';
    }

    return 'Listing content is locked while this product is in marketplace review.';
  }

  protected moderationLabel(actionType: string): string {
    return actionType
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace('Product Listing Revision', 'Revision');
  }

  protected renderCurrency(amount: number): string {
    return `R${amount.toFixed(2)}`;
  }

  protected previewPriceLabel(): string {
    const prices = this.product()?.variants
      .filter(variant => variant.status === 'Active')
      .map(variant => variant.price) ?? [];
    if (prices.length === 0) {
      return 'Add an active variant to show buyer pricing.';
    }

    return this.renderCurrency(Math.min(...prices));
  }

  protected seoPreviewTitle(): string {
    return this.basicForm.controls.seoTitle.value.trim()
      || this.basicForm.controls.title.value.trim()
      || 'Product title preview';
  }

  protected seoPreviewDescription(): string {
    return this.basicForm.controls.seoDescription.value.trim()
      || this.basicForm.controls.shortDescription.value.trim()
      || 'A concise product description will appear in marketplace and search contexts.';
  }

  protected attributePreviewEntries(): readonly { label: string; value: string }[] {
    return (this.selectedCategory()?.attributes ?? [])
      .map(attribute => {
        const value = this.attributeForm.controls[attribute.key]?.value;
        const renderted = renderPreviewValue(value);
        return renderted
          ? { label: `${attribute.name}${attribute.isRequired ? ' (required)' : ''}`, value: renderted }
          : null;
      })
      .filter((entry): entry is { label: string; value: string } => entry !== null);
  }

  protected canSubmitReview(): boolean {
    const product = this.product();
    if (this.isRevisionMode()) {
      return this.basicForm.valid
        && this.attributeForm.valid
        && this.displayImages().length > 0;
    }

    return this.basicForm.valid
      && this.attributeForm.valid
      && (product?.images.length ?? 0) > 0
      && (product?.variants.some(variant => variant.status === 'Active' && variant.availableQuantity > 0) ?? false);
  }

  protected isStepComplete(step: ProductStep): boolean {
    const product = this.product();
    return [
      this.basicForm.valid,
      this.basicForm.controls.categoryId.valid && this.attributeForm.valid,
      this.displayImages().length > 0,
      (product?.variants.length ?? 0) > 0,
      true,
      this.canSubmitReview() || product?.status === 'PendingReview'
    ][step];
  }

  protected reviewItems(): readonly { label: string; complete: boolean; summary: string }[] {
    const product = this.product();
    return [
      { label: 'Details', complete: this.basicForm.valid, summary: this.basicForm.controls.title.value || 'Basic details are required.' },
      { label: 'Category', complete: this.basicForm.controls.categoryId.valid && this.attributeForm.valid, summary: this.selectedCategory()?.name ?? 'Category is required.' },
      { label: 'Images', complete: this.displayImages().length > 0, summary: `${this.displayImages().length} attached` },
      {
        label: 'Variants',
        complete: this.isRevisionMode() || (product?.variants.some(variant => variant.status === 'Active' && variant.availableQuantity > 0) ?? false),
        summary: this.isRevisionMode() ? 'Variant and price changes are managed separately.' : `${product?.variants.length ?? 0} variants`
      }
    ];
  }

  private async loadEditor(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.categories.set(await this.productService.getCategories());
      const productId = this.route.snapshot.paramMap.get('id');
      if (productId) {
        const product = await this.productService.getProduct(productId);
        this.setProduct(product);
        if (product.status === 'Published') {
          const [revision, variantRevision] = await Promise.all([
            this.productService.getRevision(product.productId),
            this.productService.getVariantRevision(product.productId)
          ]);
          this.setRevision(revision);
          this.setVariantRevision(variantRevision);
        }
      } else {
        this.rebuildAttributeControls({});
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private setProduct(product: SellerProductDetailResponse): void {
    this.product.set(product);
    if (product.status !== 'Published') {
      this.revision.set(null);
      this.variantRevision.set(null);
    }
    this.selectedCategoryId.set(product.categoryId);
    this.basicForm.patchValue({
      categoryId: product.categoryId ?? '',
      title: product.title ?? '',
      slug: product.slug ?? '',
      shortDescription: product.shortDescription ?? '',
      fullDescription: product.fullDescription ?? '',
      seoTitle: product.seoTitle ?? '',
      seoDescription: product.seoDescription ?? '',
      merchandisingLabel: product.merchandisingLabel ?? '',
      careInstructions: product.careInstructions ?? '',
      productDisclaimer: product.productDisclaimer ?? ''
    });
    this.rebuildAttributeControls(product.attributes);

    const suggestion = this.aiSuggestion();
    if (suggestion) {
      this.rebuildAiImageAltTextControls(suggestion.imageAltText);
    }
  }

  private setRevision(revision: SellerProductRevisionResponse): void {
    this.revision.set(revision);
    this.selectedCategoryId.set(revision.categoryId);
    this.basicForm.patchValue({
      categoryId: revision.categoryId ?? '',
      title: revision.title ?? '',
      slug: revision.slug ?? '',
      shortDescription: revision.shortDescription ?? '',
      fullDescription: revision.fullDescription ?? '',
      seoTitle: revision.seoTitle ?? '',
      seoDescription: revision.seoDescription ?? '',
      merchandisingLabel: revision.merchandisingLabel ?? '',
      careInstructions: revision.careInstructions ?? '',
      productDisclaimer: revision.productDisclaimer ?? ''
    });
    this.rebuildAttributeControls(revision.attributes);
  }

  private setVariantRevision(revision: SellerProductVariantRevisionResponse): void {
    this.variantRevision.set(revision);
    this.variantRevisionForm.patchValue({
      sellerReason: revision.sellerReason ?? ''
    });
  }

  private rebuildAttributeControls(rawAttributes: Record<string, string>): void {
    for (const key of Object.keys(this.attributeForm.controls)) {
      this.attributeForm.removeControl(key);
    }

    for (const attribute of this.selectedCategory()?.attributes ?? []) {
      this.attributeForm.addControl(
        attribute.key,
        new FormControl(parseRawAttributeValue(rawAttributes[attribute.key], attribute), {
          validators: attribute.isRequired ? [Validators.required] : []
        }));
    }
  }

  private createProductRequest(): UpsertSellerProductRequest {
    return {
      categoryId: this.basicForm.controls.categoryId.value,
      brandId: null,
      title: this.basicForm.controls.title.value,
      slug: this.basicForm.controls.slug.value,
      shortDescription: this.basicForm.controls.shortDescription.value,
      fullDescription: this.basicForm.controls.fullDescription.value,
      seoTitle: emptyToNull(this.basicForm.controls.seoTitle.value),
      seoDescription: emptyToNull(this.basicForm.controls.seoDescription.value),
      merchandisingLabel: emptyToNull(this.basicForm.controls.merchandisingLabel.value),
      careInstructions: emptyToNull(this.basicForm.controls.careInstructions.value),
      productDisclaimer: emptyToNull(this.basicForm.controls.productDisclaimer.value),
      attributes: this.createAttributesRequest()
    };
  }

  private createVariantRequest(): UpsertSellerProductVariantRequest {
    const value = this.variantForm.getRawValue();
    return {
      sku: value.sku,
      size: value.size,
      colour: value.colour,
      price: Number(value.price),
      compareAtPrice: value.compareAtPrice ? Number(value.compareAtPrice) : null,
      stockQuantity: Number(value.stockQuantity),
      reservedQuantity: Number(value.reservedQuantity),
      status: value.status,
      barcode: emptyToNull(value.barcode)
    };
  }

  private variantRevisionItemsAsRequests(): UpsertSellerProductVariantRevisionItemRequest[] {
    return this.variantRevision()?.items.map(item => ({
      operation: normalizeVariantRevisionOperation(item.operation),
      sourceVariantId: item.sourceVariantId,
      sku: item.sku,
      size: item.size,
      colour: item.colour,
      price: item.price,
      compareAtPrice: item.compareAtPrice,
      initialStockQuantity: item.initialStockQuantity,
      barcode: item.barcode
    })) ?? [];
  }

  private createAttributesRequest(): Record<string, unknown> {
    const attributes: Record<string, unknown> = {};

    for (const attribute of this.selectedCategory()?.attributes ?? []) {
      const value = this.attributeForm.controls[attribute.key]?.value;
      if (value === null || value === undefined || value === '') {
        continue;
      }

      attributes[attribute.key] = attribute.dataType === 'Number' || attribute.dataType === 'Decimal'
        ? Number(value)
        : value;
    }

    return attributes;
  }

  private populateAiEditForms(suggestion: SellerAiSuggestionResponse): void {
    this.aiApplyForm.reset({
      title: false,
      shortDescription: false,
      fullDescription: false,
      category: false,
      attributes: false,
      tags: false,
      imageAltText: false,
      confirmRiskFlags: false
    });

    this.aiEditForm.patchValue({
      title: suggestion.recommendedTitle ?? '',
      shortDescription: suggestion.shortDescription ?? '',
      fullDescription: suggestion.fullDescription ?? '',
      suggestedCategoryId: suggestion.suggestedCategoryId ?? this.basicForm.controls.categoryId.value,
      tags: suggestion.tags.join(', ')
    });

    this.rebuildAiAttributeControls(suggestion.attributes);
    this.rebuildAiImageAltTextControls(suggestion.imageAltText);
  }

  private rebuildAiAttributeControls(attributes: Record<string, unknown>): void {
    for (const key of Object.keys(this.aiAttributeEditForm.controls)) {
      this.aiAttributeEditForm.removeControl(key);
    }

    for (const [key, value] of Object.entries(attributes)) {
      this.aiAttributeEditForm.addControl(key, new FormControl(renderAiValue(value), { nonNullable: true }));
    }
  }

  private rebuildAiImageAltTextControls(imageAltText: Record<string, string | null>): void {
    for (const key of Object.keys(this.aiImageAltTextForm.controls)) {
      this.aiImageAltTextForm.removeControl(key);
    }

    for (const image of this.product()?.images ?? []) {
      this.aiImageAltTextForm.addControl(
        image.imageId,
        new FormControl(imageAltText[image.imageId] ?? image.altText ?? '', { nonNullable: true }));
    }
  }

  private selectedAiFields(): string[] {
    const value = this.aiApplyForm.getRawValue();
    return [
      value.title ? 'title' : null,
      value.shortDescription ? 'shortDescription' : null,
      value.fullDescription ? 'fullDescription' : null,
      value.category ? 'category' : null,
      value.attributes ? 'attributes' : null,
      value.tags ? 'tags' : null,
      value.imageAltText ? 'imageAltText' : null
    ].filter((field): field is string => field !== null);
  }

  private createAiEditedValues(fieldsToApply: readonly string[]): Record<string, unknown> {
    const values = this.aiEditForm.getRawValue();
    const editedValues: Record<string, unknown> = {};

    if (fieldsToApply.includes('title')) {
      editedValues['title'] = emptyToNull(values.title);
    }

    if (fieldsToApply.includes('shortDescription')) {
      editedValues['shortDescription'] = emptyToNull(values.shortDescription);
    }

    if (fieldsToApply.includes('fullDescription')) {
      editedValues['fullDescription'] = emptyToNull(values.fullDescription);
    }

    if (fieldsToApply.includes('category')) {
      editedValues['suggestedCategoryId'] = values.suggestedCategoryId || null;
    }

    if (fieldsToApply.includes('attributes')) {
      editedValues['attributes'] = this.createAiAttributesRequest();
    }

    if (fieldsToApply.includes('tags')) {
      editedValues['tags'] = values.tags
        .split(',')
        .map(tag => tag.trim())
        .filter(tag => tag.length > 0);
    }

    if (fieldsToApply.includes('imageAltText')) {
      editedValues['imageAltText'] = this.createAiImageAltTextRequest();
    }

    return editedValues;
  }

  private createAiAttributesRequest(): Record<string, unknown> {
    const attributes: Record<string, unknown> = {};
    const currentCategory = this.categories().find(category =>
      category.categoryId === (this.aiEditForm.controls.suggestedCategoryId.value || this.basicForm.controls.categoryId.value));

    for (const [key, control] of Object.entries(this.aiAttributeEditForm.controls)) {
      const definition = currentCategory?.attributes.find(attribute => attribute.key === key);
      attributes[key] = parseEditedAttributeValue(control.value, definition);
    }

    return attributes;
  }

  private createAiImageAltTextRequest(): Record<string, string | null> {
    const altText: Record<string, string | null> = {};
    for (const [imageId, control] of Object.entries(this.aiImageAltTextForm.controls)) {
      altText[imageId] = emptyToNull(control.value);
    }

    return altText;
  }

  private async ensureProductSaved(): Promise<SellerProductDetailResponse | null> {
    return this.product() ?? await this.saveDraft();
  }

  private async runProductAction(
    action: () => Promise<SellerProductDetailResponse>,
    successMessage: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.setProduct(await action());
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private async runRevisionAction(
    action: () => Promise<SellerProductRevisionResponse>,
    successMessage: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.setRevision(await action());
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private async runVariantRevisionAction(
    action: () => Promise<SellerProductVariantRevisionResponse>,
    successMessage: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.setVariantRevision(await action());
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private async downloadVariantRevisionCsv(loader: () => Promise<Blob>, filename: string): Promise<void> {
    if (this.isVariantRevisionDownloading()) {
      return;
    }

    this.isVariantRevisionDownloading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const blob = await loader();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = filename;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isVariantRevisionDownloading.set(false);
    }
  }

  private ensureValid(control: AbstractControl): boolean {
    if (control.invalid || this.isSaving()) {
      control.markAllAsTouched();
      return false;
    }

    return true;
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function normalizeVariantRevisionOperation(operation: string): 'Add' | 'Update' | 'Deactivate' {
  return operation === 'Deactivate' || operation === 'Update' ? operation : 'Add';
}

function revisionItemResponseAsRequest(
  item: SellerProductVariantRevisionItemResponse): UpsertSellerProductVariantRevisionItemRequest {
  return {
    operation: normalizeVariantRevisionOperation(item.operation),
    sourceVariantId: item.sourceVariantId,
    sku: item.sku,
    size: item.size,
    colour: item.colour,
    price: item.price,
    compareAtPrice: item.compareAtPrice,
    initialStockQuantity: item.initialStockQuantity,
    barcode: item.barcode
  };
}

function sameVariantRevisionTarget(
  left: UpsertSellerProductVariantRevisionItemRequest,
  right: UpsertSellerProductVariantRevisionItemRequest): boolean {
  if (left.operation === 'Add' || right.operation === 'Add') {
    return left.operation === right.operation
      && left.sku?.trim().toLowerCase() === right.sku?.trim().toLowerCase()
      && left.size?.trim().toLowerCase() === right.size?.trim().toLowerCase()
      && left.colour?.trim().toLowerCase() === right.colour?.trim().toLowerCase();
  }

  return left.sourceVariantId === right.sourceVariantId;
}

function parseRawAttributeValue(
  rawValue: string | undefined,
  attribute: SellerCatalogCategoryAttributeResponse): unknown {
  if (rawValue === undefined) {
    return attribute.dataType === 'MultiSelect' ? [] : '';
  }

  try {
    return JSON.parse(rawValue) as unknown;
  } catch {
    return rawValue;
  }
}

function renderPreviewValue(value: unknown): string | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  if (Array.isArray(value)) {
    return value.length === 0 ? null : value.join(', ');
  }

  return String(value);
}

function parseEditedAttributeValue(
  rawValue: string,
  attribute: SellerCatalogCategoryAttributeResponse | undefined): unknown {
  const trimmed = rawValue.trim();
  if (trimmed.length === 0) {
    return null;
  }

  if (attribute?.dataType === 'Number' || attribute?.dataType === 'Decimal') {
    return Number(trimmed);
  }

  if (attribute?.dataType === 'Boolean') {
    return trimmed.toLowerCase() === 'true';
  }

  if (attribute?.dataType === 'MultiSelect') {
    return trimmed.split(',').map(value => value.trim()).filter(value => value.length > 0);
  }

  return trimmed;
}

function renderAiValue(value: unknown): string {
  if (Array.isArray(value)) {
    return value.join(', ');
  }

  if (value === null || value === undefined) {
    return '';
  }

  return typeof value === 'object'
    ? JSON.stringify(value)
    : String(value);
}
