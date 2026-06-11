import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, effect, inject, signal } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';
import { environment } from '../../environments/environment';
import { AuthService } from '../auth/auth.service';
import {
  NOTIFICATION_HUB_CONNECTION_FACTORY,
  NotificationHubConnectionFactory
} from '../buyer/buyer-notification-realtime.service';
import {
  SellerNotificationReadRealtimeEvent,
  SellerNotificationResponse,
  SellerNotificationsReadAllRealtimeEvent
} from './seller-notification.models';
import { SellerNotificationService } from './seller-notification.service';

@Injectable({ providedIn: 'root' })
export class SellerNotificationRealtimeService {
  private readonly authService = inject(AuthService);
  private readonly notificationService = inject(SellerNotificationService);
  private readonly connectionFactory = inject<NotificationHubConnectionFactory>(NOTIFICATION_HUB_CONNECTION_FACTORY);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private connection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;
  private connectionToken: string | null = null;

  readonly unreadCount = signal(0);
  readonly isConnected = signal(false);
  readonly latestNotification = signal<SellerNotificationResponse | null>(null);
  readonly latestReadEvent = signal<SellerNotificationReadRealtimeEvent | null>(null);
  readonly latestReadAllEvent = signal<SellerNotificationsReadAllRealtimeEvent | null>(null);

  constructor() {
    if (!this.isBrowser) {
      return;
    }

    effect(() => {
      const isInitialized = this.authService.isInitialized();
      const isSeller = this.authService.hasAnyRole(['Seller']);
      const isAuthenticated = this.authService.isAuthenticated();
      const accessToken = this.authService.accessToken;

      queueMicrotask(() => {
        if (isInitialized && isAuthenticated && isSeller && accessToken) {
          void this.ensureStarted(accessToken);
        } else {
          void this.stop();
        }
      });
    });
  }

  async refreshUnreadCount(): Promise<void> {
    if (!this.isBrowser || !this.authService.hasAnyRole(['Seller']) || !this.authService.isAuthenticated()) {
      this.setUnreadCount(0);
      return;
    }

    try {
      const result = await this.notificationService.getUnreadCount();
      this.setUnreadCount(result.unreadCount);
    } catch {
      this.setUnreadCount(0);
    }
  }

  applyReadSync(event: SellerNotificationReadRealtimeEvent): void {
    this.latestReadEvent.set(event);
    void this.refreshUnreadCount();
  }

  applyReadAllSync(event: SellerNotificationsReadAllRealtimeEvent): void {
    this.latestReadAllEvent.set(event);
    this.setUnreadCount(0);
  }

  dismissLatestNotification(): void {
    this.latestNotification.set(null);
  }

  private async ensureStarted(accessToken: string): Promise<void> {
    if (this.connection && this.connectionToken === accessToken) {
      return this.startPromise ?? Promise.resolve();
    }

    await this.stop();
    this.connectionToken = accessToken;
    this.connection = this.createConnection();
    this.registerHandlers(this.connection);

    this.startPromise = this.connection.start()
      .then(async () => {
        this.isConnected.set(true);
        await this.refreshUnreadCount();
      })
      .catch(() => {
        this.isConnected.set(false);
      })
      .finally(() => {
        this.startPromise = null;
      });

    await this.startPromise;
  }

  private createConnection(): HubConnection {
    return this.connectionFactory(
      `${environment.apiBaseUrl}/hubs/notifications`,
      () => this.authService.accessToken ?? '');
  }

  private registerHandlers(connection: HubConnection): void {
    connection.on('notificationCreated', (notification: SellerNotificationResponse) => {
      this.latestNotification.set(notification);
      if (!notification.readAtUtc) {
        this.setUnreadCount(this.unreadCount() + 1);
      }
    });

    connection.on('notificationRead', (event: SellerNotificationReadRealtimeEvent) => {
      this.applyReadSync(event);
    });

    connection.on('notificationsReadAll', (event: SellerNotificationsReadAllRealtimeEvent) => {
      this.applyReadAllSync(event);
    });

    connection.onreconnected(() => {
      this.isConnected.set(true);
      void this.refreshUnreadCount();
    });
  }

  private async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.connectionToken = null;
    this.startPromise = null;
    this.isConnected.set(false);
    this.setUnreadCount(0);

    if (connection) {
      try {
        await connection.stop();
      } catch {
        // The next auth transition will rebuild the connection if needed.
      }
    }
  }

  private setUnreadCount(count: number): void {
    this.unreadCount.set(count);
    this.notificationService.unreadCount.set(count);
  }
}
