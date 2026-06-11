import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { SellerProductService } from '../seller/seller-product.service';
import { SellerProductFormPageComponent } from './seller-product-form-page.component';

describe('SellerProductFormPageComponent', () => {
  let fixture: ComponentFixture<SellerProductFormPageComponent>;
  let productService: jasmine.SpyObj<SellerProductService>;

  beforeEach(async () => {
    productService = jasmine.createSpyObj<SellerProductService>(
      'SellerProductService',
      [
        'getCategories',
        'getProduct',
        'createProduct',
        'updateProduct',
        'addVariant',
        'updateVariant',
        'deleteVariant',
        'addImage',
        'uploadImage',
        'updateImage',
        'deleteImage',
        'submitForReview',
        'getRevision',
        'updateRevision',
        'uploadRevisionImage',
        'updateRevisionImage',
        'deleteRevisionImage',
        'submitRevisionForReview',
        'cancelRevision',
        'getVariantRevision',
        'updateVariantRevision',
        'exportVariantRevisionCsv',
        'downloadVariantRevisionTemplate',
        'previewVariantRevisionImport',
        'bulkStageVariantRevision',
        'submitVariantRevisionForReview',
        'cancelVariantRevision',
        'generateAiSuggestion',
        'applyAiSuggestion'
      ]);
    productService.getCategories.and.resolveTo([createCategory()]);
    productService.createProduct.and.resolveTo(createProductDetail());
    productService.updateProduct.and.resolveTo(createProductDetail());
    productService.getProduct.and.resolveTo(createProductDetail());
    productService.getRevision.and.resolveTo(createRevision());
    productService.getVariantRevision.and.resolveTo(createVariantRevision());
    productService.updateVariantRevision.and.resolveTo(createVariantRevision({
      items: [createVariantRevisionItem({ operation: 'Update', sku: 'SKU-EDIT', price: 129 })]
    }));
    productService.exportVariantRevisionCsv.and.resolveTo(new Blob(['operation,sourceVariantId'], { type: 'text/csv' }));
    productService.downloadVariantRevisionTemplate.and.resolveTo(new Blob(['operation,sourceVariantId'], { type: 'text/csv' }));
    productService.previewVariantRevisionImport.and.resolveTo(createVariantRevisionImportPreview());
    productService.bulkStageVariantRevision.and.resolveTo(createVariantRevisionImportPreview());
    productService.submitVariantRevisionForReview.and.resolveTo(createVariantRevision({ status: 'PendingReview', canEdit: false }));
    productService.cancelVariantRevision.and.resolveTo(createVariantRevision({ status: 'Cancelled', canEdit: false }));
    productService.updateImage.and.resolveTo(createProductDetail({
      images: [createImage({ altText: 'Updated side image', sortOrder: 2, isPrimary: true })]
    }));
    productService.generateAiSuggestion.and.resolveTo(createAiSuggestion());
    productService.applyAiSuggestion.and.resolveTo(createProductDetail({
      title: 'Seller reviewed AI title',
      tags: ['summer']
    }));
    productService.addVariant.and.resolveTo(createProductDetail({
      variants: [{
        variantId: 'variant-id',
        sku: 'SKU-1',
        size: 'M',
        colour: 'Black',
        price: 100,
        compareAtPrice: null,
        stockQuantity: 10,
        reservedQuantity: 0,
        status: 'Active',
        barcode: null,
        availableQuantity: 10
      }]
    }));
    productService.updateVariant.and.resolveTo(createProductDetail({
      variants: [createVariant({ sku: 'SKU-EDIT', price: 129 })]
    }));

    await TestBed.configureTestingModule({
      imports: [SellerProductFormPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({})
            }
          }
        },
        { provide: SellerProductService, useValue: productService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerProductFormPageComponent);
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('loads categories and builds required dynamic attribute controls', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      onCategoryChanged(categoryId: string): void;
      attributeForm: { controls: Record<string, unknown> };
    };

    component.onCategoryChanged('category-id');

    expect(component.attributeForm.controls['size']).toBeDefined();
  });

  it('creates a product draft with category attributes', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      onCategoryChanged(categoryId: string): void;
      basicForm: { patchValue(value: Record<string, unknown>): void };
      attributeForm: { controls: Record<string, { setValue(value: unknown): void }> };
      saveDraft(): Promise<unknown>;
    };

    component.onCategoryChanged('category-id');
    component.basicForm.patchValue({
      categoryId: 'category-id',
      title: 'Summer Dress',
      slug: 'summer-dress',
      shortDescription: 'Short',
      fullDescription: 'Full',
      seoTitle: 'SEO summer dress',
      seoDescription: 'SEO description for summer dress.',
      merchandisingLabel: 'Seller pick',
      careInstructions: 'Cold wash.',
      productDisclaimer: 'Colour may vary.'
    });
    component.attributeForm.controls['size'].setValue('M');

    await component.saveDraft();

    expect(productService.createProduct).toHaveBeenCalledWith(jasmine.objectContaining({
      categoryId: 'category-id',
      seoTitle: 'SEO summer dress',
      seoDescription: 'SEO description for summer dress.',
      merchandisingLabel: 'Seller pick',
      careInstructions: 'Cold wash.',
      productDisclaimer: 'Colour may vary.',
      attributes: jasmine.objectContaining({ size: 'M' })
    }));
  });

  it('renders seller workspace navigation, editor status, and image gallery metadata actions', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const image = createImage();
    const component = fixture.componentInstance as unknown as {
      product: { set(value: unknown): void };
      currentStep: { set(value: number): void };
      editImage(image: unknown): void;
      imageEditForm: {
        setValue(value: { altText: string; sortOrder: number; isPrimary: boolean }): void;
      };
      saveImageMetadata(): Promise<void>;
    };
    component.product.set(createProductDetail({ images: [image] }));
    component.currentStep.set(2);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Products');
    expect(compiled.textContent).toContain('Product editor');
    expect(compiled.textContent).toContain('Primary');

    component.editImage(image);
    component.imageEditForm.setValue({ altText: 'Updated side image', sortOrder: 2, isPrimary: true });
    await component.saveImageMetadata();

    expect(productService.updateImage).toHaveBeenCalledWith('product-id', 'image-id', {
      altText: 'Updated side image',
      sortOrder: 2,
      isPrimary: true
    });
  });

  it('updates existing variants and still supports add mode', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const variant = createVariant();
    const component = fixture.componentInstance as unknown as {
      product: { set(value: unknown): void };
      editVariant(variant: unknown): void;
      startNewVariant(): void;
      variantForm: {
        setValue(value: {
          sku: string;
          size: string;
          colour: string;
          price: number;
          compareAtPrice: number | null;
          stockQuantity: number;
          reservedQuantity: number;
          status: 'Active' | 'Inactive' | 'OutOfStock';
          barcode: string;
        }): void;
      };
      saveVariant(): Promise<void>;
    };
    component.product.set(createProductDetail({ variants: [variant] }));

    component.editVariant(variant);
    component.variantForm.setValue({
      sku: 'SKU-EDIT',
      size: 'L',
      colour: 'Black',
      price: 129,
      compareAtPrice: null,
      stockQuantity: 8,
      reservedQuantity: 0,
      status: 'Active',
      barcode: ''
    });
    await component.saveVariant();

    expect(productService.updateVariant).toHaveBeenCalledWith('product-id', 'variant-id', jasmine.objectContaining({
      sku: 'SKU-EDIT',
      price: 129
    }));

    component.startNewVariant();
    component.variantForm.setValue({
      sku: 'SKU-NEW',
      size: 'M',
      colour: 'Ivory',
      price: 100,
      compareAtPrice: null,
      stockQuantity: 5,
      reservedQuantity: 0,
      status: 'Active',
      barcode: ''
    });
    await component.saveVariant();

    expect(productService.addVariant).toHaveBeenCalledWith('product-id', jasmine.objectContaining({
      sku: 'SKU-NEW',
      colour: 'Ivory'
    }));
  });

  it('blocks mutating editor actions for read-only product statuses', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const image = createImage();
    const component = fixture.componentInstance as unknown as {
      product: { set(value: unknown): void };
      editImage(image: unknown): void;
      saveImageMetadata(): Promise<void>;
      isProductEditable(): boolean;
    };
    component.product.set(createProductDetail({ status: 'Published', images: [image] }));
    component.editImage(image);
    fixture.detectChanges();

    await component.saveImageMetadata();

    expect(component.isProductEditable()).toBeFalse();
    expect(productService.updateImage).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('current approved listing');
  });

  it('stages published variant revision updates without editing live variants', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const variant = createVariant();
    const component = fixture.componentInstance as unknown as {
      product: { set(value: unknown): void };
      variantRevision: { set(value: unknown): void };
      stageVariantRevisionUpdate(variant: unknown): void;
      variantRevisionForm: {
        patchValue(value: Record<string, unknown>): void;
      };
      addVariantRevisionItem(): Promise<void>;
    };
    component.product.set(createProductDetail({ status: 'Published', variants: [variant] }));
    component.variantRevision.set(createVariantRevision());

    component.stageVariantRevisionUpdate(variant);
    component.variantRevisionForm.patchValue({ sku: 'SKU-EDIT', price: 129 });
    await component.addVariantRevisionItem();

    expect(productService.updateVariant).not.toHaveBeenCalled();
    expect(productService.updateVariantRevision).toHaveBeenCalledWith('product-id', jasmine.objectContaining({
      items: [jasmine.objectContaining({
        operation: 'Update',
        sourceVariantId: 'variant-id',
        sku: 'SKU-EDIT',
        price: 129
      })]
    }));
  });

  it('bulk-stages published variant revision import rows', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      product: { set(value: unknown): void };
      variantRevision: { set(value: unknown): void };
      variantRevisionImportPreview: { set(value: unknown): void };
      variantRevisionForm: {
        patchValue(value: Record<string, unknown>): void;
      };
      bulkStageVariantRevisionImport(): Promise<void>;
    };
    component.product.set(createProductDetail({ status: 'Published', variants: [createVariant()] }));
    component.variantRevision.set(createVariantRevision());
    component.variantRevisionImportPreview.set(createVariantRevisionImportPreview());
    component.variantRevisionForm.patchValue({ sellerReason: 'Seasonal CSV update.' });

    await component.bulkStageVariantRevisionImport();

    expect(productService.bulkStageVariantRevision).toHaveBeenCalledWith('product-id', jasmine.objectContaining({
      sellerReason: 'Seasonal CSV update.',
      items: [jasmine.objectContaining({
        operation: 'Update',
        sourceVariantId: 'variant-id',
        sku: 'SKU-EDIT',
        price: 129
      })]
    }));
    expect(productService.getVariantRevision).toHaveBeenCalledWith('product-id');
  });

  it('generates an AI suggestion from the saved product draft', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      onCategoryChanged(categoryId: string): void;
      basicForm: { patchValue(value: Record<string, unknown>): void };
      attributeForm: { controls: Record<string, { setValue(value: unknown): void }> };
      aiForm: { patchValue(value: Record<string, unknown>): void };
      generateAiSuggestion(): Promise<void>;
      aiSuggestion(): { recommendedTitle: string | null } | null;
    };

    fillValidProductForm(component);
    component.aiForm.patchValue({ sellerNotes: 'Lightweight dress', productTypeHint: 'Dress' });

    await component.generateAiSuggestion();

    expect(productService.generateAiSuggestion).toHaveBeenCalledWith('product-id', jasmine.objectContaining({
      sellerNotes: 'Lightweight dress',
      productTypeHint: 'Dress',
      selectedCategoryId: 'category-id',
      knownAttributes: jasmine.objectContaining({ size: 'M' })
    }));
    expect(component.aiSuggestion()?.recommendedTitle).toBe('AI title');
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.hf-ai-listing-assistant')).not.toBeNull();
    expect(compiled.textContent).toContain('Quality score: 70%');
  });

  it('applies selected AI suggestions with seller edits', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      onCategoryChanged(categoryId: string): void;
      basicForm: { patchValue(value: Record<string, unknown>): void };
      attributeForm: { controls: Record<string, { setValue(value: unknown): void }> };
      generateAiSuggestion(): Promise<void>;
      applyAiSuggestion(): Promise<void>;
      aiApplyForm: { patchValue(value: Record<string, unknown>): void };
      aiEditForm: { patchValue(value: Record<string, unknown>): void };
    };

    fillValidProductForm(component);

    await component.generateAiSuggestion();
    component.aiApplyForm.patchValue({ title: true, tags: true });
    component.aiEditForm.patchValue({ title: 'Seller reviewed AI title', tags: 'summer' });

    await component.applyAiSuggestion();

    expect(productService.applyAiSuggestion).toHaveBeenCalledWith(
      'product-id',
      'suggestion-id',
      jasmine.objectContaining({
        fieldsToApply: ['title', 'tags'],
        editedValues: jasmine.objectContaining({
          title: 'Seller reviewed AI title',
          tags: ['summer']
        })
      }));
  });
});

