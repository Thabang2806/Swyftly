import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminAuditLogService } from './admin-audit-log.service';

describe('AdminAuditLogService', () => {
  let service: AdminAuditLogService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminAuditLogService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('searches audit logs with filters', async () => {
    const promise = service.search({
      entityType: 'Product',
      actionType: 'ProductApproved',
      pageSize: 25
    });

    const request = httpTestingController.expectOne(match =>
      match.url === `${environment.apiBaseUrl}/api/admin/audit-logs` &&
      match.params.get('entityType') === 'Product' &&
      match.params.get('actionType') === 'ProductApproved' &&
      match.params.get('pageSize') === '25');
    expect(request.request.method).toBe('GET');
    request.flush({
      items: [createAuditLog()],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1
    });

    const response = await promise;
    expect(response.items[0].actionType).toBe('ProductApproved');
  });
});

function createAuditLog() {
  return {
    id: 'audit-id',
    actorUserId: 'admin-id',
    actorRole: 'Admin',
    actionType: 'ProductApproved',
    entityType: 'Product',
    entityId: 'product-id',
    previousValueJson: '{"status":"PendingReview"}',
    newValueJson: '{"status":"Published"}',
    reason: 'Manual review complete.',
    ipAddress: '127.0.0.1',
    createdAtUtc: '2026-05-18T12:00:00Z'
  };
}
