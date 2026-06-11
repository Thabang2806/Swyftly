import { Component, input } from '@angular/core';
import { StatusBadgeComponent } from './status-badge.component';

@Component({
  selector: 'app-dashboard-card',
  template: `
    <article class="ui-dashboard-card">
      @if (eyebrow()) {
        <app-status-badge [label]="eyebrow()" tone="accent" />
      }
      <div class="ui-dashboard-card-copy">
        <h2>{{ heading() }}</h2>
        <p>{{ description() }}</p>
      </div>
      <div class="ui-dashboard-card-actions">
        <ng-content />
      </div>
    </article>
  `,
  imports: [StatusBadgeComponent]
})
export class DashboardCardComponent {
  readonly eyebrow = input<string | null>(null);
  readonly heading = input.required<string>();
  readonly description = input.required<string>();
}