function fillValidProductForm(component: {
  onCategoryChanged(categoryId: string): void;
  basicForm: { patchValue(value: Record<string, unknown>): void };
  attributeForm: { controls: Record<string, { setValue(value: unknown): void }> };
}): void {
  component.onCategoryChanged('category-id');
  component.basicForm.patchValue({
    categoryId: 'category-id',
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'Short',
    fullDescription: 'Full'
  });
  component.attributeForm.controls['size'].setValue('M');
}

function createCategory() {
  return {
    categoryId: 'category-id',
    parentCategoryId: null,
    name: 'Dresses',
    slug: 'dresses',
    displayOrder: 10,
    attributes: [{
      attributeId: 'attribute-id',
      name: 'Size',
      key: 'size',
      dataType: 'Select' as const,
      isRequired: true,
      allowedValues: ['S', 'M', 'L'],
      displayOrder: 10
    }]
  };
}

function createProductDetail(overrides: Record<string, unknown> = {}) {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    categoryId: 'category-id',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'Short',
    fullDescription: 'Full',
    tags: [],
    status: 'Draft',
    rejectionReason: null,
    createdAtUtc: '2026-05-18T12:00:00Z',
    updatedAtUtc: '2026-05-18T12:00:00Z',
    publishedAtUtc: null,
    attributes: { size: '"M"' },
    variants: [],
    images: [],
    moderationEvents: [],
    ...overrides
  };
}

