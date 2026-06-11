export interface SellerBalanceResponse {
  sellerId: string;
  balances: SellerCurrencyBalanceResponse[];
}

export interface SellerCurrencyBalanceResponse {
  currency: string;
  pendingBalance: number;
  availableBalance: number;
  heldBalance: number;
}

export interface SellerPayoutResponse {
  payoutId: string;
  sellerId: string;
  amount: number;
  currency: string;
  status: string;
  createdAtUtc: string;
  heldAtUtc: string | null;
  holdReason: string | null;
  releasedAtUtc: string | null;
  releaseReason: string | null;
  items: SellerPayoutItemResponse[];
}

export interface SellerPayoutItemResponse {
  amount: number;
  currency: string;
  createdAtUtc: string;
  sourceType: string;
}
