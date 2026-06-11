export interface BuyerVisualSearchRequest {
  imageReference: string | null;
  imageDataBase64: string | null;
  fileName: string | null;
  contentType: string | null;
}

export interface BuyerVisualSearchResponse {
  attributes: VisualSearchAttributes;
  products: BuyerVisualSearchProductCardResponse[];
  summary: string;
  imageRetentionNote: string;
}

export interface VisualSearchAttributes {
  category: string | null;
  colour: string | null;
  style: string | null;
  shape: string | null;
  pattern: string | null;
  materialGuess: string | null;
  materialConfidence: number | null;
  confidence: number;
  searchText: string;
  warnings: string[];
}

export interface BuyerVisualSearchProductCardResponse {
  productId: string;
  title: string;
  slug: string;
  sellerDisplayName: string | null;
  imageUrl: string | null;
  price: number;
  currency: string;
  matchReasons: string[];
  personalizationApplied: boolean;
  personalizationReasons: string[];
}
