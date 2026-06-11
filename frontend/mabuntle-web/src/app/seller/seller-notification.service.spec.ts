import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { SellerNotificationService } from './seller-notification.service';

describe('SellerNotificationService', () => {
  let service: SellerNotificationService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(SellerNotificationService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('calls seller notification endpoints', async () => {
    const listPromise = service.listNotifications();
    const listRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/notifications`);
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([createNotification()]);
    expect(await listPromise).toEqual([createNotification()]);

    const countPromise = service.refreshUnreadCount();
    const countRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/notifications/unread-count`);
    expect(countRequest.request.method).toBe('GET');
    countRequest.flush({ unreadCount: 3 });
    await countPromise;
    expect(service.unreadCount()).toBe(3);

    const readPromise = service.markRead('notification-id');
    const readRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/notifications/notification-id/read`);
    expect(readRequest.request.method).toBe('POST');
    readRequest.flush({ ...createNotification(), readAtUtc: '2026-05-26T10:10:00Z' });
    expect((await readPromise).readAtUtc).toBe('2026-05-26T10:10:00Z');

    const readAllPromise = service.markAllRead();
    const readAllRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/notifications/read-all`);
    expect(readAllRequest.request.method).toBe('POST');
    readAllRequest.flush({ updatedCount: 1 });
    expect(await readAllPromise).toEqual({ updatedCount: 1 });

    const preferencesPromise = service.getPreferences();
    const preferencesRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/notification-preferences`);
    expect(preferencesRequest.request.method).toBe('GET');
    preferencesRequest.flush({ preferences: [{ category: 'Products', isEnabled: true, emailEnabled: false }] });
    expect((await preferencesPromise).preferences[0].category).toBe('Products');

    const updatePromise = service.updatePreferences({
      preferences: [{ category: 'Products', isEnabled: true, emailEnabled: true }]
    });
    const updateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/notification-preferences`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body.preferences[0]).toEqual({ category: 'Products', isEnabled: true, emailEnabled: true });
    updateRequest.flush({ preferences: [{ category: 'Products', isEnabled: true, emailEnabled: true }] });
    expect((await updatePromise).preferences[0].emailEnabled).toBeTrue();
  });
});

function createNotification() {
  return {
    notificationId: 'notification-id',
    recipientUserId: 'seller-user-id',
    type: 'ProductApproved',
    title: 'Product approved',
    message: 'Your product was approved.',
    relatedEntityType: 'Product',
    relatedEntityId: 'product-id',
    readAtUtc: null,
    createdAtUtc: '2026-05-26T10:00:00Z'
  };
}
