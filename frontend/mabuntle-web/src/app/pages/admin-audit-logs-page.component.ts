import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { AdminAuditLogDetailResponse } from '../admin/admin-audit-log.models';
import { AdminAuditLogService } from '../admin/admin-audit-log.service';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-audit-logs-page',
  imports: [
    AdminWorkspaceNavComponent,
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />

      <app-page-header
        eyebrow="Admin audit"
        heading="Audit logs"
        description="Review sensitive admin actions across sellers, products, finance, support, and moderation workflows."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/sellers">Seller queue</a>
          <a data-ui-button="secondary" routerLink="/products">Product queue</a>
        </div>
      </app-page-header>

      <form [formGroup]="filtersForm" (ngSubmit)="search()" class="route-card admin-audit-filters" novalidate>
        <label class="ui-field">
          <span>Action type</span>
          <input formControlName="actionType">
        </label>

        <label class="ui-field">
          <span>Entity type</span>
          <input formControlName="entityType">
        </label>

        <label class="ui-field">
          <span>Entity id</span>
          <input formControlName="entityId">
        </label>

        <label class="ui-field">
          <span>Actor user id</span>
          <input formControlName="actorUserId">
        </label>

        <div class="admin-audit-actions">
          <button data-ui-button="primary" type="submit" [disabled]="isLoading()">Apply filters</button>
          <button data-ui-button="secondary" type="button" [disabled]="isLoading()" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading audit logs...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (auditLogs().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Empty"
            heading="No audit logs found"
            message="Sensitive admin actions will appear here after they are recorded."
          />
        } @else {
          <div class="admin-table audit-table" role="table" aria-label="Admin audit logs">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Action</span>
              <span role="columnheader">Entity</span>
              <span role="columnheader">Actor</span>
              <span role="columnheader">Created</span>
              <span role="columnheader">Reason</span>
            </div>

            @for (log of auditLogs(); track log.id) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ log.actionType }}</strong>
                  <small>{{ log.ipAddress ?? 'No IP recorded' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ log.entityType }}</strong>
                  <small>{{ log.entityId ?? 'No entity id' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ log.actorRole ?? 'Unknown role' }}</strong>
                  <small>{{ log.actorUserId ?? 'No actor id' }}</small>
                </span>
                <span role="cell">{{ log.createdAtUtc | date:'medium' }}</span>
                <span role="cell">{{ log.reason ?? 'No reason' }}</span>
              </div>
            }
          </div>

          <p class="audit-count">{{ totalCount() }} audit log{{ totalCount() === 1 ? '' : 's' }}</p>
        }
      }
    </section>
  `
})
export class AdminAuditLogsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly auditLogService = inject(AdminAuditLogService);

  protected readonly auditLogs = signal<AdminAuditLogDetailResponse[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    actionType: [''],
    entityType: [''],
    entityId: [''],
    actorUserId: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.search();
  }

  protected async search(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const filters = this.filtersForm.getRawValue();
      const response = await this.auditLogService.search({
        ...filters,
        pageSize: 50
      });
      this.auditLogs.set(response.items);
      this.totalCount.set(response.totalCount);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.auditLogs.set([]);
      this.totalCount.set(0);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset();
    await this.search();
  }
}
