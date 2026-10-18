import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus, UserLookup } from '../../core/models/common.models';
import { Member } from '../../core/models/project.models';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { Confirm } from '../../shared/confirm-dialog/confirm.service';
import { Notify } from '../../shared/notify';
import { ProjectContext } from './project-context';

/** Story B5: who can see and work on the project. Membership is what grants access (scope). */
@Component({
  selector: 'app-project-members',
  imports: [DataTable, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule],
  template: `
    <h2>Members</h2>
    @if (ctx.canManage()) {
      <div class="row add">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Add person</mat-label>
          <mat-select [(ngModel)]="newUserId">
            @for (u of candidates(); track u.id) { <mat-option [value]="u.id">{{ u.fullName }} ({{ u.role === 'ProjectManager' ? 'PM' : 'Contributor' }})</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>As</mat-label>
          <mat-select [(ngModel)]="newRole"><mat-option value="contributor">Contributor</mat-option><mat-option value="manager">Manager (PMs only)</mat-option></mat-select>
        </mat-form-field>
        <button mat-flat-button type="button" [disabled]="!newUserId" (click)="add()"><mat-icon aria-hidden="true">person_add</mat-icon> Add</button>
      </div>
    }
    <app-data-table label="Project members" [columns]="columns" [rows]="rows()" [status]="status()" [error]="error()" (retry)="load()"
                    [actions]="ctx.canManage() ? remove : null" emptyText="No members yet." />
    <ng-template #remove let-m>
      <button mat-icon-button type="button" [attr.aria-label]="'Remove ' + m.fullName" (click)="removeMember(m)"><mat-icon aria-hidden="true">person_remove</mat-icon></button>
    </ng-template>
  `,
  styles: `.add { margin-bottom: 8px; } .add mat-form-field { min-width: 220px; }`,
})
export class ProjectMembers {
  protected readonly ctx = inject(ProjectContext);
  private readonly api = inject(ProjectsApi);
  private readonly confirm = inject(Confirm);
  private readonly notify = inject(Notify);

  protected readonly rows = signal<Member[]>([]);
  protected readonly candidates = signal<UserLookup[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);
  protected newUserId: number | null = null;
  protected newRole: 'contributor' | 'manager' = 'contributor';

  protected readonly columns: Column<Member>[] = [
    { key: 'fullName', header: 'Name', value: (m) => m.fullName },
    { key: 'email', header: 'Email', value: (m) => m.email },
    { key: 'userRole', header: 'Role', value: (m) => (m.userRole === 'ProjectManager' ? 'Project Manager' : m.userRole) },
    { key: 'memberRole', header: 'On this project', value: (m) => (m.memberRole === 'manager' ? 'Manager' : 'Contributor') },
  ];

  constructor() {
    effect(() => { if (this.ctx.id()) void this.load(); });
  }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      this.rows.set(await firstValueFrom(this.api.members(this.ctx.id())));
      if (this.ctx.canManage()) {
        const everyone = await firstValueFrom(this.api.users());
        const present = new Set(this.rows().map((m) => m.userId));
        this.candidates.set(everyone.filter((u) => !present.has(u.id) && u.role !== 'Administrator'));
      }
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async add(): Promise<void> {
    try {
      await firstValueFrom(this.api.addMember(this.ctx.id(), this.newUserId!, this.newRole));
      this.notify.success('Member added.');
      this.newUserId = null;
      await this.load();
    } catch (e) { this.notify.error(toApiError(e)); }
  }

  protected async removeMember(m: Member): Promise<void> {
    if (!(await this.confirm.ask({ title: `Remove ${m.fullName}?`, message: 'They lose access to this project. Their open tasks must be reassigned first.', confirmLabel: 'Remove' }))) return;
    try {
      await firstValueFrom(this.api.removeMember(this.ctx.id(), m.userId));
      this.notify.success('Member removed.');
      await this.load();
    } catch (e) { this.notify.error(toApiError(e)); }   // 409 lists the tasks that still belong to this person
  }
}
