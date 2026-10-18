import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { LookupItem, Paged } from '../models/common.models';
import { BudgetRevision, CategorySpend, Cost, CostBody, FinanceSummary, ForecastCell, MonthlyPoint } from '../models/finance.models';
import { toParams } from './params';

@Injectable({ providedIn: 'root' })
export class FinanceApi {
  private readonly http = inject(HttpClient);

  summary(projectId: number, asOf?: string) {
    return this.http.get<FinanceSummary>(`/api/projects/${projectId}/finance/summary`, { params: toParams({ asOf }) });
  }
  byCategory(projectId: number, asOf?: string) {
    return this.http.get<CategorySpend[]>(`/api/projects/${projectId}/finance/by-category`, { params: toParams({ asOf }) });
  }
  monthly(projectId: number, asOf?: string) {
    return this.http.get<MonthlyPoint[]>(`/api/projects/${projectId}/finance/monthly`, { params: toParams({ asOf }) });
  }
  costs(projectId: number, query: { status?: string; page: number; pageSize: number }) {
    return this.http.get<Paged<Cost>>(`/api/projects/${projectId}/costs`, { params: toParams(query) });
  }
  /** The key is generated when the form opens, so a double click or a retry cannot create two costs. */
  createDraft(projectId: number, body: CostBody, idempotencyKey: string) {
    return this.http.post<Cost>(`/api/projects/${projectId}/costs`, body, { headers: { 'Idempotency-Key': idempotencyKey } });
  }
  updateDraft(costId: number, body: CostBody & { version: number }) {
    return this.http.put<Cost>(`/api/finance/costs/${costId}`, body);
  }
  deleteDraft(costId: number) {
    return this.http.delete<void>(`/api/finance/costs/${costId}`);
  }
  post(costId: number, version: number) {
    return this.http.post<Cost>(`/api/finance/costs/${costId}/post`, { version });
  }
  reverse(costId: number, reason: string) {
    return this.http.post<Cost>(`/api/finance/costs/${costId}/reverse`, { reason });
  }
  revisions(projectId: number) {
    return this.http.get<BudgetRevision[]>(`/api/projects/${projectId}/budget-revisions`);
  }
  requestRevision(projectId: number, body: { amount: number; reason: string; effectiveDate: string; changeRequestId: number | null }) {
    return this.http.post<BudgetRevision>(`/api/projects/${projectId}/budget-revisions`, body);
  }
  decideRevision(id: number, body: { decision: 'approved' | 'rejected'; reason: string; version: number }) {
    return this.http.post<BudgetRevision>(`/api/finance/budget-revisions/${id}/decision`, body);
  }
  forecasts(projectId: number) {
    return this.http.get<ForecastCell[]>(`/api/projects/${projectId}/forecasts`);
  }
  saveForecasts(projectId: number, items: { categoryId: number; periodMonth: string; amount: number }[]) {
    return this.http.put<ForecastCell[]>(`/api/projects/${projectId}/forecasts`, { items });
  }
  categories() {
    return this.http.get<LookupItem[]>('/api/lookups/cost-categories');
  }
}
