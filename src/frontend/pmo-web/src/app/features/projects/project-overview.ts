import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { TasksApi } from '../../core/api/tasks.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { ProgressSnapshot } from '../../core/models/delivery.models';
import { ProjectSummary } from '../../core/models/project.models';
import { AsyncState } from '../../shared/async-state/async-state';
import { ChartCard } from '../../shared/chart-card/chart-card';
import { AedPipe, PctPipe } from '../../shared/format.pipes';
import { KpiCard } from '../../shared/kpi-card/kpi-card';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { ProjectContext } from './project-context';
import { ProjectEditDialog } from './project-edit-dialog';

/** Story B4: delivery, governance and finance numbers of one project, all computed by the API as of one date. */
@Component({
  selector: 'app-project-overview',
  imports: [AsyncState, KpiCard, ChartCard, StatusBadge, MatButtonModule, MatIconModule, RouterLink, AedPipe, PctPipe],
  template: `
    <div class="row">
      <h2 class="grow">Where the project stands</h2>
      @if (ctx.canManage()) { <button mat-stroked-button type="button" (click)="edit()"><mat-icon aria-hidden="true">edit</mat-icon> Edit project</button> }
    </div>
    <app-async-state [status]="status()" [error]="error()" (retry)="load()">
      @if (summary(); as s) {
        <div class="row alerts">
          <app-status-badge [value]="s.health" />
          <span class="muted">Figures as of {{ s.asOf }}</span>
          @if (s.finance.nearBudget) { <span class="flag warn"><mat-icon aria-hidden="true">warning</mat-icon> Near budget (90-100% used)</span> }
          @if (s.finance.overspent) { <span class="flag bad"><mat-icon aria-hidden="true">error</mat-icon> Overspent</span> }
          @if (s.finance.projectedOverrun) { <span class="flag bad"><mat-icon aria-hidden="true">trending_up</mat-icon> Projected overrun (EAC above budget)</span> }
          @if (s.finance.noForecast) { <span class="flag"><mat-icon aria-hidden="true">help_outline</mat-icon> No forecast</span> }
          @if (s.finance.noBudget) { <span class="flag bad"><mat-icon aria-hidden="true">block</mat-icon> Spend without a budget</span> }
        </div>

        <h2>Delivery</h2>
        <div class="card-grid">
          <app-kpi-card label="Progress" [value]="s.delivery.progressPct === null ? '-' : (s.delivery.progressPct | pct)"
                        [hint]="s.delivery.progressPct === null ? 'No tasks yet' : ''" how="Weight-averaged progress of non-cancelled tasks: SUM(weight x progress) / SUM(weight)." />
          <app-kpi-card label="Overdue tasks" [value]="'' + s.delivery.overdueTasks" [tone]="s.delivery.overdueTasks > 0 ? 'bad' : 'normal'"
                        how="Open tasks due before the as-of date. A task due on the as-of date is due today, not overdue." />
          <app-kpi-card label="Next milestone" [value]="s.delivery.nextMilestone?.name ?? 'None pending'"
                        [hint]="s.delivery.nextMilestone ? (s.delivery.nextMilestone.isOverdue ? 'Overdue since ' : 'Due ') + s.delivery.nextMilestone.dueDate : ''"
                        [tone]="s.delivery.nextMilestone?.isOverdue ? 'bad' : 'normal'" how="The pending milestone with the earliest due date." />
          <app-kpi-card label="Open high-exposure risks" [value]="'' + s.governance.openHighExposureRisks" [tone]="s.governance.openHighExposureRisks > 0 ? 'warn' : 'normal'"
                        how="Open or mitigating risks and issues with probability x impact of 15 or more." />
          <app-kpi-card label="Pending change requests" [value]="'' + s.governance.pendingChangeRequests" how="Change requests waiting for a decision (status submitted)." />
        </div>

        <h2>Finance <a routerLink="../finance" class="more">Open finance</a></h2>
        <div class="card-grid">
          <app-kpi-card label="Current approved budget" [value]="s.finance.currentBudget | aed" how="Initial budget plus approved revisions whose effective date is on or before the as-of date." />
          <app-kpi-card label="Actual spend" [value]="s.finance.actualSpend | aed" how="Posted costs dated on or before the as-of date. Drafts and later-dated costs are excluded; reversals net out." />
          <app-kpi-card label="Remaining" [value]="s.finance.remainingBudget | aed" [tone]="s.finance.remainingBudget < 0 ? 'bad' : 'normal'" how="Current approved budget minus actual spend." />
          <app-kpi-card label="Budget used" [value]="s.finance.utilization === null ? '-' : (s.finance.utilization | pct: 'fraction')"
                        [hint]="s.finance.noBudget ? 'No budget set' : ''" how="Actual spend / current approved budget. Undefined when the budget is zero." />
          <app-kpi-card label="Forecast to complete" [value]="s.finance.forecastToComplete | aed" [hint]="s.finance.noForecast ? 'No forecast entered' : ''"
                        how="Forecast amounts for months after the as-of month." />
          <app-kpi-card label="EAC" [value]="s.finance.eac | aed" how="Estimate at completion = actual spend + forecast to complete." />
          <app-kpi-card label="Projected variance" [value]="s.finance.projectedVariance | aed" [tone]="s.finance.projectedVariance < 0 ? 'bad' : 'normal'"
                        how="Current approved budget minus EAC. Negative means a projected overrun." />
        </div>

        <div class="chart"><app-chart-card title="Progress history" note="Monthly snapshots" [option]="historyOption()" [status]="historyStatus()" [error]="historyError()"
                                           [empty]="history().length === 0" emptyText="No snapshots yet." (retry)="loadHistory()" /></div>
      }
    </app-async-state>
  `,
  styles: `
    .alerts { margin-bottom: 4px; }
    .flag { display: inline-flex; align-items: center; gap: 4px; font-size: 13px; padding: 2px 8px; border-radius: 10px; background: var(--mat-sys-surface-container); }
    .flag.warn { background: #fdecc8; } .flag.bad { background: #fbdada; }
    .flag mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .more { font-size: 13px; font-weight: 400; margin-left: 8px; }
    .chart { margin-top: 16px; }
  `,
})
export class ProjectOverview {
  protected readonly ctx = inject(ProjectContext);
  private readonly projects = inject(ProjectsApi);
  private readonly tasks = inject(TasksApi);
  private readonly dialog = inject(MatDialog);

