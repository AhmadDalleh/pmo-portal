import { Component, effect, inject, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { TasksApi } from '../../core/api/tasks.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { Risk } from '../../core/models/delivery.models';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { ProjectContext } from '../projects/project-context';
import { RiskDialog } from './risk-dialog';

/** Epic D for one project: risks and issues, worst first. High exposure (score 15+) is flagged with text, not only colour. */
@Component({
  selector: 'app-project-risks',
  imports: [DataTable, MatButtonModule, MatIconModule],
  template: `
    <div class="row">
      <h2 class="grow">Risks &amp; issues</h2>
      @if (ctx.canManage()) { <button mat-flat-button type="button" (click)="edit(null)"><mat-icon aria-hidden="true">add</mat-icon> New</button> }
    </div>
    <app-data-table label="Risks and issues" [columns]="columns" [rows]="rows()" [status]="status()" [error]="error()" emptyText="No risks or issues recorded."
                    [actions]="ctx.canManage() ? rowActions : null" (retry)="load()" />
    <ng-template #rowActions let-r>
      <button mat-icon-button type="button" [attr.aria-label]="'Edit ' + r.title" (click)="edit(r)"><mat-icon aria-hidden="true">edit</mat-icon></button>
    </ng-template>
  `,
})
export class ProjectRisks {
  protected readonly ctx = inject(ProjectContext);
  private readonly api = inject(TasksApi);
  private readonly projects = inject(ProjectsApi);
  private readonly dialog = inject(MatDialog);

  protected readonly rows = signal<Risk[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<Risk>[] = [
    { key: 'type', header: 'Type', value: (r) => (r.type === 'risk' ? 'Risk' : 'Issue') },
    { key: 'title', header: 'Title', value: (r) => r.title },
    { key: 'owner', header: 'Owner', value: (r) => r.ownerName },
    { key: 'score', header: 'Score (P x I)', align: 'end', value: (r) => `${r.score} (${r.probability} x ${r.impact})${r.isHighExposure && r.status !== 'closed' ? ' - HIGH' : ''}` },
    { key: 'status', header: 'Status', badge: (r) => r.status },
    { key: 'due', header: 'Due', value: (r) => r.dueDate },
  ];

  constructor() { effect(() => { if (this.ctx.id()) untracked(() => void this.load()); }); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      this.rows.set(await firstValueFrom(this.api.risks(this.ctx.id())));
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async edit(risk: Risk | null): Promise<void> {
    const members = await firstValueFrom(this.projects.members(this.ctx.id()));
    const ref = this.dialog.open(RiskDialog, { data: { projectId: this.ctx.id(), risk, members }, width: '720px', maxWidth: '96vw' });
    await firstValueFrom(ref.afterClosed());
    await this.load();
  }
}
