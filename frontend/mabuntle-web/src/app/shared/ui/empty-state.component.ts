import { Component, input } from '@angular/core';
import { StatusBadgeComponent } from './status-badge.component';

@Component({
  selector: 'app-empty-state',
  template: `
    <section class="ui-empty-state">
      @if (eyebrow()) {
        <app-status-badge [label]="eyebrow()" tone="accent" />
      }
      <h2>{{ heading() }}</h2>
      <p>{{ message() }}</p>
      <div class="ui-empty-state-actions">
        <ng-content />
      </div>
    </section>
  `,
  imports: [StatusBadgeComponent]
})
export class EmptyStateComponent {
  readonly eyebrow = input<string | null>(null);
  readonly heading = input.required<string>();
  readonly message = input.required<string>();
}
