import { Component, input } from '@angular/core';
import { StatusBadgeComponent, StatusBadgeTone } from './status-badge.component';

@Component({
  selector: 'app-metric-tile',
  imports: [StatusBadgeComponent],
  template: `
    <article class="hf-metric-tile">
      <span>{{ label() }}</span>
      <strong>{{ value() }}</strong>
      @if (badge()) {
        <app-status-badge [label]="badge()" [tone]="badgeTone()" />
      }
    </article>
  `
})
export class MetricTileComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly badge = input<string | null>(null);
  readonly badgeTone = input<StatusBadgeTone>('neutral');
}