function createImage(overrides: Record<string, unknown> = {}) {
  return {
    imageId: 'image-id',
    url: 'https://example.test/image.jpg',
    storageKey: 'image-key',
    altText: 'Product image',
    sortOrder: 0,
    isPrimary: true,
    createdAtUtc: '2026-05-18T12:00:00Z',
    ...overrides
  };
}

function createRevision(overrides: Record<string, unknown> = {}) {
  return {
    revisionId: 'revision-id',
    productId: 'product-id',
    sellerId: 'seller-id',
    status: 'Draft',
    canEdit: true,
    rejectionReason: null,
    submittedAtUtc: null,
    reviewedAtUtc: null,
    categoryId: 'category-id',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'Short',
    fullDescription: 'Full',
    tags: [],
    attributes: { size: '"M"' },
    images: [{
      revisionImageId: 'revision-image-id',
      sourceProductImageId: 'image-id',
      url: 'https://example.test/image.jpg',
      storageKey: 'image-key',
      altText: 'Product image',
      sortOrder: 0,
      isPrimary: true,
      createdAtUtc: '2026-05-18T12:00:00Z'
    }],
    moderationEvents: [],
    ...overrides
  };
}

function createVariantRevision(overrides: Record<string, unknown> = {}) {
  return {
    revisionId: 'variant-revision-id',
    productId: 'product-id',
    sellerId: 'seller-id',
    status: 'Draft',
    canEdit: true,
    sellerReason: null,
    rejectionReason: null,
    submittedAtUtc: null,
    reviewedAtUtc: null,
    currentVariants: [createRevisionVariant()],
    items: [],
    proposedFinalVariants: [createRevisionVariant()],
    validationErrors: {},
    moderationEvents: [],
    ...overrides
  };
}

