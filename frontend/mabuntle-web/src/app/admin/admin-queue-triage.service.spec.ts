import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminQueueTriageService } from './admin-queue-triage.service';

describe('AdminQueueTriageService', () => {
  let service: AdminQueueTriageService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminQueueTriageService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls item triage endpoints', async () => {
    const claimPromise = service.claim('Product', 'product-id');
    const claimRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/moderation-queue/items/Product/product-id/claim`);
    expect(claimRequest.request.method).toBe('POST');
    claimRequest.flush(createTriage());
    expect((await claimPromise).priority).toBe('High');

    const updatePromise = service.updateTriage('Product', 'product-id', { priority: 'Urgent', note: 'Review imagery.' });
    const updateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/moderation-queue/items/Product/product-id/triage`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body.priority).toBe('Urgent');
    expect(updateRequest.request.body.note).toBe('Review imagery.');
    updateRequest.flush({ ...createTriage(), priority: 'Urgent' });
    expect((await updatePromise).priority).toBe('Urgent');

    const unclaimPromise = service.unclaim('Product', 'product-id');
    const unclaimRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/moderation-queue/items/Product/product-id/unclaim`);
    expect(unclaimRequest.request.method).toBe('POST');
    unclaimRequest.flush({ ...createTriage(), assignedToUserId: null, assignedToDisplayName: null });
    expect((await unclaimPromise).assignedToUserId).toBeNull();
  });

  it('calls bulk triage endpoint', async () => {
    const promise = service.bulkTriage({
      action: 'SetPriority',
      priority: 'High',
      items: [{ itemType: 'Seller', itemId: 'seller-id' }]
    });

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/moderation-queue/bulk-triage`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.action).toBe('SetPriority');
    expect(request.request.body.items[0].itemType).toBe('Seller');
    request.flush({ successCount: 1, errorCount: 0, results: [] });

    const response = await promise;
    expect(response.successCount).toBe(1);
  });
});

function createTriage() {
  return {
    itemType: 'Product',
    itemId: 'product-id',
    assignedToUserId: 'admin-id',
    assignedToDisplayName: 'admin@example.test',
    priority: 'High',
    latestTriageNote: 'Review imagery.',
    triageNoteCount: 1,
    triageUpdatedAtUtc: '2026-05-27T12:00:00Z',
    notes: []
  };
}
