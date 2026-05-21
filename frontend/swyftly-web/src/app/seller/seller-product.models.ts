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
  tags: string[];
  status: string;
  rejectionReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  publishedAtUtc: string | null;
  attributes: Record<string, string>;
  variants: SellerProductVariantResponse[];
  images: SellerProductImageResponse[];
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
  tags: string[];
  attributes: Record<string, string>;
  images: SellerProductRevisionImageResponse[];
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

export interface UpsertSellerProductRequest {
  categoryId: string | null;
  brandId: string | null;
  title: string | null;
  slug: string | null;
  shortDescription: string | null;
  fullDescription: string | null;
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
