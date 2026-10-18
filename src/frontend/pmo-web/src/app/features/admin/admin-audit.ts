import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { ProjectsApi } from '../../core/api/projects.api';
import { toApiError } from '../../core/http/api-error';
import { AuditEvent, AuditQuery } from '../../core/models/admin.models';
import { ApiError, LoadStatus, UserLookup } from '../../core/models/common.models';
import { ProjectListItem } from '../../core/models/project.models';
import { Column, DataTable } from '../../shared/data-table/data-table';

/** Story A5: who did what, newest first. Click a row to see the event's data (before/after or key facts) as JSON. */
@Component({
  selector: 'app-admin-audit',
  imports: [DataTable, FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <form class="row filters" aria-label="Audit filters" (ngSubmit)="apply()">
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Entity type</mat-label><input matInput name="entityType" [(ngModel)]="entityType" placeholder="e.g. CostTransaction" /></mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Action</mat-label><input matInput name="action" [(ngModel)]="action" placeholder="e.g. cost.posted" /></mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Actor</mat-label>
        <mat-select name="actor" [(ngModel)]="actorId"><mat-option [value]="undefined">Anyone</mat-option>@for (u of users(); track u.id) { <mat-option [value]="u.id">{{ u.fullName }}</mat-option> }</mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Project</mat-label>
        <mat-select name="project" [(ngModel)]="projectId"><mat-option [value]="undefined">Any</mat-option>@for (p of projects(); track p.id) { <mat-option [value]="p.id">{{ p.code }}</mat-option> }</mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>From</mat-label><input matInput type="date" name="from" [(ngModel)]="from" /></mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>To</mat-label><input matInput type="date" name="to" [(ngModel)]="to" /></mat-form-field>
      <button mat-flat-button type="submit">Apply</button>
      <button mat-button type="button" (click)="clear()">Clear</button>
    </form>

    <app-data-table label="Audit events" [columns]="columns" [rows]="rows()" [total]="total()" [page]="query().page" [pageSize]="query().pageSize"
                    [status]="status()" [error]="error()" [detail]="detail" emptyText="No audit events match these filters."
                    (pageChange)="onPage($event)" (retry)="load()" />

    <ng-template #detail let-e>
      <div class="meta">Entity {{ e.entityType }} #{{ e.entityId }} · project {{ e.projectId ?? '-' }} · correlation id <code>{{ e.correlationId ?? '-' }}</code></div>
      <pre>{{ pretty(e.data) }}</pre>
    </ng-template>
  `,
  styles: `
    .filters { margin-bottom: 8px; gap: 8px; } .filters mat-form-field { width: 170px; }
    pre { margin: 6px 0 0; white-space: pre-wrap; word-break: break-word; font-size: 12px; }
    .meta { font-size: 12px; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class AdminAudit {
  private readonly api = inject(AdminApi);
  private readonly projectsApi = inject(ProjectsApi);

  protected entityType = '';
  protected action = '';
  protected actorId: number | undefined;
  protected projectId: number | undefined;
  protected from = '';
  protected to = '';

  protected readonly users = signal<UserLookup[]>([]);
  protected readonly projects = signal<ProjectListItem[]>([]);
  protected readonly rows = signal<AuditEvent[]>([]);
  protected readonly total = signal(0);
  protected readonly query = signal<AuditQuery>({ page: 1, pageSize: 20 });
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<AuditEvent>[] = [
    { key: 'when', header: 'When (UTC)', value: (e) => e.occurredAt.slice(0, 19).replace('T', ' ') },
    { key: 'actor', header: 'Actor', value: (e) => e.actorName ?? 'System' },
    { key: 'action', header: 'Action', value: (e) => e.action },
    { key: 'entity', header: 'Entity', value: (e) => `${e.entityType} ${e.entityId}` },
    { key: 'project', header: 'Project', value: (e) => this.projects().find((p) => p.id === e.projectId)?.code ?? e.projectId },
    { key: 'corr', header: 'Correlation id', value: (e) => e.correlationId },
  ];

  constructor() {
    void firstValueFrom(this.projectsApi.users()).then((u) => this.users.set(u));
    void firstValueFrom(this.projectsApi.list({ sort: 'code', dir: 'asc', page: 1, pageSize: 100 })).then((p) => this.projects.set(p.items));
    void this.load();
  }

  protected apply(): void {
    this.query.set({
      entityType: this.entityType.trim() || undefined, action: this.action.trim() || undefined, actorId: this.actorId, projectId: this.projectId,
      from: this.from || undefined, to: this.to || undefined, page: 1, pageSize: this.query().pageSize,
    });
    void this.load();
  }

  protected clear(): void {
    this.entityType = ''; this.action = ''; this.actorId = undefined; this.projectId = undefined; this.from = ''; this.to = '';
    this.apply();
  }

  protected onPage(p: { page: number; pageSize: number }): void { this.query.update((q) => ({ ...q, ...p })); void this.load(); }

  protected pretty(json: string | null): string {
    if (!json) return '(no data)';
    try { return JSON.stringify(JSON.parse(json), null, 2); } catch { return json; }
  }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const page = await firstValueFrom(this.api.audit(this.query()));   // newest first: the server orders by time
      this.rows.set(page.items);
      this.total.set(page.totalCount);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }
}
