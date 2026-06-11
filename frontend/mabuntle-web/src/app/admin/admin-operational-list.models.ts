export type AdminOperationalView = 'NeedsAttention' | 'All';

export interface AdminOperationalListQuery {
  view?: AdminOperationalView;
  status?: string | null;
  search?: string | null;
  sellerId?: string | null;
  assigned?: 'Any' | 'Mine' | 'Unassigned' | string | null;
  priority?: 'Normal' | 'High' | 'Urgent' | string | null;
  hasNotes?: boolean | null;
  sla?: 'OnTrack' | 'DueSoon' | 'Overdue' | string | null;
  savedViewId?: string | null;
  page?: number;
  pageSize?: number;
  sort?: string | null;
}

export interface AdminStatusCountResponse {
  status: string;
  count: number;
}

export interface AdminPagedResponse<TItem> {
  items: TItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  statusCounts: AdminStatusCountResponse[];
}
