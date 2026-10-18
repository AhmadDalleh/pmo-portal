import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Sort } from '@angular/material/sort';
import { firstValueFrom } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { TasksApi } from '../../core/api/tasks.api';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { Milestone, TASK_STATUSES, TaskItem } from '../../core/models/delivery.models';
import { Member } from '../../core/models/project.models';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { ProjectContext } from '../projects/project-context';
import { ProgressDialog } from './progress-dialog';
import { TaskDialog } from './task-dialog';

/** Stories C1, C2, C4: the project's task list with date indicators (icon + text), filters, sort, paging. */
@Component({
  selector: 'app-project-tasks',
  imports: [DataTable, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule],
  template: `
    <div class="row">
      <h2 class="grow">Tasks</h2>
      @if (ctx.canManage()) { <button mat-flat-button type="button" (click)="edit(null)"><mat-icon aria-hidden="true">add</mat-icon> New task</button> }
    </div>
    <div class="row filters">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select [ngModel]="statusFilter()" (ngModelChange)="setFilter($event, overdueOnly())">
          <mat-option [value]="undefined">All</mat-option>@for (s of statuses; track s) { <mat-option [value]="s">{{ s.replace('_', ' ') }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Show</mat-label>
        <mat-select [ngModel]="overdueOnly()" (ngModelChange)="setFilter(statusFilter(), $event)"><mat-option [value]="false">All tasks</mat-option><mat-option [value]="true">Overdue only</mat-option></mat-select>
      </mat-form-field>
    </div>
    <app-data-table label="Tasks" [columns]="columns" [rows]="rows()" [total]="total()" [page]="page()" [pageSize]="pageSize()" [status]="status()" [error]="error()"
                    [sortActive]="sort()" [sortDirection]="dir()" emptyText="No tasks match these filters." [actions]="rowActions"
                    (sortChange)="onSort($event)" (pageChange)="onPage($event)" (retry)="load()" />
    <ng-template #rowActions let-t>
      @if (canUpdate(t)) { <button mat-icon-button type="button" [attr.aria-label]="'Update progress of ' + t.title" (click)="progress(t)"><mat-icon aria-hidden="true">trending_up</mat-icon></button> }
      @if (ctx.canManage()) { <button mat-icon-button type="button" [attr.aria-label]="'Edit ' + t.title" (click)="edit(t)"><mat-icon aria-hidden="true">edit</mat-icon></button> }
    </ng-template>
  `,
  styles: `.filters { margin-bottom: 8px; } .filters mat-form-field { min-width: 180px; }`,
})
export class ProjectTasks {
  protected readonly ctx = inject(ProjectContext);
  private readonly auth = inject(AuthService);
  private readonly api = inject(TasksApi);
  private readonly projects = inject(ProjectsApi);
  private readonly dialog = inject(MatDialog);

  protected readonly statuses = TASK_STATUSES;
  protected readonly rows = signal<TaskItem[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly sort = signal('due');
  protected readonly dir = signal<'asc' | 'desc'>('asc');
  protected readonly statusFilter = signal<string | undefined>(undefined);
  protected readonly overdueOnly = signal(false);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<TaskItem>[] = [
    { key: 'title', header: 'Task', sortable: true, value: (t) => t.title },
    { key: 'assignee', header: 'Assignee', value: (t) => t.assigneeName ?? 'Unassigned' },
    { key: 'milestone', header: 'Milestone', value: (t) => t.milestoneName },
    { key: 'status', header: 'Status', sortable: true, badge: (t) => t.status },
    { key: 'priority', header: 'Priority', sortable: true, value: (t) => t.priority },
    { key: 'due', header: 'Due', sortable: true, value: (t) => t.dueDate },
    { key: 'indicator', header: 'Timing', badge: (t) => t.indicator || null },
    { key: 'progress', header: 'Progress', sortable: true, align: 'end', value: (t) => `${t.progressPct}%` },
  ];

  constructor() {
    effect(() => { if (this.ctx.id()) void this.load(); });
  }

  protected canUpdate(t: TaskItem): boolean {
    return this.ctx.canManage() || t.assigneeId === this.auth.user()?.id;
  }

  protected setFilter(status: string | undefined, overdue: boolean): void {
    this.statusFilter.set(status);
    this.overdueOnly.set(overdue);
    this.page.set(1);
    void this.load();
  }

  protected onSort(s: Sort): void { this.sort.set(s.active || 'due'); this.dir.set(s.direction === 'desc' ? 'desc' : 'asc'); this.page.set(1); void this.load(); }
  protected onPage(p: { page: number; pageSize: number }): void { this.page.set(p.page); this.pageSize.set(p.pageSize); void this.load(); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const result = await firstValueFrom(this.api.list(this.ctx.id(), {
        status: this.statusFilter(), overdue: this.overdueOnly() || undefined, sort: this.sort(), dir: this.dir(), page: this.page(), pageSize: this.pageSize(),
      }));
      this.rows.set(result.items);
      this.total.set(result.totalCount);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async progress(task: TaskItem): Promise<void> {
    const ref = this.dialog.open(ProgressDialog, { data: { task, canCancel: this.ctx.canManage() }, width: '440px', maxWidth: '96vw' });
    await firstValueFrom(ref.afterClosed());
    await this.load();   // new progress comes back from the server; nothing is patched locally
  }

  protected async edit(task: TaskItem | null): Promise<void> {
    const [members, milestones] = await Promise.all([firstValueFrom(this.projects.members(this.ctx.id())), firstValueFrom(this.api.milestones(this.ctx.id()))]);
    const ref = this.dialog.open(TaskDialog, {
      data: { projectId: this.ctx.id(), task, members: members as Member[], milestones: milestones as Milestone[] }, width: '720px', maxWidth: '96vw',
    });
    await firstValueFrom(ref.afterClosed());
    await this.load();
  }
}
