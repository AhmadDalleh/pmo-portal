import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { ProjectsApi } from '../../core/api/projects.api';
import { toApiError } from '../../core/http/api-error';
import { AdminUser, ROLES } from '../../core/models/admin.models';
import { ApiError, LoadStatus, LookupItem } from '../../core/models/common.models';
import { Confirm } from '../../shared/confirm-dialog/confirm.service';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { Notify } from '../../shared/notify';

const ROLE_LABEL: Record<string, string> = { Administrator: 'Administrator', ProjectManager: 'Project Manager', Contributor: 'Contributor' };

/**
 * Story A4. The API returns every user (a few dozen), so filtering and paging happen here;
 * role and active-flag changes are always confirmed, sent to the server, and the list is read again.
 * The server refuses an administrator who tries to change their own role or deactivate themselves: its message is shown as it is.
 */
@Component({
  selector: 'app-admin-users',
  imports: [DataTable, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatMenuModule, MatSelectModule],
  template: `
    <div class="row filters">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Role</mat-label>
        <mat-select [ngModel]="roleFilter()" (ngModelChange)="setRole($event)">
          <mat-option [value]="undefined">All roles</mat-option>
          @for (r of roles; track r) { <mat-option [value]="r">{{ label(r) }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Department</mat-label>
        <mat-select [ngModel]="departmentFilter()" (ngModelChange)="setDepartment($event)">
          <mat-option [value]="undefined">All departments</mat-option>
          @for (d of departments(); track d.id) { <mat-option [value]="d.id">{{ d.name }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <span class="muted">{{ filtered().length }} user(s)</span>
    </div>

    <app-data-table label="Users" [columns]="columns" [rows]="pageRows()" [total]="filtered().length" [page]="page()" [pageSize]="pageSize()"
                    [status]="status()" [error]="error()" emptyText="No users match these filters." [actions]="rowActions"
                    (pageChange)="onPage($event)" (retry)="load()" />

    <ng-template #rowActions let-u>
      <button mat-icon-button type="button" [matMenuTriggerFor]="roleMenu" [attr.aria-label]="'Change role of ' + u.fullName"><mat-icon aria-hidden="true">manage_accounts</mat-icon></button>
      <mat-menu #roleMenu="matMenu">
        @for (r of roles; track r) {
          <button mat-menu-item type="button" [disabled]="r === u.role" (click)="changeRole(u, r)">Make {{ label(r) }}</button>
        }
      </mat-menu>
      <button mat-icon-button type="button" [attr.aria-label]="(u.isActive ? 'Deactivate ' : 'Activate ') + u.fullName" (click)="toggleActive(u)">
        <mat-icon aria-hidden="true">{{ u.isActive ? 'person_off' : 'person_add' }}</mat-icon>
      </button>
    </ng-template>
  `,
  styles: `.filters { margin-bottom: 8px; } .filters mat-form-field { min-width: 200px; }`,
})
export class AdminUsers {
  private readonly api = inject(AdminApi);
  private readonly projects = inject(ProjectsApi);
  private readonly confirm = inject(Confirm);
  private readonly notify = inject(Notify);

  protected readonly roles = ROLES;
  protected readonly users = signal<AdminUser[]>([]);
  protected readonly departments = signal<LookupItem[]>([]);
  protected readonly roleFilter = signal<string | undefined>(undefined);
  protected readonly departmentFilter = signal<number | undefined>(undefined);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly filtered = computed(() => this.users().filter((u) =>
    (!this.roleFilter() || u.role === this.roleFilter()) && (!this.departmentFilter() || u.departmentId === this.departmentFilter())));
  protected readonly pageRows = computed(() => this.filtered().slice((this.page() - 1) * this.pageSize(), this.page() * this.pageSize()));

  protected readonly columns: Column<AdminUser>[] = [
    { key: 'name', header: 'Name', value: (u) => u.fullName },
    { key: 'email', header: 'Email', value: (u) => u.email },
    { key: 'department', header: 'Department', value: (u) => this.departments().find((d) => d.id === u.departmentId)?.name ?? u.departmentId },
    { key: 'role', header: 'Role', value: (u) => this.label(u.role) },
    { key: 'active', header: 'Status', badge: (u) => (u.isActive ? 'active' : 'inactive') },
  ];

  constructor() {
    void firstValueFrom(this.projects.departments()).then((d) => this.departments.set(d));
    void this.load();
  }

  protected label(role: string): string { return ROLE_LABEL[role] ?? role; }
  protected setRole(v: string | undefined): void { this.roleFilter.set(v); this.page.set(1); }
  protected setDepartment(v: number | undefined): void { this.departmentFilter.set(v); this.page.set(1); }
  protected onPage(p: { page: number; pageSize: number }): void { this.page.set(p.page); this.pageSize.set(p.pageSize); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try { this.users.set(await firstValueFrom(this.api.users())); this.status.set('idle'); }
    catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async changeRole(u: AdminUser, role: string): Promise<void> {
    const ok = await this.confirm.ask({
      title: `Make ${u.fullName} a ${this.label(role)}?`,
      message: `Currently ${this.label(u.role)}. Leaving Project Manager turns their manager memberships into contributor ones; a PM who still owns projects cannot be changed. The change is audited.`,
      confirmLabel: 'Change role',
    });
    if (ok) await this.run(() => firstValueFrom(this.api.changeRole(u.id, role)), 'Role changed.');
  }

  protected async toggleActive(u: AdminUser): Promise<void> {
    const ok = await this.confirm.ask({
      title: `${u.isActive ? 'Deactivate' : 'Activate'} ${u.fullName}?`,
      message: u.isActive ? 'They can no longer sign in and their sessions end. Their data stays.' : 'They can sign in again.',
      confirmLabel: u.isActive ? 'Deactivate' : 'Activate',
    });
    if (ok) await this.run(() => firstValueFrom(this.api.setActive(u.id, !u.isActive)), 'Status changed.');
  }

  /** The server's reason is shown as it is (for example: you cannot change your own role). The list is read again either way. */
  private async run(action: () => Promise<unknown>, success: string): Promise<void> {
    try { await action(); this.notify.success(success); } catch (e) { this.notify.error(toApiError(e)); }
    await this.load();
  }
}
