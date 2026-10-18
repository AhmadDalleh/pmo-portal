import { Component, effect, inject, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { TasksApi } from '../../core/api/tasks.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { Milestone } from '../../core/models/delivery.models';
import { Confirm } from '../../shared/confirm-dialog/confirm.service';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { Notify } from '../../shared/notify';
import { ProjectContext } from '../projects/project-context';
import { MilestoneDialog } from './milestone-dialog';

/** Story C5: milestones with how many of their tasks are done; overdue ones are flagged with icon and text. */
@Component({
  selector: 'app-project-milestones',
  imports: [DataTable, MatButtonModule, MatIconModule],
  template: `
    <div class="row">
      <h2 class="grow">Milestones</h2>
      @if (ctx.canManage()) { <button mat-flat-button type="button" (click)="edit(null)"><mat-icon aria-hidden="true">add</mat-icon> New milestone</button> }
    </div>
    <app-data-table label="Milestones" [columns]="columns" [rows]="rows()" [status]="status()" [error]="error()" emptyText="No milestones yet."
                    [actions]="ctx.canManage() ? rowActions : null" (retry)="load()" />
    <ng-template #rowActions let-m>
      @if (m.status === 'pending') {
        <button mat-icon-button type="button" [attr.aria-label]="'Complete ' + m.name" (click)="complete(m)"><mat-icon aria-hidden="true">task_alt</mat-icon></button>
      }
      <button mat-icon-button type="button" [attr.aria-label]="'Edit ' + m.name" (click)="edit(m)"><mat-icon aria-hidden="true">edit</mat-icon></button>
    </ng-template>
  `,
})
export class ProjectMilestones {
  protected readonly ctx = inject(ProjectContext);
  private readonly api = inject(TasksApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(Confirm);
  private readonly notify = inject(Notify);

  protected readonly rows = signal<Milestone[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<Milestone>[] = [
    { key: 'name', header: 'Milestone', value: (m) => m.name },
    { key: 'due', header: 'Due', value: (m) => m.dueDate },
    { key: 'status', header: 'Status', badge: (m) => (m.isOverdue ? 'overdue' : m.status) },
    { key: 'tasks', header: 'Tasks done', align: 'end', value: (m) => `${m.tasksDone} / ${m.tasksTotal}` },
    { key: 'completed', header: 'Completed', value: (m) => m.completedAt },
  ];

  constructor() { effect(() => { if (this.ctx.id()) untracked(() => void this.load()); }); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      this.rows.set(await firstValueFrom(this.api.milestones(this.ctx.id())));
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async edit(milestone: Milestone | null): Promise<void> {
    const ref = this.dialog.open(MilestoneDialog, { data: { projectId: this.ctx.id(), milestone }, width: '480px', maxWidth: '96vw' });
    await firstValueFrom(ref.afterClosed());
    await this.load();
  }

  protected async complete(m: Milestone): Promise<void> {
    if (!(await this.confirm.ask({ title: `Complete "${m.name}"?`, message: 'The completion date is recorded as today.', confirmLabel: 'Complete' }))) return;
    try {
      await firstValueFrom(this.api.completeMilestone(m.id, m.version));
      this.notify.success('Milestone completed.');
    } catch (e) { this.notify.error(toApiError(e)); }
    await this.load();
  }
}
