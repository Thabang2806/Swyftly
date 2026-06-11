import { AdminOperationalView } from './admin-operational-list.models';

export type AdminQueueKey = 'Sellers' | 'Products' | 'Ads' | 'Support';
export type AdminQueueSlaStatus = 'OnTrack' | 'DueSoon' | 'Overdue';

export interface AdminQueueSavedViewFilters {
  view?: AdminOperationalView | string | null;
  status?: string | null;
  category?: string | null;
  search?: string | null;
  sellerId?: string | null;
  assigned?: string | null;
  priority?: string | null;
  hasNotes?: boolean | null;
  sla?: AdminQueueSlaStatus | string | null;
  sort?: string | null;
  pageSize?: number | null;
}

export interface AdminQueueSavedViewResponse {
  viewId: string;
  queue: AdminQueueKey | string;
  name: string;
  isDefault: boolean;
  filters: AdminQueueSavedViewFilters;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminQueueSavedViewRequest {
  queue: AdminQueueKey | string;
  name: string;
  isDefault?: boolean | null;
  filters: AdminQueueSavedViewFilters;
}

export interface AdminQueueSummaryResponse {
  generatedAtUtc: string;
  itemTypeCounts: AdminQueueCountResponse[];
  statusCounts: AdminQueueCountResponse[];
  priorityCounts: AdminQueueCountResponse[];
  slaCounts: AdminQueueCountResponse[];
  assigneeCounts: AdminQueueAssigneeCountResponse[];
  reviewedToday: number;
  reviewedLast7Days: number;
  averageReviewHours: number | null;
}

export interface AdminQueueCountResponse {
  key: string;
  count: number;
}

export interface AdminQueueAssigneeCountResponse {
  assignedToUserId: string;
  assignedToDisplayName: string | null;
  count: number;
}
