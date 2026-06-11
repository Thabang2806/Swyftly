export interface AdminAuditLogSearchResponse {
  items: AdminAuditLogDetailResponse[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface AdminAuditLogDetailResponse {
  id: string;
  actorUserId: string | null;
  actorRole: string | null;
  actionType: string;
  entityType: string;
  entityId: string | null;
  previousValueJson: string | null;
  newValueJson: string | null;
  reason: string | null;
  ipAddress: string | null;
  createdAtUtc: string;
}

export interface AdminAuditLogSearchRequest {
  actionType?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  actorUserId?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  pageNumber?: number | null;
  pageSize?: number | null;
}
