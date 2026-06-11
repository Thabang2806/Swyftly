export type BuyerGrowthEventType =
  | 'AssistantSearchSubmitted'
  | 'AssistantProductOpened'
  | 'AssistantShopHandoff'
  | 'AssistantFeedbackSubmitted'
  | 'VisualSearchSubmitted'
  | 'VisualProductOpened'
  | 'VisualShopHandoff'
  | 'VisualFeedbackSubmitted';

export type BuyerGrowthSourceTool = 'Assistant' | 'VisualSearch';

export type BuyerGrowthConfidenceBand = 'High' | 'Medium' | 'Low';

export type BuyerGrowthFeedbackReason =
  | 'GoodMatches'
  | 'TooBroad'
  | 'WrongStyle'
  | 'WrongCategory'
  | 'Unavailable'
  | 'LowConfidence';

export interface BuyerGrowthEventRequest {
  eventType: BuyerGrowthEventType;
  sourceTool: BuyerGrowthSourceTool;
  productId?: string | null;
  resultCount?: number | null;
  confidenceBand?: BuyerGrowthConfidenceBand | null;
  category?: string | null;
  colour?: string | null;
  material?: string | null;
  sourceRoute?: string | null;
  feedbackReason?: BuyerGrowthFeedbackReason | null;
}

export interface BuyerGrowthEventResponse {
  eventId: string;
  status: string;
}
