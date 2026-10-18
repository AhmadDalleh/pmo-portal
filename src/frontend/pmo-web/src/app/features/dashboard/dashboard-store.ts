import { Injectable, inject, signal } from '@angular/core';
import { ParamMap } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DashboardApi } from '../../core/api/dashboard.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import {
  DashboardFilter, DashboardKpis, OverdueByOwner, ProjectFinanceBar, ProjectHealthRow, RiskMatrixCell,
} from '../../core/models/dashboard.models';
import { CategorySpend, MonthlyPoint } from '../../core/models/finance.models';

/** One widget's data with its own loading / error state and its own Retry. */
export class Widget<T> {
  readonly data = signal<T | null>(null);
  readonly status = signal<LoadStatus>('idle');
  readonly error = signal<ApiError | null>(null);

  async run(fetch: () => Promise<T>): Promise<void> {
    this.status.set('loading');
    try { this.data.set(await fetch()); this.status.set('idle'); }
    catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }
}

const num = (v: string | null) => (v === null || v === '' || Number.isNaN(Number(v)) ? undefined : Number(v));
const text = (v: string | null) => (v === null || v === '' ? undefined : v);

/** The filters are the URL query string: shareable, survive reload, and the back button works. */
export function filterFromParams(p: ParamMap): DashboardFilter {
  return {
    departmentId: num(p.get('departmentId')), projectId: num(p.get('projectId')), managerId: num(p.get('managerId')), status: text(p.get('status')),
    from: text(p.get('from')), to: text(p.get('to')), categoryId: num(p.get('categoryId')), asOf: text(p.get('asOf')),
  };
}

/**
 * Every widget of the dashboard. loadAll() refetches all of them for one filter, so a filter change updates the whole page.
 * Nothing here calculates a KPI: the values are what the API returned.
 */
@Injectable()
export class DashboardStore {
  private readonly api = inject(DashboardApi);
  private current: DashboardFilter = {};

  readonly kpis = new Widget<DashboardKpis>();
  readonly byProject = new Widget<ProjectFinanceBar[]>();
  readonly monthly = new Widget<MonthlyPoint[]>();
  readonly byCategory = new Widget<CategorySpend[]>();
  readonly overdue = new Widget<OverdueByOwner[]>();
  readonly matrix = new Widget<RiskMatrixCell[]>();
  readonly health = new Widget<ProjectHealthRow[]>();

  loadAll(filter: DashboardFilter): Promise<unknown> {
    this.current = filter;
    return Promise.all([this.loadKpis(), this.loadByProject(), this.loadMonthly(), this.loadByCategory(), this.loadOverdue(), this.loadMatrix(), this.loadHealth()]);
  }

  loadKpis() { return this.kpis.run(() => firstValueFrom(this.api.kpis(this.current))); }
  loadByProject() { return this.byProject.run(() => firstValueFrom(this.api.byProject(this.current))); }
  loadMonthly() { return this.monthly.run(() => firstValueFrom(this.api.monthly(this.current))); }
  loadByCategory() { return this.byCategory.run(() => firstValueFrom(this.api.byCategory(this.current))); }
  loadOverdue() { return this.overdue.run(() => firstValueFrom(this.api.overdueByOwner(this.current))); }
  loadMatrix() { return this.matrix.run(() => firstValueFrom(this.api.riskMatrix(this.current))); }
  loadHealth() { return this.health.run(() => firstValueFrom(this.api.projectHealth(this.current))); }
}
