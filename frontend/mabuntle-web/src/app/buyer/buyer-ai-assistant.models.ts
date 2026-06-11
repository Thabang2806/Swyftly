export interface BuyerAiShoppingAssistantRequest {
  message: string;
}

export interface BuyerAiShoppingAssistantResponse {
  intent: ShoppingIntent;
  products: BuyerAiProductCardResponse[];
  summary: string;
  safetyNote: string | null;
}

export interface ShoppingIntent {
  category: string | null;
  subcategory: string | null;
  budgetMax: number | null;
  budgetMin: number | null;
  size: string | null;
  colour: string | null;
  occasion: string | null;
  style: string | null;
  material: string | null;
  brand: string | null;
  beautySkinType: string | null;
  beautyConcern: string | null;
  searchText: string;
  isVague: boolean;
  clarificationPrompt: string | null;
}

export interface BuyerAiProductCardResponse {
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
