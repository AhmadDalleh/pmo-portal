import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { CostCategory } from '../../core/models/finance.models';
import { Confirm } from '../../shared/confirm-dialog/confirm.service';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { Notify } from '../../shared/notify';
import { CategoryDialog } from './category-dialog';

/**
 * Story F2: cost categories. There is no delete: a category that costs may already use can only be deactivated
 * (it then disappears from the dropdowns, but old costs keep it).
 */
@Component({
  selector: 'app-admin-categories',
  imports: [DataTable, MatButtonModule, MatIconModule],
  template: `
    <div class="row">
      <h2 class="grow">Cost categories</h2>
      <button mat-flat-button type="button" (click)="edit(null)"><mat-icon aria-hidden="true">add</mat-icon> New category</button>
    </div>
    <app-data-table label="Cost categories" [columns]="columns" [rows]="rows()" [status]="status()" [error]="error()" emptyText="No categories." [actions]="rowActions" (retry)="load()" />
    <ng-template #rowActions let-c>
      <button mat-icon-button type="button" [attr.aria-label]="'Rename ' + c.name" (click)="edit(c)"><mat-icon aria-hidden="true">edit</mat-icon></button>
      <button mat-icon-button type="button" [attr.aria-label]="(c.isActive ? 'Deactivate ' : 'Activate ') + c.name" (click)="toggle(c)">
        <mat-icon aria-hidden="true">{{ c.isActive ? 'toggle_on' : 'toggle_off' }}</mat-icon>
      </button>
    </ng-template>
  `,
})
export class AdminCategories {
  private readonly api = inject(AdminApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(Confirm);
  private readonly notify = inject(Notify);

  protected readonly rows = signal<CostCategory[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<CostCategory>[] = [
    { key: 'code', header: 'Code', value: (c) => c.code },
    { key: 'name', header: 'Name', value: (c) => c.name },
    { key: 'active', header: 'Status', badge: (c) => (c.isActive ? 'active' : 'inactive') },
  ];

  constructor() { void this.load(); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try { this.rows.set(await firstValueFrom(this.api.categories())); this.status.set('idle'); }
    catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async edit(category: CostCategory | null): Promise<void> {
    await firstValueFrom(this.dialog.open(CategoryDialog, { data: category, width: '440px', maxWidth: '96vw' }).afterClosed());
    await this.load();
  }

  protected async toggle(c: CostCategory): Promise<void> {
    const ok = await this.confirm.ask({
      title: `${c.isActive ? 'Deactivate' : 'Activate'} "${c.name}"?`,
      message: c.isActive ? 'It can no longer be chosen for new costs or forecasts. Existing costs keep it.' : 'It can be chosen again.',
      confirmLabel: c.isActive ? 'Deactivate' : 'Activate',
    });
    if (!ok) return;
    try { await firstValueFrom(this.api.updateCategory(c.id, c.name, !c.isActive)); this.notify.success('Category updated.'); }
    catch (e) { this.notify.error(toApiError(e)); }
    await this.load();
  }
}
