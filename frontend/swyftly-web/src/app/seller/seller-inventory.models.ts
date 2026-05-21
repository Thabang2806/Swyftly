export type SellerInventoryVariantStatus = 'Active' | 'Inactive' | 'OutOfStock';

export interface SellerInventoryItemResponse {
  productId: string;
  variantId: string;
  productTitle: string | null;
  productSlug: string | null;
  productStatus: string;
  primaryImageUrl: string | null;
  primaryImageAltText: string | null;
  sku: string;
  size: string;
  colour: string;
  price: number;
  stockQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  variantStatus: SellerInventoryVariantStatus;
  updatedAtUtc: string;
}

export interface AdjustSellerInventoryRequest {
  stockQuantity: number;
  status: SellerInventoryVariantStatus;
  reason: string;
}

export interface BulkAdjustSellerInventoryRequest {
  reason: string;
  items: BulkAdjustSellerInventoryItemRequest[];
}

export interface BulkAdjustSellerInventoryItemRequest {
  variantId: string | null;
  sku: string | null;
  stockQuantity: number;
  status: SellerInventoryVariantStatus;
}

export type SellerInventoryImportRowStatus = 'Changed' | 'Unchanged' | 'Error';

export interface SellerInventoryBulkAdjustmentResponse {
  totalRows: number;
  validRows: number;
  errorRows: number;
  changedRows: number;
  unchangedRows: number;
  rows: SellerInventoryBulkAdjustmentRowResponse[];
}

export interface SellerInventoryBulkAdjustmentRowResponse {
  rowNumber: number;
  variantId: string | null;
  sku: string | null;
  productId: string | null;
  productTitle: string | null;
  productSlug: string | null;
  size: string | null;
  colour: string | null;
  currentStockQuantity: number | null;
  currentReservedQuantity: number | null;
  currentStatus: SellerInventoryVariantStatus | null;
  proposedStockQuantity: number | null;
  proposedStatus: SellerInventoryVariantStatus | string | null;
  rowStatus: SellerInventoryImportRowStatus;
  messages: string[];
}
