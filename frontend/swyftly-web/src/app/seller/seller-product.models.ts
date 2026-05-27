export interface SellerCatalogCategoryResponse {
  categoryId: string;
  parentCategoryId: string | null;
  name: string;
  slug: string;
  displayOrder: number;
  attributes: SellerCatalogCategoryAttributeResponse[];
}

export interface SellerCatalogCategoryAttributeResponse {
  attributeId: string;
  name: string;
  key: string;
  dataType: 'Text' | 'Number' | 'Decimal' | 'Boolean' | 'Select' | 'MultiSelect' | 'Date';
  isRequired: boolean;
  allowedValues: string[];
  displayOrder: number;
}

export interface SellerProductSummaryResponse {
  productId: string;
  categoryId: string | null;
  title: string | null;
  slug: string | null;
  status: string;
  merchandisingLabel?: string | null;
  primaryImageUrl?: string | null;
  primaryImageAltText?: string | null;
  totalStockQuantity?: number;
  reservedQuantity?: number;
  availableQuantity?: number;
  lowStockVariantCount?: number;
  outOfStockVariantCount?: number;
  updatedAtUtc: string;
}

export interface SellerProductDetailResponse {
  productId: string;
  sellerId: string;
  categoryId: string | null;
  brandId: string | null;
  title: string | null;
  slug: string | null;
  shortDescription: string | null;
  fullDescription: string | null;
  seoTitle?: string | null;
  seoDescription?: string | null;
  merchandisingLabel?: string | null;
  careInstructions?: string | null;
  productDisclaimer?: string | null;
  tags: string[];
  status: string;
  rejectionReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  publishedAtUtc: string | null;
  attributes: Record<string, string>;
  variants: SellerProductVariantResponse[];
  images: SellerProductImageResponse[];
  moderationEvents: SellerModerationEventResponse[];
}

export interface SellerProductVariantResponse {
  variantId: string;
  sku: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  reservedQuantity: number;
  status: 'Active' | 'Inactive' | 'OutOfStock';
  barcode: string | null;
  availableQuantity: number;
}

export interface SellerProductImageResponse {
  imageId: string;
  url: string;
  storageKey: string;
  altText: string | null;
  sortOrder: number;
  isPrimary: boolean;
  createdAtUtc: string;
}

export interface SellerProductRevisionResponse {
  revisionId: string;
  productId: string;
  sellerId: string;
  status: string;
  canEdit: boolean;
  rejectionReason: string | null;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  categoryId: string | null;
  brandId: string | null;
  title: string | null;
  slug: string | null;
  shortDescription: string | null;
  fullDescription: string | null;
  seoTitle?: string | null;
  seoDescription?: string | null;
  merchandisingLabel?: string | null;
  careInstructions?: string | null;
  productDisclaimer?: string | null;
  tags: string[];
  attributes: Record<string, string>;
  images: SellerProductRevisionImageResponse[];
  moderationEvents: SellerModerationEventResponse[];
}

export interface SellerModerationEventResponse {
  auditLogId: string;
  actionType: string;
  actorRole: string | null;
  reason: string | null;
  createdAtUtc: string;
}

export interface SellerProductRevisionImageResponse {
  revisionImageId: string;
  sourceProductImageId: string | null;
  url: string;
  storageKey: string;
  altText: string | null;
  sortOrder: number;
  isPrimary: boolean;
  createdAtUtc: string;
}

export interface SellerProductVariantRevisionResponse {
  revisionId: string;
  productId: string;
  sellerId: string;
  status: string;
  canEdit: boolean;
  sellerReason: string | null;
  rejectionReason: string | null;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  currentVariants: SellerProductVariantRevisionFinalVariantResponse[];
  items: SellerProductVariantRevisionItemResponse[];
  proposedFinalVariants: SellerProductVariantRevisionFinalVariantResponse[];
  moderationEvents: SellerModerationEventResponse[];
}

export interface SellerProductVariantRevisionItemResponse {
  revisionItemId: string;
  operation: 'Add' | 'Update' | 'Deactivate' | string;
  sourceVariantId: string | null;
  sku: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  initialStockQuantity: number | null;
  proposedStatus: 'Active' | 'Inactive' | 'OutOfStock' | string;
  barcode: string | null;
}

