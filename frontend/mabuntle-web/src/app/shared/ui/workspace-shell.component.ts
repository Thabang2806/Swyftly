import { Component } from '@angular/core';

@Component({
  selector: 'app-workspace-shell',
  template: `
    <section class="hf-workspace-shell">
      <aside class="hf-workspace-shell-nav">
        <ng-content select="[workspaceNav]" />
      </aside>
      <main class="hf-workspace-shell-main">
        <ng-content />
      </main>
    </section>
  `
})
export class WorkspaceShellComponent {
}
