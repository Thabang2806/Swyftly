import { SellerPolicySnapshotResponse } from '../shared/seller-policy.models';

export interface SellerReturnRequestResult {
  returnRequestId: string;
  orderId: string;
  buyerId: string;
  sellerId: string;
  status: string;
  reason: string;
  details: string | null;
  requestedAtUtc: string;
  sellerRespondedAtUtc: string | null;
  sellerResponseReason: string | null;
  disputedAtUtc: string | null;
  disputeReason: string | null;
  items: SellerReturnItemResult[];
  messages: SellerReturnMessageResult[];
  sellerPolicySnapshot: SellerPolicySnapshotResponse | null;
}

export interface SellerReturnItemResult {
  returnItemId: string;
  orderItemId: string;
  productId: string;
  productVariantId: string;
  quantity: number;
  reason: string;
  isOpenedOrUnsealed: boolean;
  note: string | null;
}

export interface SellerReturnMessageResult {
  returnMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  createdAtUtc: string;
}

export interface SellerReturnResponseRequest {
  message: string | null;
}

export interface SellerReturnRestockDecisionResponse {
  restockDecisionId: string;
  returnRequestId: string;
  returnItemId: string;
  productId: string;
  productVariantId: string;
  sku: string;
  size: string;
  colour: string;
  quantityReturned: number;
  quantityRestocked: number;
  condition: SellerReturnRestockCondition;
  reason: string;
  actorUserId: string;
  createdAtUtc: string;
}

export type SellerReturnRestockCondition =
  | 'Sellable'
  | 'Damaged'
  | 'OpenedOrUsed'
  | 'Missing'
  | 'Other';

export interface SellerReturnRestockDecisionRequest {
  items: SellerReturnRestockDecisionItemRequest[];
}

export interface SellerReturnRestockDecisionItemRequest {
  returnItemId: string;
  quantityRestocked: number;
  condition: SellerReturnRestockCondition;
  reason: string;
}
