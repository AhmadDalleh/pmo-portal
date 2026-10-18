import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Sort } from '@angular/material/sort';
import { firstValueFrom } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus, LookupItem } from '../../core/models/common.models';
import { PROJECT_STATUSES, ProjectListItem, ProjectQuery } from '../../core/models/project.models';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { PctPipe } from '../../shared/format.pipes';

/** Story B3: search, filter, sort and page the portfolio. Everything is done by the server; the table only reports the choices. */
@Component({
  selector: 'app-projects-page',
  imports: [FormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, DataTable],
  template: `
    <div class="page">
      <div class="row">
        <h1 class="grow">Projects</h1>
        @if (auth.isManager()) { <a mat-flat-button routerLink="/projects/new"><mat-icon aria-hidden="true">add</mat-icon> New project</a> }
      </div>

      <div class="row filters">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Search code or name</mat-label>
          <input matInput [ngModel]="query().search" (ngModelChange)="set({ search: $event })" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Department</mat-label>
          <mat-select [ngModel]="query().departmentId" (ngModelChange)="set({ departmentId: $event })">
            <mat-option [value]="undefined">All</mat-option>
            @for (d of departments(); track d.id) { <mat-option [value]="d.id">{{ d.name }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Status</mat-label>
          <mat-select [ngModel]="query().status" (ngModelChange)="set({ status: $event })">
            <mat-option [value]="undefined">All</mat-option>
            @for (s of statuses; track s) { <mat-option [value]="s">{{ s.replace('_', ' ') }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>

      <app-data-table label="Projects" [columns]="columns" [rows]="rows()" [total]="total()" [page]="query().page" [pageSize]="query().pageSize"
                      [status]="status()" [error]="error()" [sortActive]="query().sort" [sortDirection]="query().dir" [clickable]="true"
                      emptyText="No projects match these filters." (sortChange)="onSort($event)" (pageChange)="onPage($event)"
                      (retry)="load()" (rowClick)="open($event)" />
    </div>
  `,
  styles: `.filters { margin-bottom: 8px; } .filters mat-form-field { min-width: 200px; }`,
})
export class ProjectsPage {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ProjectsApi);
  private readonly router = inject(Router);
  private readonly pct = new PctPipe();

  protected readonly statuses = PROJECT_STATUSES;
  protected readonly departments = signal<LookupItem[]>([]);
  protected readonly rows = signal<ProjectListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);
  protected readonly query = signal<ProjectQuery>({ sort: 'code', dir: 'asc', page: 1, pageSize: 20 });

  protected readonly columns: Column<ProjectListItem>[] = [
    { key: 'code', header: 'Code', sortable: true, value: (p) => p.code },
    { key: 'name', header: 'Name', sortable: true, value: (p) => p.name },
    { key: 'department', header: 'Department', sortable: true, value: (p) => p.departmentName },
    { key: 'owner', header: 'Owner', sortable: true, value: (p) => p.ownerName },
    { key: 'status', header: 'Status', sortable: true, badge: (p) => p.status },
    { key: 'progress', header: 'Progress', sortable: true, align: 'end', value: (p) => (p.progressPct === null ? 'No tasks' : this.pct.transform(p.progressPct)) },
    { key: 'utilization', header: 'Budget used', sortable: true, align: 'end', value: (p) => this.pct.transform(p.utilization, 'fraction') },
    { key: 'health', header: 'Health', badge: (p) => p.health },
  ];

  constructor() {
    void firstValueFrom(this.api.departments()).then((d) => this.departments.set(d));
    void this.load();
  }

  protected set(patch: Partial<ProjectQuery>): void {
    this.query.update((q) => ({ ...q, ...patch, page: 1 }));
    void this.load();
  }

  protected onSort(s: Sort): void {
    this.query.update((q) => ({ ...q, sort: s.active || 'code', dir: s.direction === 'desc' ? 'desc' : 'asc', page: 1 }));
    void this.load();
  }

  protected onPage(p: { page: number; pageSize: number }): void {
    this.query.update((q) => ({ ...q, ...p }));
    void this.load();
  }

  protected open(p: ProjectListItem): void {
    void this.router.navigate(['/projects', p.id]);
  }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const page = await firstValueFrom(this.api.list(this.query()));
      this.rows.set(page.items);
      this.total.set(page.totalCount);
      this.status.set('idle');
    } catch (e) {
      this.error.set(toApiError(e));
      this.status.set('error');
    }
  }
}
