import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { TasksApi } from '../../core/api/tasks.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { TaskItem } from '../../core/models/delivery.models';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { ProgressDialog } from './progress-dialog';

/** Story C3: the signed-in person's own tasks across all projects, with overdue and due-soon indicators. */
@Component({
  selector: 'app-my-work-page',
  imports: [DataTable, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule],
  template: `
    <div class="page">
      <div class="row">
        <h1 class="grow">My work</h1>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Show</mat-label>
          <mat-select [ngModel]="filter()" (ngModelChange)="setFilter($event)">
            <mat-option [value]="undefined">Open tasks</mat-option><mat-option value="done">Done</mat-option><mat-option value="all">Everything</mat-option>
          </mat-select>
        </mat-form-field>
      </div>
      <app-data-table label="My tasks" [columns]="columns" [rows]="rows()" [total]="total()" [page]="page()" [pageSize]="pageSize()" [status]="status()" [error]="error()"
                      emptyText="Nothing is assigned to you right now." [actions]="rowActions" (pageChange)="onPage($event)" (retry)="load()" />
      <ng-template #rowActions let-t>
        <button mat-icon-button type="button" [attr.aria-label]="'Update progress of ' + t.title" (click)="progress(t)"><mat-icon aria-hidden="true">trending_up</mat-icon></button>
        <button mat-icon-button type="button" [attr.aria-label]="'Open project ' + t.projectCode" (click)="openProject(t)"><mat-icon aria-hidden="true">open_in_new</mat-icon></button>
      </ng-template>
    </div>
  `,
})
export class MyWorkPage {
  private readonly api = inject(TasksApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  protected readonly rows = signal<TaskItem[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly filter = signal<string | undefined>(undefined);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<TaskItem>[] = [
    { key: 'project', header: 'Project', value: (t) => t.projectCode },
    { key: 'title', header: 'Task', value: (t) => t.title },
    { key: 'status', header: 'Status', badge: (t) => t.status },
    { key: 'due', header: 'Due', value: (t) => t.dueDate },
    { key: 'indicator', header: 'Timing', badge: (t) => t.indicator || null },
    { key: 'progress', header: 'Progress', align: 'end', value: (t) => `${t.progressPct}%` },
  ];

  constructor() { void this.load(); }

  protected setFilter(value: string | undefined): void { this.filter.set(value); this.page.set(1); void this.load(); }
  protected onPage(p: { page: number; pageSize: number }): void { this.page.set(p.page); this.pageSize.set(p.pageSize); void this.load(); }
  protected openProject(t: TaskItem): void { void this.router.navigate(['/projects', t.projectId, 'tasks']); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const result = await firstValueFrom(this.api.mine(this.filter(), this.page(), this.pageSize()));
      this.rows.set(result.items);
      this.total.set(result.totalCount);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async progress(task: TaskItem): Promise<void> {
    const ref = this.dialog.open(ProgressDialog, { data: { task, canCancel: false }, width: '440px', maxWidth: '96vw' });
    await firstValueFrom(ref.afterClosed());
    await this.load();
  }
}
