import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerProductService } from './seller-product.service';

describe('SellerProductService', () => {
  let service: SellerProductService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerProductService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads seller catalog categories', async () => {
    const promise = service.getCategories();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/catalog/categories`);
    expect(request.request.method).toBe('GET');
    request.flush([createCategory()]);

    const categories = await promise;
    expect(categories[0].attributes[0].key).toBe('size');
  });

  it('creates and submits a product draft', async () => {
    const createPromise = service.createProduct(createProductRequest());
    const createRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products`);
    expect(createRequest.request.method).toBe('POST');
    createRequest.flush(createProductDetail());

    const created = await createPromise;
    expect(created.status).toBe('Draft');

    const submitPromise = service.submitForReview('product-id');
    const submitRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/submit-review`);
    expect(submitRequest.request.method).toBe('POST');
    submitRequest.flush(createProductDetail({ status: 'PendingReview' }));

    const submitted = await submitPromise;
    expect(submitted.status).toBe('PendingReview');
  });

  it('adds and removes variants and images', async () => {
    const variantPromise = service.addVariant('product-id', {
      sku: 'SKU-1',
      size: 'M',
      colour: 'Black',
      price: 100,
      compareAtPrice: null,
      stockQuantity: 10,
      reservedQuantity: 0,
      status: 'Active',
      barcode: null
    });
    const variantRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variants`);
    expect(variantRequest.request.method).toBe('POST');
    variantRequest.flush(createProductDetail());
    await expectAsync(variantPromise).toBeResolved();

    const updateVariantPromise = service.updateVariant('product-id', 'variant-id', {
      sku: 'SKU-1',
      size: 'L',
      colour: 'Black',
      price: 120,
      compareAtPrice: null,
      stockQuantity: 8,
      reservedQuantity: 0,
      status: 'Active',
      barcode: null
    });
    const updateVariantRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variants/variant-id`);
    expect(updateVariantRequest.request.method).toBe('PUT');
    updateVariantRequest.flush(createProductDetail());
    await expectAsync(updateVariantPromise).toBeResolved();

    const deleteVariantPromise = service.deleteVariant('product-id', 'variant-id');
    const deleteVariantRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variants/variant-id`);
    expect(deleteVariantRequest.request.method).toBe('DELETE');
    deleteVariantRequest.flush(createProductDetail());
    await expectAsync(deleteVariantPromise).toBeResolved();

    const imagePromise = service.addImage('product-id', {
      storageKey: 'image-key',
      url: 'https://example.test/image.jpg',
      altText: 'Image',
      sortOrder: 0,
      isPrimary: true
    });
    const imageRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/images`);
    expect(imageRequest.request.method).toBe('POST');
    imageRequest.flush(createProductDetail());
    await expectAsync(imagePromise).toBeResolved();

    const updateImagePromise = service.updateImage('product-id', 'image-id', {
      altText: 'Updated image',
      sortOrder: 2,
      isPrimary: true
    });
    const updateImageRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/images/image-id`);
    expect(updateImageRequest.request.method).toBe('PUT');
    expect(updateImageRequest.request.body).toEqual({
      altText: 'Updated image',
      sortOrder: 2,
      isPrimary: true
    });
    updateImageRequest.flush(createProductDetail());
    await expectAsync(updateImagePromise).toBeResolved();

    const deleteImagePromise = service.deleteImage('product-id', 'image-id');
    const deleteImageRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/images/image-id`);
    expect(deleteImageRequest.request.method).toBe('DELETE');
    deleteImageRequest.flush(createProductDetail());
    await expectAsync(deleteImagePromise).toBeResolved();
  });

  it('generates and applies AI product suggestions', async () => {
    const generatePromise = service.generateAiSuggestion('product-id', {
      sellerNotes: 'Lightweight dress',
      productTypeHint: 'Dress',
      selectedCategoryId: 'category-id',
      knownAttributes: { size: 'M' },
      imageIds: ['image-id']
    });
    const generateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/ai-suggestions`);
    expect(generateRequest.request.method).toBe('POST');
    generateRequest.flush(createAiSuggestion());

    const suggestion = await generatePromise;
    expect(suggestion.recommendedTitle).toBe('AI title');

    const applyPromise = service.applyAiSuggestion('product-id', 'suggestion-id', {
      fieldsToApply: ['title'],
      editedValues: { title: 'Seller title' },
      confirmRiskFlags: false
    });
    const applyRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/ai-suggestions/suggestion-id/apply`);
    expect(applyRequest.request.method).toBe('POST');
    applyRequest.flush(createProductDetail({ title: 'Seller title' }));

    const product = await applyPromise;
    expect(product.title).toBe('Seller title');
  });

  it('manages published variant revision requests', async () => {
    const getPromise = service.getVariantRevision('product-id');
    const getRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variant-revision`);
    expect(getRequest.request.method).toBe('GET');
    getRequest.flush(createVariantRevision());
    await expectAsync(getPromise).toBeResolved();

    const updatePromise = service.updateVariantRevision('product-id', {
      sellerReason: 'Seasonal price update.',
      items: [{
        operation: 'Update',
        sourceVariantId: 'variant-id',
        sku: 'SKU-EDIT',
        size: 'M',
        colour: 'Black',
        price: 120,
        compareAtPrice: null,
        initialStockQuantity: null,
        barcode: null
      }]
    });
    const updateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variant-revision`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body.sellerReason).toBe('Seasonal price update.');
    updateRequest.flush(createVariantRevision({ items: [createVariantRevisionItem()] }));
    await expectAsync(updatePromise).toBeResolved();

    const exportPromise = service.exportVariantRevisionCsv('product-id');
    const exportRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variant-revision/export.csv`);
    expect(exportRequest.request.method).toBe('GET');
    expect(exportRequest.request.responseType).toBe('blob');
    exportRequest.flush(new Blob(['operation,sourceVariantId']));
    await expectAsync(exportPromise).toBeResolved();

    const templatePromise = service.downloadVariantRevisionTemplate('product-id');
    const templateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variant-revision/import-template.csv`);
    expect(templateRequest.request.method).toBe('GET');
    expect(templateRequest.request.responseType).toBe('blob');
    templateRequest.flush(new Blob(['operation,sourceVariantId']));
    await expectAsync(templatePromise).toBeResolved();

    const previewPromise = service.previewVariantRevisionImport('product-id', new File(['csv'], 'variants.csv', { type: 'text/csv' }));
    const previewRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variant-revision/import/preview`);
    expect(previewRequest.request.method).toBe('POST');
    expect(previewRequest.request.body instanceof FormData).toBeTrue();
    previewRequest.flush({
      totalRows: 1,
      validRows: 1,
      errorRows: 0,
      changedRows: 1,
      unchangedRows: 0,
      rows: [],
      proposedFinalVariants: []
    });
    await expectAsync(previewPromise).toBeResolved();

    const bulkPromise = service.bulkStageVariantRevision('product-id', {
      sellerReason: 'CSV update.',
      items: [{
        operation: 'Update',
        sourceVariantId: 'variant-id',
        sku: 'SKU-EDIT',
        size: 'M',
        colour: 'Black',
        price: 120,
        compareAtPrice: null,
        initialStockQuantity: null,
        barcode: null
      }]
    });
    const bulkRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variant-revision/bulk-stage`);
    expect(bulkRequest.request.method).toBe('POST');
    expect(bulkRequest.request.body.sellerReason).toBe('CSV update.');
    bulkRequest.flush({
      totalRows: 1,
      validRows: 1,
      errorRows: 0,
      changedRows: 1,
      unchangedRows: 0,
      rows: [],
      proposedFinalVariants: []
    });
    await expectAsync(bulkPromise).toBeResolved();

    const submitPromise = service.submitVariantRevisionForReview('product-id');
    const submitRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variant-revision/submit-review`);
    expect(submitRequest.request.method).toBe('POST');
    submitRequest.flush(createVariantRevision({ status: 'PendingReview' }));
    await expectAsync(submitPromise).toBeResolved();

    const cancelPromise = service.cancelVariantRevision('product-id');
    const cancelRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/products/product-id/variant-revision/cancel`);
    expect(cancelRequest.request.method).toBe('POST');
    cancelRequest.flush(createVariantRevision({ status: 'Cancelled' }));
    await expectAsync(cancelPromise).toBeResolved();
  });
});

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
      dataType: 'Select',
      isRequired: true,
      allowedValues: ['S', 'M', 'L'],
      displayOrder: 10
    }]
  };
}

function createProductRequest() {
  return {
    categoryId: 'category-id',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: 'Short',
    fullDescription: 'Full',
    attributes: { size: 'M' }
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
    missingFields: [],
    riskFlags: [],
    qualityScore: 70
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
    currentVariants: [],
    items: [],
    proposedFinalVariants: [],
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
    sku: 'SKU-EDIT',
    size: 'M',
    colour: 'Black',
    price: 120,
    compareAtPrice: null,
    initialStockQuantity: null,
    proposedStatus: 'Active',
    barcode: null,
    ...overrides
  };
}
