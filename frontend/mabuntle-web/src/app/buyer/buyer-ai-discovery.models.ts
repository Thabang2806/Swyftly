export type BuyerAiDiscoverySourceTool = 'Assistant' | 'VisualSearch';

export interface BuyerAiDiscoveryPreferenceRequest {
  historyEnabled: boolean;
  personalizationEnabled?: boolean;
}

export interface BuyerAiDiscoveryPreferenceResponse {
  historyEnabled: boolean;
  personalizationEnabled: boolean;
  updatedAtUtc: string | null;
}

export interface BuyerAiDiscoveryHistoryListResponse {
  items: BuyerAiDiscoveryHistoryResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface BuyerAiDiscoveryHistoryResponse {
  historyId: string;
  sourceTool: BuyerAiDiscoverySourceTool;
  category: string | null;
  colour: string | null;
  material: string | null;
  confidenceBand: 'High' | 'Medium' | 'Low' | null;
  resultCount: number;
  productIds: string[];
  products: BuyerAiDiscoveryHistoryProductResponse[];
  sourceRoute: string | null;
  createdAtUtc: string;
}

export interface BuyerAiDiscoveryHistoryProductResponse {
  productId: string;
  title: string;
  slug: string;
}

export interface BuyerAiDiscoveryHistoryQuery {
  page?: number;
  pageSize?: number;
  tool?: BuyerAiDiscoverySourceTool | null;
}
