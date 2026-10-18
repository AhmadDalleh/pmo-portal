import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { NotificationItem } from '../models/auth.models';
import { Paged } from '../models/common.models';
import { ChangeRequest, ChangeRequestBody, ChangeRequestHistoryItem } from '../models/governance.models';
import { toParams } from './params';

/** Change requests, the approvals queue and notifications. */
@Injectable({ providedIn: 'root' })
export class GovernanceApi {
  private readonly http = inject(HttpClient);

  forProject(projectId: number, page: number, pageSize: number) {
    return this.http.get<Paged<ChangeRequest>>(`/api/projects/${projectId}/change-requests`, { params: toParams({ page, pageSize }) });
  }
  get(id: number) {
    return this.http.get<ChangeRequest>(`/api/change-requests/${id}`);
  }
  create(projectId: number, body: ChangeRequestBody) {
    return this.http.post<ChangeRequest>(`/api/projects/${projectId}/change-requests`, body);
  }
  update(id: number, body: ChangeRequestBody & { version: number }) {
    return this.http.put<ChangeRequest>(`/api/change-requests/${id}`, body);
  }
  submit(id: number, version: number) {
    return this.http.post<ChangeRequest>(`/api/change-requests/${id}/submit`, { version });
  }
  withdraw(id: number, version: number, reason: string | null) {
    return this.http.post<ChangeRequest>(`/api/change-requests/${id}/withdraw`, { version, reason });
  }
  history(id: number) {
    return this.http.get<ChangeRequestHistoryItem[]>(`/api/change-requests/${id}/history`);
  }
  queue(page: number, pageSize: number) {
    return this.http.get<Paged<ChangeRequest>>('/api/approvals/change-requests', { params: toParams({ page, pageSize }) });
  }
  decide(id: number, body: { decision: 'approve' | 'reject' | 'return'; reason: string; version: number }) {
    return this.http.post<ChangeRequest>(`/api/approvals/change-requests/${id}/decision`, body);
  }

  notifications(page: number, pageSize: number) {
    return this.http.get<Paged<NotificationItem>>('/api/notifications', { params: toParams({ page, pageSize }) });
  }
  unreadCount() {
    return this.http.get<number>('/api/notifications/unread-count');
  }
  markRead(id: number) {
    return this.http.post<NotificationItem>(`/api/notifications/${id}/read`, {});
  }
}
