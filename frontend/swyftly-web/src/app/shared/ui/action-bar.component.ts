import { Component } from '@angular/core';

@Component({
  selector: 'app-action-bar',
  template: `
    <div class="hf-action-bar">
      <ng-content />
    </div>
  `
})
export class ActionBarComponent {
}
