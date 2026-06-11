import { Component, computed, input } from '@angular/core';

export type UiAlertTone = 'info' | 'success' | 'warning' | 'error';

@Component({
  selector: 'app-ui-alert',
  template: `
    <div
      class="ui-alert"
      [class.ui-alert--success]="tone() === 'success'"
      [class.ui-alert--warning]="tone() === 'warning'"
      [class.ui-alert--error]="tone() === 'error'"
      [attr.role]="role()"
    >
      <ng-content />
    </div>
  `
})
export class UiAlertComponent {
  readonly tone = input<UiAlertTone>('info');
  protected readonly role = computed(() => this.tone() === 'error' ? 'alert' : 'status');
}
