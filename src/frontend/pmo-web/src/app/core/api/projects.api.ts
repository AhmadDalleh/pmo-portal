import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { LookupItem, Paged, UserLookup } from '../models/common.models';
import { Member, Project, ProjectListItem, ProjectQuery, ProjectSummary } from '../models/project.models';
import { toParams } from './params';

@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);

  list(query: ProjectQuery) {
    return this.http.get<Paged<ProjectListItem>>('/api/projects', { params: toParams(query) });
  }
  get(id: number) {
    return this.http.get<Project>(`/api/projects/${id}`);
  }
  summary(id: number, asOf?: string) {
    return this.http.get<ProjectSummary>(`/api/projects/${id}/summary`, { params: toParams({ asOf }) });
  }
  create(body: { code: string; name: string; description: string | null; departmentId: number; ownerId: number | null; startDate: string; plannedEndDate: string; initialBudget: number }) {
    return this.http.post<Project>('/api/projects', body);
  }
  update(id: number, body: { name: string; description: string | null; ownerId: number; status: string; startDate: string; plannedEndDate: string; actualEndDate: string | null; version: number }) {
    return this.http.put<Project>(`/api/projects/${id}`, body);
  }
  members(id: number) {
    return this.http.get<Member[]>(`/api/projects/${id}/members`);
  }
  addMember(id: number, userId: number, memberRole: string) {
    return this.http.post<Member>(`/api/projects/${id}/members`, { userId, memberRole });
  }
  removeMember(id: number, userId: number) {
    return this.http.delete<void>(`/api/projects/${id}/members/${userId}`);
  }
  departments() {
    return this.http.get<LookupItem[]>('/api/lookups/departments');
  }
  users(role?: string) {
    return this.http.get<UserLookup[]>('/api/lookups/users', { params: toParams({ role }) });
  }
}
