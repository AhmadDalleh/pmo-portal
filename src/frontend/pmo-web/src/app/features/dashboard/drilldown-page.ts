import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { DashboardApi } from '../../core/api/dashboard.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { DrilldownCost, DrilldownRisk, DrilldownTask } from '../../core/models/dashboard.models';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { AedPipe } from '../../shared/format.pipes';
import { filterFromParams } from './dashboard-store';

/**
 * Story G4 / F11: the records behind a dashboard number. The filters arrive in the URL exactly as the dashboard had them,
 * so the server applies the same project filter and cutoff. For costs, the total is shown next to the KPI it must equal.
 */
@Component({
  selector: 'app-drilldown-page',
  imports: [DataTable, RouterLink, MatButtonModule, MatIconModule],
  template: `
    <div class="page">
      <a mat-button routerLink="/dashboard" [queryParams]="backParams()"><mat-icon aria-hidden="true">arrow_back</mat-icon> Back to the dashboard</a>
      <h1>{{ title() }}</h1>
      <p class="muted" role="status">
        {{ total() }} record(s)@if (kind() === 'costs') { · total {{ money(totalAmount()) }}
          @if (kpiActual() !== null) { · {{ kpiActual() === totalAmount() ? 'matches' : 'does NOT match' }} the "Actual spend" card ({{ money(kpiActual()!) }}) }
          @else if (hasCategory()) { · filtered by one category, so it is smaller than the "Actual spend" card }
        }
      </p>
      @switch (kind()) {
        @case ('costs') {
          <app-data-table label="Posted costs" [columns]="costColumns" [rows]="costs()" [total]="total()" [page]="page()" [pageSize]="pageSize()" [status]="status()" [error]="error()" (pageChange)="onPage($event)" (retry)="load()" />
        }
        @case ('tasks') {
          <app-data-table label="Overdue tasks" [columns]="taskColumns" [rows]="tasks()" [total]="total()" [page]="page()" [pageSize]="pageSize()" [status]="status()" [error]="error()" (pageChange)="onPage($event)" (retry)="load()" />
        }
        @case ('risks') {
          <app-data-table label="Open risks and issues" [columns]="riskColumns" [rows]="risks()" [total]="total()" [page]="page()" [pageSize]="pageSize()" [status]="status()" [error]="error()" (pageChange)="onPage($event)" (retry)="load()" />
        }
      }
    </div>
  `,
})
export class DrilldownPage {
  /** 'costs' | 'tasks' | 'risks' from the route (withComponentInputBinding). */
  readonly kind = input.required<string>();

  private readonly api = inject(DashboardApi);
  private readonly route = inject(ActivatedRoute);
  private readonly aed = new AedPipe();

  protected readonly costs = signal<DrilldownCost[]>([]);
  protected readonly tasks = signal<DrilldownTask[]>([]);
  protected readonly risks = signal<DrilldownRisk[]>([]);
  protected readonly total = signal(0);
  protected readonly totalAmount = signal(0);
  protected readonly kpiActual = signal<number | null>(null);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly costColumns: Column<DrilldownCost>[] = [
    { key: 'date', header: 'Date', value: (c) => c.txnDate }, { key: 'project', header: 'Project', value: (c) => c.projectCode },
    { key: 'category', header: 'Category', value: (c) => c.categoryName }, { key: 'description', header: 'Description', value: (c) => c.description },
    { key: 'vendor', header: 'Vendor', value: (c) => c.vendorName }, { key: 'amount', header: 'Amount', align: 'end', value: (c) => this.aed.transform(c.amount, 'cents') },
  ];
  protected readonly taskColumns: Column<DrilldownTask>[] = [
    { key: 'project', header: 'Project', value: (t) => t.projectCode }, { key: 'title', header: 'Task', value: (t) => t.title },
    { key: 'assignee', header: 'Assignee', value: (t) => t.assigneeName }, { key: 'status', header: 'Status', badge: (t) => t.status },
    { key: 'due', header: 'Due', value: (t) => t.dueDate }, { key: 'days', header: 'Days overdue', align: 'end', value: (t) => t.daysOverdue },
    { key: 'progress', header: 'Progress', align: 'end', value: (t) => `${t.progressPct}%` },
  ];
  protected readonly riskColumns: Column<DrilldownRisk>[] = [
    { key: 'project', header: 'Project', value: (r) => r.projectCode }, { key: 'type', header: 'Type', value: (r) => r.type },
    { key: 'title', header: 'Title', value: (r) => r.title }, { key: 'owner', header: 'Owner', value: (r) => r.ownerName },
    { key: 'score', header: 'Score (P x I)', align: 'end', value: (r) => `${r.score} (${r.probability} x ${r.impact})` }, { key: 'status', header: 'Status', badge: (r) => r.status },
  ];

  /** Tracks only `kind`; page changes call load() themselves, so load() must not be tracked. */
  constructor() { effect(() => { this.kind(); untracked(() => void this.load()); }); }

  protected title(): string {
    return ({ costs: 'Posted costs behind actual spend', tasks: 'Overdue tasks', risks: 'Open risks and issues' } as Record<string, string>)[this.kind()] ?? 'Records';
  }
  protected money(v: number): string { return this.aed.transform(v, 'cents'); }
  protected hasCategory(): boolean { return this.route.snapshot.queryParamMap.has('categoryId'); }
  protected backParams() {
    const { departmentId, projectId, managerId, status, from, to, categoryId, asOf } = filterFromParams(this.route.snapshot.queryParamMap);
    return { departmentId, projectId, managerId, status, from, to, categoryId, asOf };
  }
  protected onPage(p: { page: number; pageSize: number }): void { this.page.set(p.page); this.pageSize.set(p.pageSize); void this.load(); }

  protected async load(): Promise<void> {
    const params = this.route.snapshot.queryParamMap;
    const filter = filterFromParams(params);
    const num = (k: string) => (params.get(k) === null ? undefined : Number(params.get(k)));
    this.status.set('loading');
    try {
      if (this.kind() === 'costs') {
        const r = await firstValueFrom(this.api.costs(filter, this.page(), this.pageSize()));
        this.costs.set(r.items); this.total.set(r.totalCount); this.totalAmount.set(r.totalAmount);
        this.kpiActual.set(filter.categoryId === undefined ? (await firstValueFrom(this.api.kpis(filter))).actualSpend : null);
      } else if (this.kind() === 'tasks') {
        const r = await firstValueFrom(this.api.tasks(filter, { assigneeId: num('assigneeId'), unassigned: params.get('unassigned') === 'true' || undefined, page: this.page(), pageSize: this.pageSize() }));
        this.tasks.set(r.items); this.total.set(r.totalCount);
      } else {
        const r = await firstValueFrom(this.api.risks(filter, { probability: num('probability'), impact: num('impact'), highExposure: params.get('highExposure') === 'true' || undefined, page: this.page(), pageSize: this.pageSize() }));
        this.risks.set(r.items); this.total.set(r.totalCount);
      }
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }
}
