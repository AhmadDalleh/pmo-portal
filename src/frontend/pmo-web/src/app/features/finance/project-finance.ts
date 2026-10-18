import { Component, effect, inject, signal } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/api/finance.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { CategorySpend, FinanceSummary, MonthlyPoint } from '../../core/models/finance.models';
import { AsyncState } from '../../shared/async-state/async-state';
import { ChartCard } from '../../shared/chart-card/chart-card';
import { AedPipe, PctPipe } from '../../shared/format.pipes';
import { KpiCard } from '../../shared/kpi-card/kpi-card';
import { ProjectContext } from '../projects/project-context';
import { ForecastGrid } from './forecast-grid';
import { ProjectCosts } from './project-costs';
import { ProjectRevisions } from './project-revisions';

/** Story F1/F9/F10: budget overview, spend charts and the tabs for costs, revisions and forecast. Numbers always come from the API. */
@Component({
  selector: 'app-project-finance',
  imports: [AsyncState, KpiCard, ChartCard, MatTabsModule, ProjectCosts, ProjectRevisions, ForecastGrid, AedPipe, PctPipe],
  template: `
    <h2>Budget overview</h2>
    <app-async-state [status]="status()" [error]="error()" (retry)="load()">
      @if (summary(); as s) {
        <p class="muted">As of {{ s.asOf }} · {{ s.currency }}@if (s.pendingRevisions > 0) { · {{ s.pendingRevisions }} revision(s) waiting for approval (not counted) }</p>
        <div class="card-grid">
          <app-kpi-card label="Initial budget" [value]="s.initialBudget | aed" how="The budget approved when the project was created." />
          <app-kpi-card label="Approved revisions" [value]="s.approvedRevisions | aed" how="Sum of approved revisions effective on or before the as-of date. Pending and rejected ones are ignored." />
          <app-kpi-card label="Current approved budget" [value]="s.currentBudget | aed" how="Initial budget plus approved revisions." />
          <app-kpi-card label="Actual spend" [value]="s.actualSpend | aed" how="Posted costs dated on or before the as-of date; drafts excluded; reversals net out." />
          <app-kpi-card label="Remaining" [value]="s.remainingBudget | aed" [tone]="s.remainingBudget < 0 ? 'bad' : 'normal'" how="Current approved budget minus actual spend." />
          <app-kpi-card label="Budget used" [value]="s.utilization === null ? '-' : (s.utilization | pct: 'fraction')" [tone]="s.overspent ? 'bad' : s.nearBudget ? 'warn' : 'normal'"
                        [hint]="s.overspent ? 'Overspent' : s.nearBudget ? 'Near budget' : s.noBudget ? 'No budget set' : ''" how="Actual spend / current approved budget." />
          <app-kpi-card label="Forecast to complete" [value]="s.forecastToComplete | aed" [hint]="s.noForecast ? 'No forecast entered' : ''" how="Forecast for months after the as-of month." />
          <app-kpi-card label="EAC" [value]="s.eac | aed" how="Estimate at completion: actual spend + forecast to complete." />
          <app-kpi-card label="Projected variance" [value]="s.projectedVariance | aed" [tone]="s.projectedOverrun ? 'bad' : 'normal'" [hint]="s.projectedOverrun ? 'Projected overrun' : ''"
                        how="Current approved budget minus EAC. Negative means a projected overrun." />
        </div>
      }
    </app-async-state>

    <div class="charts">
      <app-chart-card title="Spend by category" [option]="categoryOption()" [status]="status()" [error]="error()" [empty]="categories().length === 0" (retry)="load()" [height]="260" />
      <app-chart-card title="Monthly spend and forecast" [option]="monthlyOption()" [status]="status()" [error]="error()" [empty]="monthly().length === 0" (retry)="load()" [height]="260" />
    </div>

    <mat-tab-group class="tabs" animationDuration="0ms">
      <mat-tab label="Costs"><div class="pad"><app-project-costs [projectId]="ctx.id()" [canManage]="ctx.canManage()" (changed)="load()" /></div></mat-tab>
      <mat-tab label="Budget revisions"><div class="pad"><app-project-revisions [projectId]="ctx.id()" [canManage]="ctx.canManage()" (changed)="load()" /></div></mat-tab>
      <mat-tab label="Forecast"><div class="pad"><app-forecast-grid [projectId]="ctx.id()" [canManage]="ctx.canManage()" (changed)="load()" /></div></mat-tab>
    </mat-tab-group>
  `,
  styles: `
    .charts { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 12px; margin: 16px 0; }
    .tabs { margin-top: 8px; } .pad { padding: 12px 0; }
  `,
})
export class ProjectFinance {
  protected readonly ctx = inject(ProjectContext);
  private readonly api = inject(FinanceApi);

  protected readonly summary = signal<FinanceSummary | null>(null);
  protected readonly categories = signal<CategorySpend[]>([]);
  protected readonly monthly = signal<MonthlyPoint[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  constructor() { effect(() => { if (this.ctx.id()) void this.load(); }); }

  /** Called on first view and after every posting, reversal, revision or forecast save: always a fresh read from the server. */
  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const id = this.ctx.id();
      const [summary, categories, monthly] = await Promise.all([firstValueFrom(this.api.summary(id)), firstValueFrom(this.api.byCategory(id)), firstValueFrom(this.api.monthly(id))]);
      this.summary.set(summary);
      this.categories.set(categories);
      this.monthly.set(monthly);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected categoryOption() {
    const c = this.categories();
    return {
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } }, legend: { bottom: 0 }, grid: { left: 60, right: 16, top: 16, bottom: 36 },
      xAxis: { type: 'category', data: c.map((x) => x.name) }, yAxis: { type: 'value', axisLabel: { formatter: (v: number) => `${Math.round(v / 1000)}k` } },
      series: [{ name: 'Actual (AED)', type: 'bar', data: c.map((x) => x.actualSpend) }, { name: 'Forecast (AED)', type: 'bar', data: c.map((x) => x.forecastToComplete) }],
    };
  }

  protected monthlyOption() {
    const m = this.monthly();
    return {
      tooltip: { trigger: 'axis' }, legend: { bottom: 0 }, grid: { left: 60, right: 16, top: 16, bottom: 36 },
      xAxis: { type: 'category', data: m.map((x) => x.month.slice(0, 7)) }, yAxis: { type: 'value', axisLabel: { formatter: (v: number) => `${Math.round(v / 1000)}k` } },
      series: [{ name: 'Actual (AED)', type: 'bar', data: m.map((x) => x.actualSpend) }, { name: 'Forecast (AED)', type: 'line', data: m.map((x) => x.forecast) }],
    };
  }
}
