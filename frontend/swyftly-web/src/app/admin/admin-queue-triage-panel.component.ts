import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';
import { AdminQueueItemType, AdminQueuePriority, AdminQueueTriageFields, AdminQueueTriageResponse } from './admin-queue-triage.models';
import { AdminQueueTriageService } from './admin-queue-triage.service';

@Component({
  selector: 'app-admin-queue-triage-panel',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    UiAlertComponent
  ],
  template: `
    <div class="hf-admin-summary-panel">
      <strong>Internal triage</strong>
      <span>Priority: {{ summary?.priority ?? 'Normal' }}</span>
      <span>Assigned: {{ summary?.assignedToDisplayName ?? 'Unassigned' }}</span>
      @if (summary?.latestTriageNote) {
        <span>{{ summary?.latestTriageNote }}</span>
      } @else {
        <span>No internal notes yet</span>
      }
    </div>

    @if (errorMessage()) {
      <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
    }

    @if (successMessage()) {
      <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
    }

    <div class="admin-audit-actions">
      <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="claim()">Claim</button>
      <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="unclaim()">Unclaim</button>
      @for (priority of priorities; track priority) {
        <button mat-stroked-button type="button" [disabled]="isSaving()" (click)="setPriority(priority)">
          {{ priority }}
        </button>
      }
    </div>

    <div class="admin-moderation-filters compact">
      <mat-form-field appearance="outline">
        <mat-label>Internal note</mat-label>
        <textarea matInput rows="3" [(ngModel)]="note" maxlength="1000"></textarea>
      </mat-form-field>
      <button mat-flat-button type="button" [disabled]="isSaving() || !note.trim()" (click)="addNote()">Add note</button>
    </div>

    @if (triage()?.notes?.length) {
      <div class="hf-admin-summary-panel">
        <strong>Recent notes</strong>
        @for (entry of triage()!.notes.slice(0, 3); track entry.noteId) {
          <span>{{ entry.createdAtUtc | date:'short' }} / {{ entry.actorDisplayName ?? 'Admin' }}: {{ entry.note }}</span>
        }
      </div>
    }
  `
})
export class AdminQueueTriagePanelComponent {
  private readonly triageService = inject(AdminQueueTriageService);

  @Input({ required: true }) itemType!: AdminQueueItemType | string;
  @Input({ required: true }) itemId!: string;
  @Input() summary: AdminQueueTriageFields | null = null;
  @Output() triageChanged = new EventEmitter<void>();

  protected readonly priorities: AdminQueuePriority[] = ['Normal', 'High', 'Urgent'];
  protected readonly triage = signal<AdminQueueTriageResponse | null>(null);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected note = '';

  async claim(): Promise<void> {
    await this.run(() => this.triageService.claim(this.itemType, this.itemId), 'Queue item claimed.');
  }

  async unclaim(): Promise<void> {
    await this.run(() => this.triageService.unclaim(this.itemType, this.itemId), 'Queue item unclaimed.');
  }

  async setPriority(priority: AdminQueuePriority): Promise<void> {
    await this.run(() => this.triageService.updateTriage(this.itemType, this.itemId, { priority }), `Priority set to ${priority}.`);
  }

  async addNote(): Promise<void> {
    const note = this.note.trim();
    if (!note) {
      return;
    }

    await this.run(() => this.triageService.updateTriage(this.itemType, this.itemId, { note }), 'Internal note added.');
    this.note = '';
  }

  private async run(action: () => Promise<AdminQueueTriageResponse>, successMessage: string): Promise<void> {
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      this.triage.set(await action());
      this.successMessage.set(successMessage);
      this.triageChanged.emit();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }
}
