import { Component, input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header ui-page-header">
      <div class="ui-page-header-copy">
        @if (eyebrow()) {
          <span class="eyebrow">{{ eyebrow() }}</span>
        }
        <h1>{{ heading() }}</h1>
        @if (description()) {
          <p>{{ description() }}</p>
        }
      </div>

      <div class="ui-page-header-actions">
        <ng-content select="[pageHeaderActions]" />
      </div>
    </header>
  `
})
export class PageHeaderComponent {
  readonly eyebrow = input<string | null>(null);
  readonly heading = input.required<string>();
  readonly description = input<string | null>(null);
}
