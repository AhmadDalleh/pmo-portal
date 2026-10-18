import { Component, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { GovernanceApi } from '../../core/api/governance.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { ChangeRequest } from '../../core/models/governance.models';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { ProjectContext } from '../projects/project-context';
import { CrDialog } from './cr-dialog';

/** Story E1/E4 for one project: every member can raise a change request; the row opens the page with the timeline. */
@Component({
  selector: 'app-project-change-requests',
  imports: [DataTable, MatButtonModule, MatIconModule],
  template: `
    <div class="row">
      <h2 class="grow">Change requests</h2>
      <button mat-flat-button type="button" (click)="create()"><mat-icon aria-hidden="true">add</mat-icon> New change request</button>
    </div>
    <app-data-table label="Change requests" [columns]="columns" [rows]="rows()" [total]="total()" [page]="page()" [pageSize]="pageSize()" [status]="status()" [error]="error()"
                    emptyText="No change requests yet." [clickable]="true" (rowClick)="open($event)" (pageChange)="onPage($event)" (retry)="load()" />
  `,
})
export class ProjectChangeRequests {
  protected readonly ctx = inject(ProjectContext);
  private readonly api = inject(GovernanceApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  protected readonly rows = signal<ChangeRequest[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<ChangeRequest>[] = [
    { key: 'code', header: 'Code', value: (c) => c.code },
    { key: 'title', header: 'Title', value: (c) => c.title },
    { key: 'type', header: 'Type', value: (c) => c.type },
    { key: 'requester', header: 'Requested by', value: (c) => c.requestedByName },
    { key: 'status', header: 'Status', badge: (c) => c.status },
    { key: 'submitted', header: 'Submitted', value: (c) => c.submittedAt?.slice(0, 10) },
  ];

  constructor() { effect(() => { if (this.ctx.id()) void this.load(); }); }

  protected onPage(p: { page: number; pageSize: number }): void { this.page.set(p.page); this.pageSize.set(p.pageSize); void this.load(); }
  protected open(c: ChangeRequest): void { void this.router.navigate(['/change-requests', c.id]); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const result = await firstValueFrom(this.api.forProject(this.ctx.id(), this.page(), this.pageSize()));
      this.rows.set(result.items);
      this.total.set(result.totalCount);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async create(): Promise<void> {
    const ref = this.dialog.open(CrDialog, { data: { projectId: this.ctx.id(), request: null }, width: '680px', maxWidth: '96vw' });
    const saved = await firstValueFrom(ref.afterClosed());
    if (saved) void this.router.navigate(['/change-requests', saved.id]);
    else await this.load();
  }
}
