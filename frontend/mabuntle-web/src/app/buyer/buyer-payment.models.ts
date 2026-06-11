export interface PaymentInitiationResponse {
  paymentId: string;
  orderId: string;
  provider: string;
  providerReference: string | null;
  amount: number;
  currency: string;
  status: string;
  checkoutUrl: string | null;
}

