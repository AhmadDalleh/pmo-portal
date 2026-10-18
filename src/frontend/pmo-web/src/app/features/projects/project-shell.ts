import { Component, effect, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { AsyncState } from '../../shared/async-state/async-state';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { ProjectContext } from './project-context';

/** Header and tab bar of one project. The tabs are child routes (overview, tasks, milestones, finance, risks, change requests, members). */
@Component({
  selector: 'app-project-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatTabsModule, AsyncState, StatusBadge],
  providers: [ProjectContext],
  template: `
    <div class="page">
      <app-async-state [status]="ctx.status()" [error]="ctx.error()" (retry)="ctx.reload()">
        @if (ctx.project(); as p) {
          <div class="row head">
            <h1 class="grow"><span class="muted">{{ p.code }}</span> {{ p.name }}</h1>
            <app-status-badge [value]="p.status" />
          </div>
          <p class="muted">{{ p.departmentName }} · Owner {{ p.ownerName }} · {{ p.startDate }} to {{ p.plannedEndDate }}</p>
          <nav mat-tab-nav-bar [tabPanel]="panel" aria-label="Project sections">
            @for (t of tabs; track t.path) {
              <a mat-tab-link [routerLink]="t.path" routerLinkActive #rla="routerLinkActive" [active]="rla.isActive">{{ t.label }}</a>
            }
          </nav>
          <mat-tab-nav-panel #panel><div class="tab"><router-outlet /></div></mat-tab-nav-panel>
        }
      </app-async-state>
    </div>
  `,
  styles: `.head { margin-bottom: 0; } .tab { padding-top: 14px; }`,
})
export class ProjectShell {
  /** Bound from the route parameter :id (withComponentInputBinding). */
  readonly id = input.required<string>();
  protected readonly ctx = inject(ProjectContext);

  protected readonly tabs = [
    { path: 'overview', label: 'Overview' },
    { path: 'tasks', label: 'Tasks' },
    { path: 'milestones', label: 'Milestones' },
    { path: 'finance', label: 'Finance' },
    { path: 'risks', label: 'Risks & issues' },
    { path: 'change-requests', label: 'Change requests' },
    { path: 'members', label: 'Members' },
  ];

  constructor() {
    effect(() => void this.ctx.load(Number(this.id())));
  }
}
