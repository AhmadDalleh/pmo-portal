import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Paged } from '../models/common.models';
import { Milestone, ProgressSnapshot, Risk, RiskBody, TaskBody, TaskItem } from '../models/delivery.models';
import { toParams } from './params';

/** Tasks, milestones, risks and progress history: everything about delivery of one project. */
@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);

  list(projectId: number, query: { status?: string; assigneeId?: number; overdue?: boolean; sort: string; dir: string; page: number; pageSize: number }) {
    return this.http.get<Paged<TaskItem>>(`/api/projects/${projectId}/tasks`, { params: toParams(query) });
  }
  mine(status: string | undefined, page: number, pageSize: number) {
    return this.http.get<Paged<TaskItem>>('/api/me/tasks', { params: toParams({ status, page, pageSize }) });
  }
  create(projectId: number, body: TaskBody) {
    return this.http.post<TaskItem>(`/api/projects/${projectId}/tasks`, body);
  }
  update(taskId: number, body: TaskBody & { version: number }) {
    return this.http.put<TaskItem>(`/api/tasks/${taskId}`, body);
  }
  updateProgress(taskId: number, body: { progressPct: number; status: string; version: number }) {
    return this.http.patch<TaskItem>(`/api/tasks/${taskId}/progress`, body);
  }

  milestones(projectId: number) {
    return this.http.get<Milestone[]>(`/api/projects/${projectId}/milestones`);
  }
  createMilestone(projectId: number, body: { name: string; description: string | null; dueDate: string }) {
    return this.http.post<Milestone>(`/api/projects/${projectId}/milestones`, body);
  }
  updateMilestone(id: number, body: { name: string; description: string | null; dueDate: string; version: number }) {
    return this.http.put<Milestone>(`/api/milestones/${id}`, body);
  }
  completeMilestone(id: number, version: number) {
    return this.http.post<Milestone>(`/api/milestones/${id}/complete`, { version });
  }

  risks(projectId: number) {
    return this.http.get<Risk[]>(`/api/projects/${projectId}/risks`);
  }
  createRisk(projectId: number, body: RiskBody) {
    return this.http.post<Risk>(`/api/projects/${projectId}/risks`, body);
  }
  updateRisk(id: number, body: RiskBody & { version: number }) {
    return this.http.put<Risk>(`/api/risks/${id}`, body);
  }

  history(projectId: number) {
    return this.http.get<ProgressSnapshot[]>(`/api/projects/${projectId}/progress-history`);
  }
}