export interface SellerProductVariantRevisionFinalVariantResponse {
  sourceVariantId: string | null;
  changeType: string;
  sku: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  reservedQuantity: number;
  status: string;
  barcode: string | null;
  availableQuantity: number;
}

export interface UpsertSellerProductRequest {
  categoryId: string | null;
  brandId: string | null;
  title: string | null;
  slug: string | null;
  shortDescription: string | null;
  fullDescription: string | null;
  seoTitle?: string | null;
  seoDescription?: string | null;
  merchandisingLabel?: string | null;
  careInstructions?: string | null;
  productDisclaimer?: string | null;
  attributes: Record<string, unknown>;
}

export interface UpsertSellerProductRevisionRequest extends UpsertSellerProductRequest {
  tags: string[];
}

export interface UpsertSellerProductVariantRequest {
  sku: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  reservedQuantity: number;
  status: 'Active' | 'Inactive' | 'OutOfStock';
  barcode: string | null;
}

export interface UpsertSellerProductVariantRevisionRequest {
  sellerReason: string | null;
  items: UpsertSellerProductVariantRevisionItemRequest[];
}

export interface UpsertSellerProductVariantRevisionItemRequest {
  operation: 'Add' | 'Update' | 'Deactivate';
  sourceVariantId: string | null;
  sku: string | null;
  size: string | null;
  colour: string | null;
  price: number | null;
  compareAtPrice: number | null;
  initialStockQuantity: number | null;
  barcode: string | null;
}

export interface BulkStageSellerProductVariantRevisionRequest {
  sellerReason: string | null;
  items: UpsertSellerProductVariantRevisionItemRequest[];
}

export interface SellerProductVariantRevisionBulkImportResponse {
  totalRows: number;
  validRows: number;
  errorRows: number;
  changedRows: number;
  unchangedRows: number;
  rows: SellerProductVariantRevisionBulkImportRowResponse[];
  proposedFinalVariants: SellerProductVariantRevisionFinalVariantResponse[];
}

export interface SellerProductVariantRevisionBulkImportRowResponse {
  rowNumber: number;
  operation: 'Add' | 'Update' | 'Deactivate' | string;
  sourceVariantId: string | null;
  currentSku: string | null;
  currentSize: string | null;
  currentColour: string | null;
  currentPrice: number | null;
  currentCompareAtPrice: number | null;
  currentStatus: string | null;
  currentBarcode: string | null;
  proposedSku: string | null;
  proposedSize: string | null;
  proposedColour: string | null;
  proposedPrice: number | null;
  proposedCompareAtPrice: number | null;
  proposedInitialStockQuantity: number | null;
  proposedBarcode: string | null;
  rowStatus: 'Changed' | 'Unchanged' | 'Error' | string;
  validationMessages: string[];
}

export interface AttachSellerProductImageRequest {
  storageKey: string;
  url: string | null;
  altText: string | null;
  sortOrder: number;
  isPrimary: boolean;
}

export interface UpdateSellerProductImageRequest {
  altText: string | null;
  sortOrder: number;
  isPrimary: boolean;
}

export interface GenerateSellerAiSuggestionRequest {
  sellerNotes: string | null;
  productTypeHint: string | null;
  selectedCategoryId: string | null;
  knownAttributes: Record<string, unknown>;
  imageIds: string[];
}

export interface SellerAiSuggestionResponse {
  suggestionId: string;
  recommendedTitle: string | null;
  titleSuggestions: string[];
  shortDescription: string | null;
  fullDescription: string | null;
  suggestedCategoryId: string | null;
  suggestedCategoryPath: string | null;
  attributes: Record<string, unknown>;
  tags: string[];
  seo: Record<string, unknown>;
  imageAltText: Record<string, string | null>;
  missingFields: string[];
  riskFlags: string[];
  qualityScore: number;
}

export interface ApplySellerAiSuggestionRequest {
  fieldsToApply: string[];
  editedValues: Record<string, unknown>;
  confirmRiskFlags: boolean;
}
