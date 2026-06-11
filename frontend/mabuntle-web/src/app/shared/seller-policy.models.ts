export interface SellerPolicyResponse {
  returnWindowDays: number | null;
  returnPolicy: string | null;
  exchangePolicy: string | null;
  fulfilmentPolicy: string | null;
  supportPolicy: string | null;
  careInstructions: string | null;
  productDisclaimer: string | null;
  isComplete: boolean;
  missingFields: string[];
  updatedAtUtc: string | null;
}

export interface SellerPolicySnapshotResponse {
  returnWindowDays: number | null;
  returnPolicy: string | null;
  exchangePolicy: string | null;
  fulfilmentPolicy: string | null;
  supportPolicy: string | null;
  careInstructions: string | null;
  productDisclaimer: string | null;
  snapshotAtUtc: string | null;
}

export interface SellerStorePolicyRequest {
  returnWindowDays: number | null;
  returnPolicy: string | null;
  exchangePolicy: string | null;
  fulfilmentPolicy: string | null;
  supportPolicy: string | null;
  careInstructions: string | null;
  productDisclaimer: string | null;
}
