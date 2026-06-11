import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';
import { environment } from '../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { BuyerEngagementService } from './buyer-engagement.service';
import {
  BuyerNotificationRealtimeService,
  NOTIFICATION_HUB_CONNECTION_FACTORY,
  NotificationHubConnectionFactory
} from './buyer-notification-realtime.service';

describe('BuyerNotificationRealtimeService', () => {
  let initialized: ReturnType<typeof signal<boolean>>;
  let authenticated: ReturnType<typeof signal<boolean>>;
  let roles: ReturnType<typeof signal<string[]>>;
  let accessToken: ReturnType<typeof signal<string | null>>;
  let connection: FakeHubConnection;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;
  let factory: jasmine.Spy<NotificationHubConnectionFactory>;

  beforeEach(() => {
    initialized = signal(false);
    authenticated = signal(false);
    roles = signal<string[]>([]);
    accessToken = signal<string | null>(null);
    connection = new FakeHubConnection();
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['getUnreadNotificationCount']);
    engagementService.getUnreadNotificationCount.and.resolveTo({ unreadCount: 2 });
    factory = jasmine.createSpy('notificationHubConnectionFactory')
      .and.returnValue(connection as unknown as HubConnection);

    const authService = {
      isInitialized: initialized,
      isAuthenticated: authenticated,
      hasAnyRole: (allowedRoles: readonly string[]) => allowedRoles.some(role => roles().includes(role)),
      get accessToken(): string | null {
        return accessToken();
      }
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: BuyerEngagementService, useValue: engagementService },
        { provide: NOTIFICATION_HUB_CONNECTION_FACTORY, useValue: factory }
      ]
    });
  });

  it('starts only for authenticated buyers and refreshes unread count', async () => {
    const service = TestBed.inject(BuyerNotificationRealtimeService);
    await settle();
    expect(factory).not.toHaveBeenCalled();

    initialized.set(true);
    authenticated.set(true);
    roles.set(['Buyer']);
    accessToken.set('access-token');
    await settle();

    expect(factory).toHaveBeenCalledOnceWith(
      `${environment.apiBaseUrl}/hubs/notifications`,
      jasmine.any(Function));
    expect(connection.start).toHaveBeenCalled();
    expect(service.isConnected()).toBeTrue();
    expect(service.unreadCount()).toBe(2);
  });

  it('updates signals from hub events', async () => {
    const service = TestBed.inject(BuyerNotificationRealtimeService);
    initialized.set(true);
    authenticated.set(true);
    roles.set(['Buyer']);
    accessToken.set('access-token');
    await settle();

    connection.emit('notificationCreated', createNotification());
    expect(service.latestNotification()?.notificationId).toBe('notification-id');
    expect(service.unreadCount()).toBe(3);

    engagementService.getUnreadNotificationCount.and.resolveTo({ unreadCount: 1 });
    connection.emit('notificationRead', { notificationId: 'notification-id', readAtUtc: '2026-05-21T10:05:00Z' });
    await settle();
    expect(service.latestReadEvent()?.notificationId).toBe('notification-id');
    expect(service.unreadCount()).toBe(1);

    connection.emit('notificationsReadAll', { readAtUtc: '2026-05-21T10:06:00Z', updatedCount: 1 });
    expect(service.latestReadAllEvent()?.updatedCount).toBe(1);
    expect(service.unreadCount()).toBe(0);
  });

  it('stops when the buyer session ends', async () => {
    const service = TestBed.inject(BuyerNotificationRealtimeService);
    initialized.set(true);
    authenticated.set(true);
    roles.set(['Buyer']);
    accessToken.set('access-token');
    await settle();

    authenticated.set(false);
    roles.set([]);
    accessToken.set(null);
    await settle();

    expect(connection.stop).toHaveBeenCalled();
    expect(service.isConnected()).toBeFalse();
    expect(service.unreadCount()).toBe(0);
  });
});

class FakeHubConnection {
  readonly start = jasmine.createSpy('start').and.resolveTo();
  readonly stop = jasmine.createSpy('stop').and.resolveTo();
  private readonly handlers = new Map<string, (...args: unknown[]) => void>();

  on(methodName: string, handler: (...args: unknown[]) => void): void {
    this.handlers.set(methodName, handler);
  }

  onreconnected(): void {
  }

  emit(methodName: string, payload: unknown): void {
    this.handlers.get(methodName)?.(payload);
  }
}

function createNotification() {
  return {
    notificationId: 'notification-id',
    recipientUserId: 'buyer-user-id',
    type: 'OrderShipped',
    title: 'Order shipped',
    message: 'Your order is on the way.',
    relatedEntityType: 'Order',
    relatedEntityId: 'order-id',
    readAtUtc: null,
    createdAtUtc: '2026-05-21T10:00:00Z'
  };
}

async function settle(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => setTimeout(resolve));
}