  protected readonly summary = signal<ProjectSummary | null>(null);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);
  protected readonly history = signal<ProgressSnapshot[]>([]);
  protected readonly historyStatus = signal<LoadStatus>('idle');
  protected readonly historyError = signal<ApiError | null>(null);

  constructor() {
    effect(() => { if (this.ctx.id()) { void this.load(); void this.loadHistory(); } });
  }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      this.summary.set(await firstValueFrom(this.projects.summary(this.ctx.id())));
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async loadHistory(): Promise<void> {
    this.historyStatus.set('loading');
    try {
      this.history.set(await firstValueFrom(this.tasks.history(this.ctx.id())));
      this.historyStatus.set('idle');
    } catch (e) { this.historyError.set(toApiError(e)); this.historyStatus.set('error'); }
  }

  protected historyOption() {
    const h = this.history();
    return {
      tooltip: { trigger: 'axis' },
      grid: { left: 48, right: 48, top: 24, bottom: 28 },
      xAxis: { type: 'category', data: h.map((p) => p.snapshotDate) },
      yAxis: [{ type: 'value', min: 0, max: 100, axisLabel: { formatter: '{value}%' } }, { type: 'value', axisLabel: { formatter: (v: number) => `${Math.round(v / 1000)}k` } }],
      legend: { bottom: 0 },
      series: [
        { name: 'Progress %', type: 'line', data: h.map((p) => p.progressPct) },
        { name: 'Actual spend (AED)', type: 'line', yAxisIndex: 1, data: h.map((p) => p.actualSpend), lineStyle: { type: 'dashed' } },
      ],
    };
  }

  protected async edit(): Promise<void> {
    const project = this.ctx.project();
    if (!project) return;
    const ref = this.dialog.open(ProjectEditDialog, { data: project, width: '720px', maxWidth: '96vw' });
    await firstValueFrom(ref.afterClosed());
    await this.ctx.reload();   // the header, the version and every number come from the server again
    await this.load();
  }
}
