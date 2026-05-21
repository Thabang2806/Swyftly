import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerNotificationResponse } from '../buyer/buyer-engagement.models';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
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
    MatButtonModule,
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
          <a mat-stroked-button routerLink="/account/settings">Notification settings</a>
          <button mat-stroked-button type="button" [disabled]="unreadCount() === 0 || isSaving()" (click)="markAllRead()">
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
            <a mat-flat-button routerLink="/account">Back to account</a>
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

                @if (!notification.readAtUtc) {
                  <button
                    mat-stroked-button
                    type="button"
                    [disabled]="savingNotificationId() === notification.notificationId"
                    (click)="markRead(notification)"
                  >
                    {{ savingNotificationId() === notification.notificationId ? 'Saving...' : 'Mark read' }}
                  </button>
                }
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

  protected readonly notifications = signal<BuyerNotificationResponse[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly savingNotificationId = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly unreadCount = computed(() =>
    this.notifications().filter(notification => !notification.readAtUtc).length);

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
      this.successMessage.set(`${result.updatedCount} notification${result.updatedCount === 1 ? '' : 's'} marked read.`);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private async loadNotifications(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.notifications.set(await this.engagementService.listNotifications());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
