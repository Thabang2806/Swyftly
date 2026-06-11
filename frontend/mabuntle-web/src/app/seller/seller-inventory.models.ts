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
  barcode: string | null;
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
  barcode?: string | null;
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
  barcode: string | null;
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

export type SellerInventoryMovementType =
  | 'SellerAdjustment'
  | 'BulkImportAdjustment'
  | 'ReservationCreated'
  | 'ReservationReleased'
  | 'ReservationExpired'
  | 'ReservationConfirmed'
  | 'PaymentFailedReservationReleased'
  | 'ReturnRequested'
  | 'RefundCompleted'
  | 'ReturnRestocked';

export interface SellerInventoryHistoryFilters {
  productId?: string | null;
  variantId?: string | null;
  sku?: string | null;
  barcode?: string | null;
  movementType?: SellerInventoryMovementType | null;
  orderId?: string | null;
  cartId?: string | null;
  reservationId?: string | null;
  paymentId?: string | null;
  returnRequestId?: string | null;
  refundId?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface SellerInventoryMovementResponse {
  movementId: string;
  productId: string;
  variantId: string;
  productTitle: string;
  productSlug: string;
  sku: string;
  barcode: string | null;
  size: string;
  colour: string;
  movementType: SellerInventoryMovementType;
  stockQuantityBefore: number;
  stockQuantityAfter: number;
  reservedQuantityBefore: number;
  reservedQuantityAfter: number;
  quantityDelta: number;
  reservedQuantityDelta: number;
  statusBefore: SellerInventoryVariantStatus;
  statusAfter: SellerInventoryVariantStatus;
  source: string;
  reason: string;
  actorUserId: string | null;
  batchReference: string | null;
  cartId: string | null;
  orderId: string | null;
  reservationId: string | null;
  paymentId: string | null;
  returnRequestId: string | null;
  refundId: string | null;
  relatedRoute: string | null;
  occurredAtUtc: string;
}
