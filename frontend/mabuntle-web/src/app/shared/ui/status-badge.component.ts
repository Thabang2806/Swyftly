import { Component, input } from '@angular/core';

export type StatusBadgeTone = 'neutral' | 'accent' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'app-status-badge',
  template: `
    <span
      class="ui-status-badge"
      [class.ui-status-badge--accent]="tone() === 'accent'"
      [class.ui-status-badge--success]="tone() === 'success'"
      [class.ui-status-badge--warning]="tone() === 'warning'"
      [class.ui-status-badge--danger]="tone() === 'danger'"
    >
      @if (label()) {
        {{ label() }}
      } @else {
        <ng-content />
      }
    </span>
  `
})
export class StatusBadgeComponent {
  readonly label = input<string | null>(null);
  readonly tone = input<StatusBadgeTone>('neutral');
}