function createVariantRevisionItem(overrides: Record<string, unknown> = {}) {
  return {
    revisionItemId: 'variant-revision-item-id',
    operation: 'Update',
    sourceVariantId: 'variant-id',
    sku: 'SKU-1',
    size: 'M',
    colour: 'Black',
    price: 100,
    compareAtPrice: null,
    initialStockQuantity: null,
    proposedStatus: 'Active',
    barcode: null,
    ...overrides
  };
}

function createVariantRevisionImportPreview(overrides: Record<string, unknown> = {}) {
  return {
    totalRows: 1,
    validRows: 1,
    errorRows: 0,
    changedRows: 1,
    unchangedRows: 0,
    rows: [{
      rowNumber: 2,
      operation: 'Update',
      sourceVariantId: 'variant-id',
      currentSku: 'SKU-1',
      currentSize: 'M',
      currentColour: 'Black',
      currentPrice: 100,
      currentCompareAtPrice: null,
      currentStatus: 'Active',
      currentBarcode: null,
      proposedSku: 'SKU-EDIT',
      proposedSize: 'M',
      proposedColour: 'Black',
      proposedPrice: 129,
      proposedCompareAtPrice: null,
      proposedInitialStockQuantity: null,
      proposedBarcode: null,
      rowStatus: 'Changed',
      validationMessages: []
    }],
    proposedFinalVariants: [createRevisionVariant({ sku: 'SKU-EDIT', price: 129, changeType: 'Update' })],
    ...overrides
  };
}

function createRevisionVariant(overrides: Record<string, unknown> = {}) {
  return {
    sourceVariantId: 'variant-id',
    changeType: 'Unchanged',
    sku: 'SKU-1',
    size: 'M',
    colour: 'Black',
    price: 100,
    compareAtPrice: null,
    stockQuantity: 10,
    reservedQuantity: 0,
    status: 'Active',
    barcode: null,
    availableQuantity: 10,
    ...overrides
  };
}

function createVariant(overrides: Record<string, unknown> = {}) {
  return {
    variantId: 'variant-id',
    sku: 'SKU-1',
    size: 'M',
    colour: 'Black',
    price: 100,
    compareAtPrice: null,
    stockQuantity: 10,
    reservedQuantity: 0,
    status: 'Active' as const,
    barcode: null,
    availableQuantity: 10,
    ...overrides
  };
}

function createAiSuggestion() {
  return {
    suggestionId: 'suggestion-id',
    recommendedTitle: 'AI title',
    titleSuggestions: ['AI title'],
    shortDescription: 'AI short',
    fullDescription: 'AI full',
    suggestedCategoryId: 'category-id',
    suggestedCategoryPath: 'Dresses',
    attributes: { size: 'M' },
    tags: ['summer'],
    seo: {},
    imageAltText: {},
    missingFields: ['brand'],
    riskFlags: [],
    qualityScore: 70
  };
}
