import { NgTemplateOutlet } from '@angular/common';
import { Component, TemplateRef, computed, input, output } from '@angular/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { AsyncState } from '../async-state/async-state';
import { StatusBadge } from '../status-badge/status-badge';

export interface Column<T> {
  key: string;
  header: string;
  /** Server-side sort: the key is sent to the API as `sort`. */
  sortable?: boolean;
  align?: 'end';
  value?: (row: T) => string | number | null | undefined;
  /** Shows the returned status value as an icon + text badge instead of plain text. */
  badge?: (row: T) => string | null | undefined;
}

/**
 * The one list component: sortable headers, paging, row actions and the loading / empty / error states.
 * Sorting and paging are done by the server; this component only reports what the user chose.
 */
@Component({
  selector: 'app-data-table',
  imports: [MatTableModule, MatSortModule, MatPaginatorModule, AsyncState, StatusBadge, NgTemplateOutlet],
  template: `
    <app-async-state [status]="status()" [empty]="rows().length === 0" [error]="error()" [emptyText]="emptyText()" (retry)="retry.emit()">
      <div class="scroll">
        <table mat-table [dataSource]="rows()" matSort [matSortActive]="sortActive()" [matSortDirection]="sortDirection()"
               (matSortChange)="sortChange.emit($event)" [attr.aria-label]="label()">
          @for (col of columns(); track col.key) {
            <ng-container [matColumnDef]="col.key">
              <th mat-header-cell *matHeaderCellDef [mat-sort-header]="col.key" [disabled]="!col.sortable" [class.end]="col.align === 'end'">{{ col.header }}</th>
              <td mat-cell *matCellDef="let row" [class.end]="col.align === 'end'">
                @if (col.badge && col.badge(row)) { <app-status-badge [value]="col.badge(row)!" /> }
                @else { {{ col.value ? (col.value(row) ?? '-') : '-' }} }
              </td>
            </ng-container>
          }
          @if (actions()) {
            <ng-container matColumnDef="__actions">
              <th mat-header-cell *matHeaderCellDef class="end"><span class="cdk-visually-hidden">Actions</span></th>
              <td mat-cell *matCellDef="let row" class="end"><ng-container [ngTemplateOutlet]="actions()!" [ngTemplateOutletContext]="{ $implicit: row }" /></td>
            </ng-container>
          }
          <tr mat-header-row *matHeaderRowDef="displayed()"></tr>
          <tr mat-row *matRowDef="let row; columns: displayed()" [class.clickable]="clickable()" (click)="rowClick.emit(row)"></tr>
        </table>
      </div>
      @if (total() > 0) {
        <mat-paginator [length]="total()" [pageSize]="pageSize()" [pageIndex]="page() - 1" [pageSizeOptions]="[10, 20, 50]" (page)="onPage($event)" aria-label="Pages" />
      }
    </app-async-state>
  `,
  styles: `
    .scroll { overflow-x: auto; }
    table { width: 100%; }
    .end { text-align: right; }
    tr.clickable { cursor: pointer; }
    tr.clickable:hover { background: var(--mat-sys-surface-container); }
  `,
})
export class DataTable<T> {
  readonly columns = input.required<Column<T>[]>();
  readonly rows = input.required<T[]>();
  readonly label = input('Table');
  readonly total = input(0);
  readonly page = input(1);
  readonly pageSize = input(20);
  readonly status = input<LoadStatus>('idle');
  readonly error = input<ApiError | null>(null);
  readonly emptyText = input('No data for these filters.');
  readonly sortActive = input('');
  readonly sortDirection = input<'asc' | 'desc'>('asc');
  readonly clickable = input(false);
  readonly actions = input<TemplateRef<{ $implicit: T }> | null>(null);

  readonly sortChange = output<Sort>();
  readonly pageChange = output<{ page: number; pageSize: number }>();
  readonly retry = output<void>();
  readonly rowClick = output<T>();

  protected readonly displayed = computed(() => [...this.columns().map((c) => c.key), ...(this.actions() ? ['__actions'] : [])]);

  protected onPage(e: PageEvent): void {
    this.pageChange.emit({ page: e.pageIndex + 1, pageSize: e.pageSize });
  }
}
