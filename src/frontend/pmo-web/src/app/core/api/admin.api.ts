import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Paged } from '../models/common.models';
import { AdminUser, AuditEvent, AuditQuery, AutomationRun, OutboxMessage } from '../models/admin.models';
import { CostCategory } from '../models/finance.models';
import { toParams } from './params';

/** Administrator-only endpoints: users, audit log, automation monitor, cost categories. */
@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  users() {
    return this.http.get<AdminUser[]>('/api/admin/users');
  }
  changeRole(id: number, role: string) {
    return this.http.put<AdminUser>(`/api/admin/users/${id}/role`, { role });
  }
  setActive(id: number, isActive: boolean) {
    return this.http.put<AdminUser>(`/api/admin/users/${id}/status`, { isActive });
  }

  audit(query: AuditQuery) {
    return this.http.get<Paged<AuditEvent>>('/api/admin/audit-events', { params: toParams(query) });
  }

  runs(page: number, pageSize: number) {
    return this.http.get<Paged<AutomationRun>>('/api/admin/automation/runs', { params: toParams({ page, pageSize }) });
  }
  outbox(status: string | undefined, page: number, pageSize: number) {
    return this.http.get<Paged<OutboxMessage>>('/api/admin/automation/outbox', { params: toParams({ status, page, pageSize }) });
  }
  retry(id: number) {
    return this.http.post<OutboxMessage>(`/api/admin/automation/outbox/${id}/retry`, {});
  }

  /** All categories, including inactive ones (the lookup endpoint returns active ones only). */
  categories() {
    return this.http.get<CostCategory[]>('/api/finance/cost-categories');
  }
  createCategory(code: string, name: string) {
    return this.http.post<CostCategory>('/api/finance/cost-categories', { code, name });
  }
  updateCategory(id: number, name: string, isActive: boolean) {
    return this.http.put<CostCategory>(`/api/finance/cost-categories/${id}`, { name, isActive });
  }
}
