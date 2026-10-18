import { Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/api/finance.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus, LookupItem } from '../../core/models/common.models';
import { Cost } from '../../core/models/finance.models';
import { Confirm } from '../../shared/confirm-dialog/confirm.service';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { AedPipe } from '../../shared/format.pipes';
import { Notify } from '../../shared/notify';
import { CostDialog } from './cost-dialog';

/**
 * Stories F5-F7: draft costs, posting and reversal. Every consequential click asks first and says exactly what will happen.
 * After posting or reversing, the parent reloads the summary and charts from the server; no number is changed here.
 */
@Component({
  selector: 'app-project-costs',
  imports: [DataTable, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule],
  template: `
    <div class="row">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Show</mat-label>
        <mat-select [ngModel]="filter()" (ngModelChange)="setFilter($event)"><mat-option [value]="undefined">All costs</mat-option><mat-option value="draft">Drafts</mat-option><mat-option value="posted">Posted</mat-option></mat-select>
      </mat-form-field>
      <span class="grow"></span>
      @if (canManage()) { <button mat-flat-button type="button" (click)="edit(null)"><mat-icon aria-hidden="true">add</mat-icon> New draft cost</button> }
    </div>
    <app-data-table label="Costs" [columns]="columns" [rows]="rows()" [total]="total()" [page]="page()" [pageSize]="pageSize()" [status]="status()" [error]="error()"
                    emptyText="No costs recorded." [actions]="canManage() ? rowActions : null" (pageChange)="onPage($event)" (retry)="load()" />
    <ng-template #rowActions let-c>
      @if (c.status === 'draft') {
        <button mat-icon-button type="button" [attr.aria-label]="'Post ' + c.description" (click)="post(c)"><mat-icon aria-hidden="true">publish</mat-icon></button>
        <button mat-icon-button type="button" [attr.aria-label]="'Edit ' + c.description" (click)="edit(c)"><mat-icon aria-hidden="true">edit</mat-icon></button>
        <button mat-icon-button type="button" [attr.aria-label]="'Delete ' + c.description" (click)="remove(c)"><mat-icon aria-hidden="true">delete</mat-icon></button>
      } @else if (c.reversesTransactionId === null && !c.isReversed) {
        <button mat-icon-button type="button" [attr.aria-label]="'Reverse ' + c.description" (click)="reverse(c)"><mat-icon aria-hidden="true">undo</mat-icon></button>
      }
    </ng-template>
  `,
})
export class ProjectCosts {
  readonly projectId = input.required<number>();
  readonly canManage = input(false);
  /** Fired after any change so the parent can refetch the summary and charts. */
  readonly changed = output<void>();

  private readonly api = inject(FinanceApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(Confirm);
  private readonly notify = inject(Notify);
  private readonly aed = new AedPipe();

  protected readonly rows = signal<Cost[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly filter = signal<string | undefined>(undefined);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);
  private categories: LookupItem[] = [];

  protected readonly columns: Column<Cost>[] = [
    { key: 'date', header: 'Date', value: (c) => c.txnDate },
    { key: 'category', header: 'Category', value: (c) => c.categoryName },
    { key: 'description', header: 'Description', value: (c) => c.description + (c.isReversed ? ' (reversed)' : '') },
    { key: 'vendor', header: 'Vendor', value: (c) => c.vendorName },
    { key: 'amount', header: 'Amount', align: 'end', value: (c) => this.aed.transform(c.amount, 'cents') },
    { key: 'status', header: 'Status', badge: (c) => c.status },
  ];

  constructor() { effect(() => { if (this.projectId()) untracked(() => void this.load()); }); }

  protected setFilter(v: string | undefined): void { this.filter.set(v); this.page.set(1); void this.load(); }
  protected onPage(p: { page: number; pageSize: number }): void { this.page.set(p.page); this.pageSize.set(p.pageSize); void this.load(); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const result = await firstValueFrom(this.api.costs(this.projectId(), { status: this.filter(), page: this.page(), pageSize: this.pageSize() }));
      this.rows.set(result.items);
      this.total.set(result.totalCount);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async edit(cost: Cost | null): Promise<void> {
    if (this.categories.length === 0) this.categories = await firstValueFrom(this.api.categories());
    const ref = this.dialog.open(CostDialog, { data: { projectId: this.projectId(), cost, categories: this.categories }, width: '640px', maxWidth: '96vw' });
    await firstValueFrom(ref.afterClosed());
    await this.refresh();
  }

  protected async post(c: Cost): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Post this cost?', message: `Post ${this.aed.transform(c.amount, 'cents')} (${c.description}) dated ${c.txnDate}? Once posted it counts toward actual spend and cannot be edited or deleted; only a reversal can correct it.`,
      confirmLabel: 'Post',
    });
    if (ok) await this.run(() => firstValueFrom(this.api.post(c.id, c.version)), 'Cost posted.');
  }

  protected async reverse(c: Cost): Promise<void> {
    const reason = await this.confirm.ask({
      title: 'Reverse this cost?', message: `Creates a posted reversal of -${this.aed.transform(c.amount, 'cents').replace('AED ', 'AED ')} dated ${c.txnDate}. The original stays as it is. One reversal per cost.`,
      confirmLabel: 'Reverse', reasonLabel: 'Reason (required)',
    });
    if (typeof reason === 'string') await this.run(() => firstValueFrom(this.api.reverse(c.id, reason)), 'Reversal posted.');
  }

  protected async remove(c: Cost): Promise<void> {
    if (await this.confirm.ask({ title: 'Delete this draft?', message: `${c.description} (${this.aed.transform(c.amount, 'cents')}) will be removed. Drafts never counted toward spend.`, confirmLabel: 'Delete' })) {
      await this.run(() => firstValueFrom(this.api.deleteDraft(c.id)), 'Draft deleted.');
    }
  }

  private async run(action: () => Promise<unknown>, success: string): Promise<void> {
    try { await action(); this.notify.success(success); } catch (e) { this.notify.error(toApiError(e)); }   // 403 / 409 show the server's explanation
    await this.refresh();
  }

  private async refresh(): Promise<void> {
    await this.load();
    this.changed.emit();
  }
}
