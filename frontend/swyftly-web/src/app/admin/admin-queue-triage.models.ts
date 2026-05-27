export type AdminQueueItemType = 'Seller' | 'Product' | 'ListingRevision' | 'VariantRevision' | 'AdCampaign';
export type AdminQueuePriority = 'Normal' | 'High' | 'Urgent';
export type AdminQueueBulkTriageAction = 'SetPriority' | 'AddNote' | 'Claim' | 'Unclaim';

export interface AdminQueueTriageFields {
  assignedToUserId?: string | null;
  assignedToDisplayName?: string | null;
  priority?: AdminQueuePriority | string;
  latestTriageNote?: string | null;
  triageNoteCount?: number;
  triageUpdatedAtUtc?: string | null;
  ageHours?: number;
  slaStatus?: 'OnTrack' | 'DueSoon' | 'Overdue' | string;
  slaDueAtUtc?: string | null;
}

export interface AdminQueueTriageResponse extends AdminQueueTriageFields {
  itemType: AdminQueueItemType | string;
  itemId: string;
  notes: AdminQueueTriageNoteResponse[];
}

export interface AdminQueueTriageNoteResponse {
  noteId: string;
  actorUserId: string;
  actorDisplayName: string | null;
  note: string;
  createdAtUtc: string;
}

export interface AdminQueueTriageUpdateRequest {
  priority?: AdminQueuePriority | string | null;
  note?: string | null;
  assignedToUserId?: string | null;
  clearAssignment?: boolean | null;
}

export interface AdminQueueBulkTriageRequest {
  action: AdminQueueBulkTriageAction;
  priority?: AdminQueuePriority | string | null;
  note?: string | null;
  items: AdminQueueBulkTriageItemRequest[];
}

export interface AdminQueueBulkTriageItemRequest {
  itemType: AdminQueueItemType | string;
  itemId: string;
}

export interface AdminQueueBulkTriageResponse {
  successCount: number;
  errorCount: number;
  results: AdminQueueBulkTriageItemResult[];
}

export interface AdminQueueBulkTriageItemResult {
  itemType: string;
  itemId: string;
  isSuccess: boolean;
  error: string | null;
  triage: AdminQueueTriageResponse | null;
}
