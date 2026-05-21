import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerNotificationsPageComponent } from './buyer-notifications-page.component';

describe('BuyerNotificationsPageComponent', () => {
  let fixture: ComponentFixture<BuyerNotificationsPageComponent>;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;

  beforeEach(async () => {
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['listNotifications', 'markNotificationRead', 'markAllNotificationsRead']);
    engagementService.listNotifications.and.resolveTo([createNotification()]);
    engagementService.markNotificationRead.and.resolveTo({ ...createNotification(), readAtUtc: '2026-05-19T10:05:00Z' });
    engagementService.markAllNotificationsRead.and.resolveTo({ updatedCount: 1 });

    await TestBed.configureTestingModule({
      imports: [BuyerNotificationsPageComponent],
      providers: [
        provideRouter([]),
        { provide: BuyerEngagementService, useValue: engagementService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerNotificationsPageComponent);
  });

  it('lists notifications and marks one read', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Order shipped');
    expect(compiled.textContent).toContain('unread notification');
    expect(compiled.textContent).toContain('Notification settings');

    const readButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Mark read')) as HTMLButtonElement;
    readButton.click();
    await fixture.whenStable();

    expect(engagementService.markNotificationRead).toHaveBeenCalledWith('notification-id');
  });

  it('marks all notifications read', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const allButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Mark all read')) as HTMLButtonElement;
    allButton.click();
    await fixture.whenStable();

    expect(engagementService.markAllNotificationsRead).toHaveBeenCalled();
  });
});

function createNotification() {
  return {
    notificationId: 'notification-id',
    recipientUserId: 'buyer-user-id',
    type: 'OrderUpdate',
    title: 'Order shipped',
    message: 'Your order has shipped.',
    relatedEntityType: 'Order',
    relatedEntityId: 'order-id',
    readAtUtc: null,
    createdAtUtc: '2026-05-19T10:00:00Z'
  };
}
