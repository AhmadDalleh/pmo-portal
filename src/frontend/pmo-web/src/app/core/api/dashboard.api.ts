import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Paged } from '../models/common.models';
import {
  DashboardFilter, DashboardKpis, DrilldownCosts, DrilldownRisk, DrilldownTask, OverdueByOwner, ProjectFinanceBar, ProjectHealthRow, RiskMatrixCell,
} from '../models/dashboard.models';
import { CategorySpend, MonthlyPoint } from '../models/finance.models';
import { toParams } from './params';

/** Every call takes the same DashboardFilter, so each widget answers for exactly the same set of projects. */
@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  private get<T>(path: string, filter: DashboardFilter, extra: object = {}) {
    return this.http.get<T>(`/api/dashboard/${path}`, { params: toParams({ ...filter, ...extra }) });
  }

  kpis(f: DashboardFilter) { return this.get<DashboardKpis>('kpis', f); }
  /** Budget vs actual vs EAC ignores the category filter (a budget has no category). */
  byProject(f: DashboardFilter) { return this.get<ProjectFinanceBar[]>('finance/by-project', { ...f, categoryId: undefined }); }
  monthly(f: DashboardFilter) { return this.get<MonthlyPoint[]>('finance/monthly', f); }
  byCategory(f: DashboardFilter) { return this.get<CategorySpend[]>('finance/by-category', f); }
  overdueByOwner(f: DashboardFilter) { return this.get<OverdueByOwner[]>('delivery/overdue-by-owner', { ...f, categoryId: undefined }); }
  projectHealth(f: DashboardFilter) { return this.get<ProjectHealthRow[]>('delivery/project-health', { ...f, categoryId: undefined }); }
  riskMatrix(f: DashboardFilter) { return this.get<RiskMatrixCell[]>('risks/matrix', { ...f, categoryId: undefined }); }

  costs(f: DashboardFilter, page: number, pageSize: number) { return this.get<DrilldownCosts>('drilldown/costs', f, { page, pageSize }); }
  tasks(f: DashboardFilter, extra: { assigneeId?: number; unassigned?: boolean; page: number; pageSize: number }) {
    return this.get<Paged<DrilldownTask>>('drilldown/tasks', { ...f, categoryId: undefined }, { overdue: true, ...extra });
  }
  risks(f: DashboardFilter, extra: { probability?: number; impact?: number; highExposure?: boolean; page: number; pageSize: number }) {
    return this.get<Paged<DrilldownRisk>>('drilldown/risks', { ...f, categoryId: undefined }, extra);
  }
}
