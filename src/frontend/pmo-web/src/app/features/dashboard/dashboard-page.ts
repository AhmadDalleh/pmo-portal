import { Component, effect, inject, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { DashboardFilter } from '../../core/models/dashboard.models';
import { ChartCard } from '../../shared/chart-card/chart-card';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { AedPipe, PctPipe } from '../../shared/format.pipes';
import { KpiCard } from '../../shared/kpi-card/kpi-card';
import { ProjectHealthRow } from '../../core/models/dashboard.models';
import { budgetVsActualOption, categoryOption, matrixOption, monthlyOption, overdueOption } from './chart-options';
import { DashboardFilters } from './dashboard-filters';
import { DashboardStore, filterFromParams } from './dashboard-store';

type Point = { dataIndex: number; name: string; data?: unknown };

/**
 * Epic G. The filters are the URL; changing one navigates, and the effect below refetches every widget.
 * KPI cards and charts are drawn from API values only; clicking a number opens the records behind it.
 */
@Component({
  selector: 'app-dashboard-page',
  imports: [DashboardFilters, KpiCard, ChartCard, DataTable, AedPipe, PctPipe],
  providers: [DashboardStore],
  template: `
    <div class="page">
      <h1>Executive dashboard</h1>
      <app-dashboard-filters [filter]="filter()" (changed)="setFilter($event)" (clear)="clearFilters()" />

      <h2>Delivery</h2>
      <div class="card-grid">
        @if (store.kpis.data(); as k) {
          <app-kpi-card label="Projects" [value]="'' + k.totalProjects" [hint]="k.activeProjects + ' active'" how="Projects in your scope that match the filters." />
          <app-kpi-card label="Portfolio progress" [value]="k.portfolioProgressPct === null ? '-' : (k.portfolioProgressPct | pct)" how="Task-weighted: SUM(weight x progress) / SUM(weight) over all in-scope, non-cancelled tasks (not an average of projects)." />
          <app-kpi-card label="Overdue tasks" [value]="'' + k.overdueTasks" [tone]="k.overdueTasks > 0 ? 'bad' : 'normal'" [clickable]="true" (select)="drill('tasks', {})"
                        how="Open tasks due before the as-of date. Click to see the tasks." />
          <app-kpi-card label="Milestones due in 14 days" [value]="'' + k.milestonesDueSoon" how="Pending milestones due from the as-of date up to 14 days later." />
        }
      </div>
      <h2>Risk &amp; governance</h2>
      <div class="card-grid">
        @if (store.kpis.data(); as k) {
          <app-kpi-card label="Open high-exposure risks" [value]="'' + k.openHighRisks" [tone]="k.openHighRisks > 0 ? 'warn' : 'normal'" [clickable]="true" (select)="drill('risks', { highExposure: true })"
                        how="Open or mitigating risks and issues with score (probability x impact) of 15 or more. Click to see them." />
          <app-kpi-card label="Blocked tasks" [value]="'' + k.blockedTasks" [tone]="k.blockedTasks > 0 ? 'warn' : 'normal'" how="Tasks with status blocked." />
          <app-kpi-card label="Pending change requests" [value]="'' + k.pendingChangeRequests" how="Change requests with status submitted." />
        }
      </div>
      <h2>Finance <span class="muted unit">(AED)</span></h2>
      <div class="card-grid">
        @if (store.kpis.data(); as k) {
          <app-kpi-card label="Current approved budget" [value]="k.currentBudget | aed" how="Initial budgets plus approved revisions effective on or before the as-of date." />
          <app-kpi-card label="Actual spend" [value]="k.actualSpend | aed" [clickable]="true" (select)="drill('costs', {})" how="Posted costs dated on or before the as-of date. Click to see the transactions; their total equals this number." />
          <app-kpi-card label="Remaining" [value]="k.remainingBudget | aed" [tone]="k.remainingBudget < 0 ? 'bad' : 'normal'" how="Current approved budget minus actual spend." />
          <app-kpi-card label="Budget used" [value]="k.utilization === null ? '-' : (k.utilization | pct: 'fraction')" how="Actual spend / current approved budget." />
          <app-kpi-card label="EAC" [value]="k.eac | aed" [hint]="'Forecast to complete ' + (k.forecastToComplete | aed)" how="Estimate at completion = actual spend + forecast to complete (months after the as-of month)." />
          <app-kpi-card label="Projected overrun" [value]="k.projectedOverrunProjects + (k.projectedOverrunProjects === 1 ? ' project' : ' projects')" [hint]="k.projectedOverrunAmount | aed" [tone]="k.projectedOverrunProjects > 0 ? 'bad' : 'normal'"
                        how="Projects where EAC is above the current approved budget, and the total amount above budget." />
        }
      </div>
      @if (store.kpis.status() === 'loading') { <p class="muted" role="status">Loading figures...</p> }
      @if (store.kpis.status() === 'error') { <p class="neg" role="alert">{{ store.kpis.error()?.detail }} (reference {{ store.kpis.error()?.correlationId }}) <button type="button" (click)="store.loadKpis()">Retry</button></p> }

      <h2>Charts <span class="muted unit">(click a bar, slice or cell to see the records behind it)</span></h2>
      <div class="charts">
        <app-chart-card title="Budget vs actual vs EAC by project" note="Category filter does not apply" [option]="budgetOption()" [status]="store.byProject.status()" [error]="store.byProject.error()"
                        [empty]="(store.byProject.data() ?? []).length === 0" (retry)="store.loadByProject()" (pointClick)="onProject($event)" />
        <app-chart-card title="Monthly cost trend (actual + forecast)" note="Respects the category filter" [option]="monthly()" [status]="store.monthly.status()" [error]="store.monthly.error()"
                        [empty]="(store.monthly.data() ?? []).length === 0" (retry)="store.loadMonthly()" />
        <app-chart-card title="Spend by category" note="Respects the category filter" [option]="category()" [status]="store.byCategory.status()" [error]="store.byCategory.error()"
                        [empty]="(store.byCategory.data() ?? []).length === 0" (retry)="store.loadByCategory()" (pointClick)="onCategory($event)" />
        <app-chart-card title="Overdue tasks by owner" note="Category filter does not apply" [option]="overdue()" [status]="store.overdue.status()" [error]="store.overdue.error()"
                        [empty]="(store.overdue.data() ?? []).length === 0" emptyText="No overdue tasks for these filters." (retry)="store.loadOverdue()" (pointClick)="onOwner($event)" />
        <app-chart-card title="Risk matrix (open items)" note="Category filter does not apply" [option]="matrix()" [status]="store.matrix.status()" [error]="store.matrix.error()"
                        [empty]="(store.matrix.data() ?? []).length === 0" (retry)="store.loadMatrix()" (pointClick)="onCell($event)" />
      </div>

      <h2>Project health</h2>
      <app-data-table label="Project health" [columns]="healthColumns" [rows]="store.health.data() ?? []" [status]="store.health.status()" [error]="store.health.error()"
                      [clickable]="true" (rowClick)="openProject($event)" (retry)="store.loadHealth()" emptyText="No projects match these filters." />
    </div>
  `,
  styles: `
    .unit { font-size: 13px; font-weight: 400; }
    .charts { display: grid; grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); gap: 12px; }
    @media (max-width: 480px) { .charts { grid-template-columns: 1fr; } }
  `,
})
export class DashboardPage {
  protected readonly store = inject(DashboardStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly pct = new PctPipe();

  /** The URL is the single source of truth for the filters. */
  protected readonly filter = toSignal(this.route.queryParamMap.pipe(map(filterFromParams)), { initialValue: {} as DashboardFilter });

  protected readonly healthColumns: Column<ProjectHealthRow>[] = [
    { key: 'code', header: 'Project', value: (r) => `${r.code} ${r.name}` },
    { key: 'status', header: 'Status', badge: (r) => r.status },
    { key: 'progress', header: 'Progress', align: 'end', value: (r) => (r.progressPct === null ? 'No tasks' : this.pct.transform(r.progressPct)) },
    { key: 'overdue', header: 'Overdue tasks', align: 'end', value: (r) => r.overdueTasks },
    { key: 'risks', header: 'High risks', align: 'end', value: (r) => r.openHighRisks },
    { key: 'used', header: 'Budget used', align: 'end', value: (r) => this.pct.transform(r.utilization, 'fraction') },
    { key: 'health', header: 'Health', badge: (r) => r.health },
  ];

  constructor() {
    // Every filter change (a new URL) refetches every widget.
    effect(() => { const f = this.filter(); untracked(() => void this.store.loadAll(f)); });
  }

  protected budgetOption() { return budgetVsActualOption(this.store.byProject.data() ?? []); }
  protected monthly() { return monthlyOption(this.store.monthly.data() ?? []); }
  protected category() { return categoryOption(this.store.byCategory.data() ?? []); }
  protected overdue() { return overdueOption(this.store.overdue.data() ?? []); }
  protected matrix() { return matrixOption(this.store.matrix.data() ?? []); }

  protected setFilter(patch: Partial<DashboardFilter>): void {
    const queryParams = Object.fromEntries(Object.entries(patch).map(([k, v]) => [k, v === undefined || v === '' ? null : v]));
    void this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }

  protected clearFilters(): void {
    void this.router.navigate([], { queryParams: { departmentId: null, projectId: null, managerId: null, status: null, from: null, to: null, categoryId: null, asOf: null } });
  }

  protected openProject(r: ProjectHealthRow): void { void this.router.navigate(['/projects', r.projectId]); }

  /** Same filters in the URL of the drill-down page, so the server applies the same WHERE as for the number that was clicked. */
  protected drill(kind: 'costs' | 'tasks' | 'risks', extra: Record<string, string | number | boolean | null>): void {
    const f = this.filter();
    const keep = kind === 'costs' ? f : { ...f, categoryId: undefined };
    const queryParams = Object.fromEntries(Object.entries({ ...keep, ...extra }).filter(([, v]) => v !== undefined && v !== null));
    void this.router.navigate(['/dashboard/drilldown', kind], { queryParams });
  }

  protected onProject(p: Point): void {
    const row = this.store.byProject.data()?.[p.dataIndex];
    if (row) this.drill('costs', { projectId: row.projectId, categoryId: null });
  }

  protected onCategory(p: Point): void {
    const row = this.store.byCategory.data()?.find((c) => c.name === p.name);
    if (row) this.drill('costs', { categoryId: row.categoryId });
  }

  protected onOwner(p: Point): void {
    const rows = [...(this.store.overdue.data() ?? [])].reverse();   // the chart lists them bottom-up
    const row = rows[p.dataIndex];
    if (row) this.drill('tasks', row.userId === null ? { unassigned: true } : { assigneeId: row.userId });
  }

  protected onCell(p: Point): void {
    const d = p.data as number[] | undefined;
    if (d) this.drill('risks', { impact: d[0] + 1, probability: d[1] + 1 });
  }
}
