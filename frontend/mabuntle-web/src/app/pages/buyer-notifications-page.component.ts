import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerNotificationResponse } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerNotificationRealtimeService } from '../buyer/buyer-notification-realtime.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-notifications-page',
  imports: [
    BuyerWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <app-page-header
        eyebrow="Buyer account"
        heading="Notifications"
        description="Read marketplace updates about your account activity."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/account/settings">Notification settings</a>
          <button data-ui-button="secondary" type="button" [disabled]="unreadCount() === 0 || isSaving()" (click)="markAllRead()">
            Mark all read
          </button>
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading notifications...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <div class="buyer-notification-summary">
          <strong>{{ unreadCount() }}</strong>
          <span>unread notification{{ unreadCount() === 1 ? '' : 's' }}</span>
        </div>

        @if (notifications().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Notifications"
            heading="No notifications yet"
            message="Order, return, support, and account updates will appear here when marketplace activity creates them."
          >
            <a data-ui-button="primary" routerLink="/account">Back to account</a>
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

                <div class="buyer-action-row">
                  @if (relatedRoute(notification)) {
                    <a data-ui-button="secondary" [routerLink]="relatedRoute(notification)">Open</a>
                  }

                  @if (!notification.readAtUtc) {
                    <button data-ui-button="secondary"
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
export class BuyerNotificationsPageComponent implements OnInit {
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly notificationRealtime = inject(BuyerNotificationRealtimeService);

  protected readonly notifications = signal<BuyerNotificationResponse[]>([]);
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
      if (notification) {
        queueMicrotask(() => this.prependNotification(notification));
      }
    });

    effect(() => {
      const event = this.notificationRealtime.latestReadEvent();
      if (event) {
        queueMicrotask(() => this.applyReadEvent(event.notificationId, event.readAtUtc));
      }
    });

    effect(() => {
      const event = this.notificationRealtime.latestReadAllEvent();
      if (event) {
        queueMicrotask(() => this.applyReadAllEvent(event.readAtUtc));
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.loadNotifications();
  }

  protected async markRead(notification: BuyerNotificationResponse): Promise<void> {
    if (this.savingNotificationId()) {
      return;
    }

    this.savingNotificationId.set(notification.notificationId);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const updated = await this.engagementService.markNotificationRead(notification.notificationId);
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
      const result = await this.engagementService.markAllNotificationsRead();
      const readAtUtc = new Date().toISOString();
      this.notifications.set(this.notifications().map(notification =>
        notification.readAtUtc ? notification : { ...notification, readAtUtc }));
      this.notificationRealtime.applyReadAllSync({ readAtUtc, updatedCount: result.updatedCount });
      this.successMessage.set(`${result.updatedCount} notification${result.updatedCount === 1 ? '' : 's'} marked read.`);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected relatedRoute(notification: BuyerNotificationResponse): string[] | null {
    if (!notification.relatedEntityType || !notification.relatedEntityId) {
      return null;
    }

    const entityType = notification.relatedEntityType.trim().toLowerCase();
    if (entityType === 'order') {
      return ['/account/orders', notification.relatedEntityId];
    }

    if (entityType === 'returnrequest' || entityType === 'return') {
      return ['/account/returns', notification.relatedEntityId];
    }

    if (entityType === 'supportticket' || entityType === 'support') {
      return ['/account/support', notification.relatedEntityId];
    }

    if (entityType === 'refund') {
      return ['/account/refunds'];
    }

    return null;
  }

  private async loadNotifications(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.notifications.set(await this.engagementService.listNotifications());
      await this.notificationRealtime.refreshUnreadCount();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private prependNotification(notification: BuyerNotificationResponse): void {
    this.notifications.update(existing => {
      if (existing.some(item => item.notificationId === notification.notificationId)) {
        return existing.map(item => item.notificationId === notification.notificationId ? notification : item);
      }

      return [notification, ...existing];
    });
  }

  private applyReadEvent(notificationId: string, readAtUtc: string): void {
    this.notifications.update(existing => existing.map(notification =>
      notification.notificationId === notificationId
        ? { ...notification, readAtUtc }
        : notification));
  }

  private applyReadAllEvent(readAtUtc: string): void {
    this.notifications.update(existing => existing.map(notification =>
      notification.readAtUtc ? notification : { ...notification, readAtUtc }));
  }
}
