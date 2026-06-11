import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WritableSignal, signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { SellerNotificationRealtimeService } from '../seller/seller-notification-realtime.service';
import { SellerNotificationService } from '../seller/seller-notification.service';
import { SellerNotificationsPageComponent } from './seller-notifications-page.component';

describe('SellerNotificationsPageComponent', () => {
  let fixture: ComponentFixture<SellerNotificationsPageComponent>;
  let notificationService: {
    unreadCount: WritableSignal<number>;
    listNotifications: jasmine.Spy;
    markRead: jasmine.Spy;
    markAllRead: jasmine.Spy;
    refreshUnreadCount: jasmine.Spy;
  };
  let notificationRealtime: {
    latestNotification: WritableSignal<ReturnType<typeof createNotification> | null>;
    latestReadEvent: WritableSignal<{ notificationId: string; readAtUtc: string } | null>;
    latestReadAllEvent: WritableSignal<{ readAtUtc: string; updatedCount: number } | null>;
    refreshUnreadCount: jasmine.Spy;
  };

  beforeEach(async () => {
    notificationService = {
      unreadCount: signal(1),
      listNotifications: jasmine.createSpy('listNotifications').and.resolveTo([createNotification()]),
      markRead: jasmine.createSpy('markRead').and.resolveTo({ ...createNotification(), readAtUtc: '2026-05-26T10:05:00Z' }),
      markAllRead: jasmine.createSpy('markAllRead').and.resolveTo({ updatedCount: 1 }),
      refreshUnreadCount: jasmine.createSpy('refreshUnreadCount').and.resolveTo()
    };
    notificationRealtime = {
      latestNotification: signal<ReturnType<typeof createNotification> | null>(null),
      latestReadEvent: signal<{ notificationId: string; readAtUtc: string } | null>(null),
      latestReadAllEvent: signal<{ readAtUtc: string; updatedCount: number } | null>(null),
      refreshUnreadCount: jasmine.createSpy('refreshUnreadCount').and.resolveTo()
    };

    await TestBed.configureTestingModule({
      imports: [SellerNotificationsPageComponent],
      providers: [
        provideRouter([]),
        { provide: SellerNotificationService, useValue: notificationService },
        { provide: SellerNotificationRealtimeService, useValue: notificationRealtime }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerNotificationsPageComponent);
  });

  it('lists seller notifications and marks one read', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Product approved');
    expect(compiled.querySelector('a[href="/products/product-id/edit"]')).not.toBeNull();

    const button = Array.from(compiled.querySelectorAll('button')).find(item => item.textContent?.includes('Mark read')) as HTMLButtonElement;
    button.click();
    await fixture.whenStable();

    expect(notificationService.markRead).toHaveBeenCalledWith('notification-id');
    expect(notificationRealtime.refreshUnreadCount).toHaveBeenCalled();
  });

  it('marks all seller notifications read', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find(item => item.textContent?.includes('Mark all read')) as HTMLButtonElement;
    button.click();
    await fixture.whenStable();

    expect(notificationService.markAllRead).toHaveBeenCalled();
  });

  it('prepends live notifications and applies realtime read sync', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    notificationRealtime.latestNotification.set({
      ...createNotification(),
      notificationId: 'live-notification-id',
      title: 'Ad approved',
      relatedEntityType: 'AdCampaign',
      relatedEntityId: 'ad-id'
    });
    fixture.detectChanges();
    await fixture.whenStable();
    await settle();
    fixture.detectChanges();

    let liveCard = findNotificationCard(fixture, 'Ad approved');
    expect(liveCard).not.toBeNull();
    expect(liveCard!.querySelector('a[href="/ads/ad-id"]')).not.toBeNull();

    notificationRealtime.latestReadEvent.set({
      notificationId: 'live-notification-id',
      readAtUtc: '2026-05-26T10:20:00Z'
    });
    fixture.detectChanges();
    await fixture.whenStable();
    await settle();
    fixture.detectChanges();

    liveCard = findNotificationCard(fixture, 'Ad approved');
    expect(liveCard?.classList.contains('unread')).toBeFalse();
  });
});

function findNotificationCard(
  fixture: ComponentFixture<SellerNotificationsPageComponent>,
  title: string
): HTMLElement | null {
  return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.buyer-notification-card'))
    .find(card => card.textContent?.includes(title)) ?? null;
}

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

async function settle(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => setTimeout(resolve));
}
