import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';

/** The Administrator area: four tabs (users, audit log, automation, cost categories). Reached only through adminGuard. */
@Component({
  selector: 'app-admin-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatTabsModule],
  template: `
    <div class="page">
      <h1>Administration</h1>
      <nav mat-tab-nav-bar [tabPanel]="panel" aria-label="Administration sections">
        @for (t of tabs; track t.path) {
          <a mat-tab-link [routerLink]="t.path" routerLinkActive #rla="routerLinkActive" [active]="rla.isActive">{{ t.label }}</a>
        }
      </nav>
      <mat-tab-nav-panel #panel><div class="tab"><router-outlet /></div></mat-tab-nav-panel>
    </div>
  `,
  styles: `.tab { padding-top: 14px; }`,
})
export class AdminShell {
  protected readonly tabs = [
    { path: 'users', label: 'Users' },
    { path: 'audit', label: 'Audit log' },
    { path: 'automation', label: 'Automation' },
    { path: 'categories', label: 'Cost categories' },
  ];
}
