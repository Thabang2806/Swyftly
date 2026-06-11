import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerNotificationResponse } from '../seller/seller-notification.models';
import { SellerNotificationRealtimeService } from '../seller/seller-notification-realtime.service';
import { SellerNotificationService } from '../seller/seller-notification.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-notifications-page',
  imports: [
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page">
      <app-seller-workspace-nav />

      <app-page-header
        eyebrow="Seller studio"
        heading="Notifications"
        description="Review marketplace decisions about verification, listings, revisions, and ad campaigns."
      >
        <div pageHeaderActions>
          <button data-ui-button="secondary" type="button" [disabled]="unreadCount() === 0 || isSaving()" (click)="markAllRead()">
            Mark all read
          </button>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading seller notifications...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <div class="buyer-notification-summary">
          <strong>{{ unreadCount() }}</strong>
          <span>unread seller notification{{ unreadCount() === 1 ? '' : 's' }}</span>
        </div>

        @if (notifications().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Seller notifications"
            heading="No seller updates yet"
            message="Verification, product review, listing revision, and ad campaign decisions will appear here."
          >
            <a data-ui-button="primary" routerLink="">Back to seller dashboard</a>
          </app-empty-state>
        } @else {
          <div class="buyer-notification-list">
            @for (notification of notifications(); track notification.notificationId) {
              <article class="buyer-notification-card" [class.unread]="!notification.readAtUtc">
                <div>
                  <app-status-badge [label]="notification.readAtUtc ? 'Read' : 'Unread'" [tone]="notification.readAtUtc ? 'neutral' : 'accent'" />
                  <strong>{{ notification.title }}</strong>
                  <p>{{ notification.message }}</p>
                  <small>
                    {{ notification.type }} -
                    {{ notification.createdAtUtc | date:'medium' }}
                    @if (notification.relatedEntityType) {
                      - {{ notification.relatedEntityType }}
                    }
                  </small>
                </div>

                <div class="seller-notification-actions">
                  @if (notificationLink(notification)) {
                    <a data-ui-button="secondary" [routerLink]="notificationLink(notification)">Open</a>
                  }
                  @if (!notification.readAtUtc) {
                    <button
                      data-ui-button="secondary"
                      type="button"
                      [disabled]="savingNotificationId() === notification.notificationId"
                      (click)="markRead(notification)"
                    >
                      {{ savingNotificationId() === notification.notificationId ? 'Saving...' : 'Mark read' }}
                    </button>
                  }
                </div>
              </article>
            }
          </div>
        }
      }
    </section>
  `
})
export class SellerNotificationsPageComponent implements OnInit {
  private readonly notificationService = inject(SellerNotificationService);
  private readonly notificationRealtime = inject(SellerNotificationRealtimeService);

  protected readonly notifications = signal<SellerNotificationResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly savingNotificationId = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly unreadCount = computed(() =>
    this.notifications().filter(notification => !notification.readAtUtc).length);

  constructor() {
    effect(() => {
      const notification = this.notificationRealtime.latestNotification();
      if (!notification) {
        return;
      }

      queueMicrotask(() => this.upsertLiveNotification(notification));
    });

    effect(() => {
      const readEvent = this.notificationRealtime.latestReadEvent();
      if (!readEvent) {
        return;
      }

      queueMicrotask(() => {
        this.notifications.set(this.notifications().map(notification =>
          notification.notificationId === readEvent.notificationId
            ? { ...notification, readAtUtc: readEvent.readAtUtc }
            : notification));
      });
    });

    effect(() => {
      const readAllEvent = this.notificationRealtime.latestReadAllEvent();
      if (!readAllEvent) {
        return;
      }

      queueMicrotask(() => {
        this.notifications.set(this.notifications().map(notification =>
          notification.readAtUtc ? notification : { ...notification, readAtUtc: readAllEvent.readAtUtc }));
      });
    });
  }

  async ngOnInit(): Promise<void> {
    await this.loadNotifications();
  }

  protected async markRead(notification: SellerNotificationResponse): Promise<void> {
    if (this.savingNotificationId()) {
      return;
    }

    this.savingNotificationId.set(notification.notificationId);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.notificationService.markRead(notification.notificationId);
      this.notifications.set(this.notifications().map(existing =>
        existing.notificationId === updated.notificationId ? updated : existing));
      await this.notificationRealtime.refreshUnreadCount();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.savingNotificationId.set(null);
    }
  }

  protected async markAllRead(): Promise<void> {
    if (this.isSaving() || this.unreadCount() === 0) {
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const result = await this.notificationService.markAllRead();
      const readAtUtc = new Date().toISOString();
      this.notifications.set(this.notifications().map(notification =>
        notification.readAtUtc ? notification : { ...notification, readAtUtc }));
      await this.notificationRealtime.refreshUnreadCount();
      this.successMessage.set(`${result.updatedCount} notification${result.updatedCount === 1 ? '' : 's'} marked read.`);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected notificationLink(notification: SellerNotificationResponse): string | null {
    if (notification.relatedEntityType === 'Product' && notification.relatedEntityId) {
      return `/products/${notification.relatedEntityId}/edit`;
    }

    if (notification.relatedEntityType === 'AdCampaign' && notification.relatedEntityId) {
      return `/ads/${notification.relatedEntityId}`;
    }

    if (notification.relatedEntityType === 'SellerProfile') {
      return '';
    }

    if (notification.relatedEntityType === 'SellerAnalytics') {
      return '/analytics';
    }

    return null;
  }

  private async loadNotifications(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.notifications.set(await this.notificationService.listNotifications());
      await this.notificationRealtime.refreshUnreadCount();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private upsertLiveNotification(notification: SellerNotificationResponse): void {
    this.notifications.set([
      notification,
      ...this.notifications().filter(existing => existing.notificationId !== notification.notificationId)
    ]);
  }
}
