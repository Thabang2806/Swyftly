import { SellerPolicySnapshotResponse } from '../shared/seller-policy.models';

export interface BuyerReturnRequestResult {
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
  items: BuyerReturnItemResult[];
  messages: BuyerReturnMessageResult[];
  sellerPolicySnapshot: SellerPolicySnapshotResponse | null;
}

export interface BuyerReturnItemResult {
  returnItemId: string;
  orderItemId: string;
  productId: string;
  productVariantId: string;
  quantity: number;
  reason: string;
  isOpenedOrUnsealed: boolean;
  note: string | null;
}

export interface BuyerReturnMessageResult {
  returnMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  createdAtUtc: string;
}

export interface CreateBuyerReturnRequest {
  reason: string;
  details: string | null;
  items: CreateBuyerReturnItemRequest[];
}

export interface CreateBuyerReturnItemRequest {
  orderItemId: string;
  quantity: number;
  reason: string;
  isOpenedOrUnsealed: boolean;
  note: string | null;
}

export interface DisputeBuyerReturnRequest {
  reason: string;
}
