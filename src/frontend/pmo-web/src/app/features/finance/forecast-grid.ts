import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/api/finance.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus, LookupItem } from '../../core/models/common.models';
import { AsyncState } from '../../shared/async-state/async-state';
import { Notify } from '../../shared/notify';

/** Story F8: months (columns) by categories (rows). The whole grid is saved in one request; the server stores it all or nothing. */
@Component({
  selector: 'app-forecast-grid',
  imports: [FormsModule, MatButtonModule, MatIconModule, AsyncState],
  template: `
    <div class="row">
      <span class="grow muted">Forecast amounts for months after the reporting month. They feed "forecast to complete" and EAC.</span>
      @if (canManage()) {
        <button mat-stroked-button type="button" (click)="addMonth()"><mat-icon aria-hidden="true">add</mat-icon> Add month</button>
        <button mat-flat-button type="button" [disabled]="saving()" (click)="save()">Save forecast</button>
      }
    </div>
    <app-async-state [status]="status()" [error]="error()" [empty]="months().length === 0" emptyText="No forecast entered yet. Add a month to start." (retry)="load()">
      <div class="scroll">
        <table class="grid" aria-label="Forecast by category and month">
          <thead><tr><th scope="col">Category</th>@for (m of months(); track m) { <th scope="col" class="end">{{ m.slice(0, 7) }}</th> }</tr></thead>
          <tbody>
            @for (c of categories(); track c.id) {
              <tr>
                <th scope="row">{{ c.name }}</th>
                @for (m of months(); track m) {
                  <td class="end"><input type="number" min="0" step="0.01" [disabled]="!canManage()" [attr.aria-label]="c.name + ' ' + m.slice(0, 7)" [ngModel]="cell(c.id, m)" (ngModelChange)="setCell(c.id, m, $event)" /></td>
                }
              </tr>
            }
            <tr class="total"><th scope="row">Total</th>@for (m of months(); track m) { <td class="end">{{ monthTotal(m) }}</td> }</tr>
          </tbody>
        </table>
      </div>
    </app-async-state>
  `,
  styles: `
    .scroll { overflow-x: auto; margin-top: 8px; }
    .grid { border-collapse: collapse; min-width: 100%; }
    th, td { padding: 4px 8px; border-bottom: 1px solid var(--mat-sys-outline-variant); text-align: left; white-space: nowrap; }
    th.end, td.end { text-align: right; }
    input { width: 110px; text-align: right; padding: 4px; border: 1px solid var(--mat-sys-outline); border-radius: 4px; background: transparent; color: inherit; }
    .total th, .total td { font-weight: 600; }
  `,
})
export class ForecastGrid {
  readonly projectId = input.required<number>();
  readonly canManage = input(false);
  readonly changed = output<void>();

  private readonly api = inject(FinanceApi);
  private readonly notify = inject(Notify);

  protected readonly categories = signal<LookupItem[]>([]);
  protected readonly months = signal<string[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  private readonly values = signal<Record<string, number>>({});
  private loadedKeys = new Set<string>();   // cells that exist on the server, so a cleared cell is saved as 0 instead of being forgotten

  constructor() { effect(() => { if (this.projectId()) void this.load(); }); }

  private key(categoryId: number, month: string): string { return `${categoryId}|${month}`; }
  protected cell(categoryId: number, month: string): number { return this.values()[this.key(categoryId, month)] ?? 0; }
  protected setCell(categoryId: number, month: string, value: number | string): void {
    this.values.update((v) => ({ ...v, [this.key(categoryId, month)]: Number(value) || 0 }));
  }
  /** A display total of what is typed in the column; the real forecast totals come from the server. */
  protected monthTotal(month: string): number { return this.categories().reduce((sum, c) => sum + this.cell(c.id, month), 0); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const [categories, cells] = await Promise.all([firstValueFrom(this.api.categories()), firstValueFrom(this.api.forecasts(this.projectId()))]);
      this.categories.set(categories);
      this.months.set([...new Set(cells.map((c) => c.periodMonth))].sort());
      this.values.set(Object.fromEntries(cells.map((c) => [this.key(c.categoryId, c.periodMonth), c.amount])));
      this.loadedKeys = new Set(cells.map((c) => this.key(c.categoryId, c.periodMonth)));
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  /** The first month the server accepts is the month after the reporting date (2026-10-01 for the 2026-09-30 snapshot). */
  protected addMonth(): void {
    const last = this.months().at(-1) ?? '2026-09-01';
    const d = new Date(`${last}T00:00:00Z`);
    d.setUTCMonth(d.getUTCMonth() + 1);
    this.months.update((m) => [...m, d.toISOString().slice(0, 10)]);
  }

  protected async save(): Promise<void> {
    const items = this.months()
      .flatMap((month) => this.categories().map((c) => ({ categoryId: c.id, periodMonth: month, amount: this.cell(c.id, month) })))
      .filter((i) => i.amount !== 0 || this.loadedKeys.has(this.key(i.categoryId, i.periodMonth)));   // empty cells are not stored
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.saveForecasts(this.projectId(), items));
      this.notify.success('Forecast saved.');
      await this.load();
      this.changed.emit();
    } catch (e) { this.notify.error(toApiError(e)); }
    finally { this.saving.set(false); }
  }
}
