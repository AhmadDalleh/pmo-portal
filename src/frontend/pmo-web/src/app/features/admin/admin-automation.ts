import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { toApiError } from '../../core/http/api-error';
import { AutomationRun, OutboxMessage } from '../../core/models/admin.models';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { Confirm } from '../../shared/confirm-dialog/confirm.service';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { Notify } from '../../shared/notify';

const when = (iso: string | null) => (iso ? iso.slice(0, 19).replace('T', ' ') : null);

/** Story H3: what the background worker did (runs) and the messages it works through (outbox), with Retry for failed ones. */
@Component({
  selector: 'app-admin-automation',
  imports: [DataTable, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule],
  template: `
    <h2>Outbox messages</h2>
    <div class="row filters">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select [ngModel]="outboxStatus()" (ngModelChange)="setStatus($event)">
          <mat-option [value]="undefined">All</mat-option><mat-option value="pending">Pending</mat-option><mat-option value="processed">Processed</mat-option><mat-option value="failed">Failed</mat-option>
        </mat-select>
      </mat-form-field>
      <button mat-button type="button" (click)="reloadAll()"><mat-icon aria-hidden="true">refresh</mat-icon> Refresh</button>
    </div>
    <app-data-table label="Outbox messages" [columns]="outboxColumns" [rows]="outbox()" [total]="outboxTotal()" [page]="outboxPage()" [pageSize]="outboxSize()"
                    [status]="outboxState()" [error]="outboxError()" emptyText="No messages with this status." [actions]="outboxActions"
                    (pageChange)="onOutboxPage($event)" (retry)="loadOutbox()" />
    <ng-template #outboxActions let-m>
      @if (m.status === 'failed') {
        <button mat-stroked-button type="button" [attr.aria-label]="'Retry message ' + m.id" (click)="retry(m)"><mat-icon aria-hidden="true">replay</mat-icon> Retry</button>
      }
    </ng-template>

    <h2>Automation runs</h2>
    <app-data-table label="Automation runs" [columns]="runColumns" [rows]="runs()" [total]="runTotal()" [page]="runPage()" [pageSize]="runSize()"
                    [status]="runState()" [error]="runError()" emptyText="No runs yet." (pageChange)="onRunPage($event)" (retry)="loadRuns()" />
  `,
  styles: `.filters { margin-bottom: 8px; } .filters mat-form-field { min-width: 180px; }`,
})
export class AdminAutomation {
  private readonly api = inject(AdminApi);
  private readonly confirm = inject(Confirm);
  private readonly notify = inject(Notify);

  protected readonly outbox = signal<OutboxMessage[]>([]);
  protected readonly outboxTotal = signal(0);
  protected readonly outboxPage = signal(1);
  protected readonly outboxSize = signal(10);
  protected readonly outboxStatus = signal<string | undefined>(undefined);
  protected readonly outboxState = signal<LoadStatus>('idle');
  protected readonly outboxError = signal<ApiError | null>(null);

  protected readonly runs = signal<AutomationRun[]>([]);
  protected readonly runTotal = signal(0);
  protected readonly runPage = signal(1);
  protected readonly runSize = signal(10);
  protected readonly runState = signal<LoadStatus>('idle');
  protected readonly runError = signal<ApiError | null>(null);

  protected readonly outboxColumns: Column<OutboxMessage>[] = [
    { key: 'id', header: 'Id', value: (m) => m.id },
    { key: 'type', header: 'Type', value: (m) => m.type },
    { key: 'key', header: 'Dedup key', value: (m) => m.dedupKey },
    { key: 'status', header: 'Status', badge: (m) => m.status },
    { key: 'attempts', header: 'Attempts', align: 'end', value: (m) => m.attempts },
    { key: 'next', header: 'Next attempt (UTC)', value: (m) => (m.status === 'pending' ? when(m.availableAt) : null) },
    { key: 'error', header: 'Last error', value: (m) => m.lastError },
  ];

  protected readonly runColumns: Column<AutomationRun>[] = [
    { key: 'job', header: 'Job', value: (r) => r.jobName },
    { key: 'started', header: 'Started (UTC)', value: (r) => when(r.startedAt) },
    { key: 'status', header: 'Status', badge: (r) => r.status },
    { key: 'done', header: 'Processed', align: 'end', value: (r) => r.messagesProcessed },
    { key: 'failed', header: 'Failed', align: 'end', value: (r) => r.messagesFailed },
    { key: 'error', header: 'Error', value: (r) => r.error },
    { key: 'corr', header: 'Correlation id', value: (r) => r.correlationId },
  ];

  constructor() { void this.reloadAll(); }

  protected reloadAll(): Promise<unknown> { return Promise.all([this.loadOutbox(), this.loadRuns()]); }
  protected setStatus(v: string | undefined): void { this.outboxStatus.set(v); this.outboxPage.set(1); void this.loadOutbox(); }
  protected onOutboxPage(p: { page: number; pageSize: number }): void { this.outboxPage.set(p.page); this.outboxSize.set(p.pageSize); void this.loadOutbox(); }
  protected onRunPage(p: { page: number; pageSize: number }): void { this.runPage.set(p.page); this.runSize.set(p.pageSize); void this.loadRuns(); }

  protected async loadOutbox(): Promise<void> {
    this.outboxState.set('loading');
    try {
      const page = await firstValueFrom(this.api.outbox(this.outboxStatus(), this.outboxPage(), this.outboxSize()));
      this.outbox.set(page.items); this.outboxTotal.set(page.totalCount); this.outboxState.set('idle');
    } catch (e) { this.outboxError.set(toApiError(e)); this.outboxState.set('error'); }
  }

  protected async loadRuns(): Promise<void> {
    this.runState.set('loading');
    try {
      const page = await firstValueFrom(this.api.runs(this.runPage(), this.runSize()));
      this.runs.set(page.items); this.runTotal.set(page.totalCount); this.runState.set('idle');
    } catch (e) { this.runError.set(toApiError(e)); this.runState.set('error'); }
  }

  protected async retry(m: OutboxMessage): Promise<void> {
    const ok = await this.confirm.ask({
      title: `Retry message ${m.id}?`,
      message: `${m.type} (${m.dedupKey}) failed after ${m.attempts} attempts: ${m.lastError ?? 'no error recorded'}. It goes back to pending with 0 attempts and the worker picks it up within seconds.`,
      confirmLabel: 'Retry',
    });
    if (!ok) return;
    try { await firstValueFrom(this.api.retry(m.id)); this.notify.success('Message queued again.'); } catch (e) { this.notify.error(toApiError(e)); }
    await this.reloadAll();   // the worker may already have run: show the real state of both tables
  }
}
