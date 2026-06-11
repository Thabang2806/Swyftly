import { isPlatformBrowser } from '@angular/common';
import { Injectable, InjectionToken, PLATFORM_ID, effect, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { environment } from '../../environments/environment';
import { AuthService } from '../auth/auth.service';
import {
  BuyerNotificationResponse,
  NotificationReadRealtimeEvent,
  NotificationsReadAllRealtimeEvent
} from './buyer-engagement.models';
import { BuyerEngagementService } from './buyer-engagement.service';

export type NotificationHubConnectionFactory = (
  url: string,
  accessTokenFactory: () => string
) => HubConnection;

export const NOTIFICATION_HUB_CONNECTION_FACTORY = new InjectionToken<NotificationHubConnectionFactory>(
  'NOTIFICATION_HUB_CONNECTION_FACTORY',
  {
    providedIn: 'root',
    factory: () => (url, accessTokenFactory) => new HubConnectionBuilder()
      .withUrl(url, { accessTokenFactory })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
  }
);

@Injectable({ providedIn: 'root' })
export class BuyerNotificationRealtimeService {
  private readonly authService = inject(AuthService);
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly connectionFactory = inject(NOTIFICATION_HUB_CONNECTION_FACTORY);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private connection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;
  private connectionToken: string | null = null;

  readonly unreadCount = signal(0);
  readonly isConnected = signal(false);
  readonly latestNotification = signal<BuyerNotificationResponse | null>(null);
  readonly latestReadEvent = signal<NotificationReadRealtimeEvent | null>(null);
  readonly latestReadAllEvent = signal<NotificationsReadAllRealtimeEvent | null>(null);

  constructor() {
    if (!this.isBrowser) {
      return;
    }

    effect(() => {
      const isInitialized = this.authService.isInitialized();
      const isBuyer = this.authService.hasAnyRole(['Buyer']);
      const isAuthenticated = this.authService.isAuthenticated();
      const accessToken = this.authService.accessToken;

      queueMicrotask(() => {
        if (isInitialized && isAuthenticated && isBuyer && accessToken) {
          void this.ensureStarted(accessToken);
        } else {
          void this.stop();
        }
      });
    });
  }

  async refreshUnreadCount(): Promise<void> {
    if (!this.isBrowser || !this.authService.hasAnyRole(['Buyer']) || !this.authService.isAuthenticated()) {
      this.unreadCount.set(0);
      return;
    }

    try {
      const result = await this.engagementService.getUnreadNotificationCount();
      this.unreadCount.set(result.unreadCount);
    } catch {
      this.unreadCount.set(0);
    }
  }

  applyReadSync(event: NotificationReadRealtimeEvent): void {
    this.latestReadEvent.set(event);
    void this.refreshUnreadCount();
  }

  applyReadAllSync(event: NotificationsReadAllRealtimeEvent): void {
    this.latestReadAllEvent.set(event);
    this.unreadCount.set(0);
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
    connection.on('notificationCreated', (notification: BuyerNotificationResponse) => {
      this.latestNotification.set(notification);
      if (!notification.readAtUtc) {
        this.unreadCount.update(count => count + 1);
      }
    });

    connection.on('notificationRead', (event: NotificationReadRealtimeEvent) => {
      this.applyReadSync(event);
    });

    connection.on('notificationsReadAll', (event: NotificationsReadAllRealtimeEvent) => {
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
    this.unreadCount.set(0);

    if (connection) {
      try {
        await connection.stop();
      } catch {
        // The next auth transition will rebuild the connection if needed.
      }
    }
  }
}
