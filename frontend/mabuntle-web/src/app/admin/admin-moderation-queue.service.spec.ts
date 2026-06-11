import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminModerationQueueService } from './admin-moderation-queue.service';

describe('AdminModerationQueueService', () => {
  let service: AdminModerationQueueService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminModerationQueueService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads saved views for a queue', async () => {
    const promise = service.getSavedViews('Products');
    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/moderation-queue/views?queue=Products`);
    expect(request.request.method).toBe('GET');
    request.flush([createView()]);

    const response = await promise;
    expect(response[0].name).toBe('Product review');
  });

  it('creates and defaults saved views', async () => {
    const promise = service.createSavedView({
      queue: 'Sellers',
      name: 'Urgent sellers',
      isDefault: false,
      filters: { view: 'NeedsAttention', sla: 'Overdue', pageSize: 25 }
    });
    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/moderation-queue/views`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.filters.sla).toBe('Overdue');
    request.flush({ ...createView(), name: 'Urgent sellers' });
    expect((await promise).name).toBe('Urgent sellers');

    const defaultPromise = service.makeDefault('view-id');
    const defaultRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/moderation-queue/views/view-id/make-default`);
    expect(defaultRequest.request.method).toBe('POST');
    defaultRequest.flush({ ...createView(), isDefault: true });
    expect((await defaultPromise).isDefault).toBeTrue();
  });

  it('loads queue summary', async () => {
    const promise = service.getSummary();
    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/admin/moderation-queue/summary`);
    expect(request.request.method).toBe('GET');
    request.flush({
      generatedAtUtc: '2026-05-27T12:00:00Z',
      itemTypeCounts: [],
      statusCounts: [],
      priorityCounts: [],
      slaCounts: [{ key: 'Overdue', count: 2 }],
      assigneeCounts: [],
      reviewedToday: 1,
      reviewedLast7Days: 4,
      averageReviewHours: 3.5
    });

    const response = await promise;
    expect(response.reviewedLast7Days).toBe(4);
    expect(response.slaCounts[0].key).toBe('Overdue');
  });
});

function createView() {
  return {
    viewId: 'view-id',
    queue: 'Products',
    name: 'Product review',
    isDefault: false,
    filters: {
      view: 'NeedsAttention',
      status: null,
      search: null,
      sellerId: null,
      assigned: 'Any',
      priority: null,
      hasNotes: null,
      sla: 'Overdue',
      sort: 'UpdatedDesc',
      pageSize: 25
    },
    createdAtUtc: '2026-05-27T12:00:00Z',
    updatedAtUtc: '2026-05-27T12:00:00Z'
  };
}
