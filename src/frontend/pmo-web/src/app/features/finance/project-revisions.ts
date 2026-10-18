import { Component, effect, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/api/finance.api';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { BudgetRevision } from '../../core/models/finance.models';
import { Confirm } from '../../shared/confirm-dialog/confirm.service';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { AedPipe } from '../../shared/format.pipes';
import { Notify } from '../../shared/notify';
import { RevisionDialog } from './revision-dialog';

/** Stories F3 and F4: the project's budget revisions; administrators decide the pending ones (never their own). */
@Component({
  selector: 'app-project-revisions',
  imports: [DataTable, MatButtonModule, MatIconModule],
  template: `
    <div class="row">
      <span class="grow muted">Only approved revisions with an effective date on or before the as-of date count toward the budget.</span>
      @if (canManage()) { <button mat-flat-button type="button" (click)="request()"><mat-icon aria-hidden="true">add</mat-icon> Request revision</button> }
    </div>
    <app-data-table label="Budget revisions" [columns]="columns" [rows]="rows()" [status]="status()" [error]="error()" emptyText="No revisions yet."
                    [actions]="auth.isAdmin() ? rowActions : null" (retry)="load()" />
    <ng-template #rowActions let-r>
      @if (r.status === 'pending') {
        <button mat-icon-button type="button" [attr.aria-label]="'Approve revision ' + r.id" (click)="decide(r, 'approved')"><mat-icon aria-hidden="true">thumb_up</mat-icon></button>
        <button mat-icon-button type="button" [attr.aria-label]="'Reject revision ' + r.id" (click)="decide(r, 'rejected')"><mat-icon aria-hidden="true">thumb_down</mat-icon></button>
      }
    </ng-template>
  `,
})
export class ProjectRevisions {
  readonly projectId = input.required<number>();
  readonly canManage = input(false);
  readonly changed = output<void>();

  protected readonly auth = inject(AuthService);
  private readonly api = inject(FinanceApi);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(Confirm);
  private readonly notify = inject(Notify);
  private readonly aed = new AedPipe();

  protected readonly rows = signal<BudgetRevision[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<BudgetRevision>[] = [
    { key: 'amount', header: 'Amount', align: 'end', value: (r) => this.aed.transform(r.amount) },
    { key: 'effective', header: 'Effective', value: (r) => r.effectiveDate },
    { key: 'status', header: 'Status', badge: (r) => r.status },
    { key: 'reason', header: 'Reason', value: (r) => r.reason },
    { key: 'requested', header: 'Requested by', value: (r) => r.requestedByName },
    { key: 'decided', header: 'Decided by', value: (r) => r.decidedByName ? `${r.decidedByName}: ${r.decisionReason}` : null },
    { key: 'cr', header: 'Change request', value: (r) => r.changeRequestId },
  ];

  constructor() { effect(() => { if (this.projectId()) void this.load(); }); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try { this.rows.set(await firstValueFrom(this.api.revisions(this.projectId()))); this.status.set('idle'); }
    catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async request(): Promise<void> {
    await firstValueFrom(this.dialog.open(RevisionDialog, { data: { projectId: this.projectId() }, width: '600px', maxWidth: '96vw' }).afterClosed());
    await this.refresh();
  }

  protected async decide(r: BudgetRevision, decision: 'approved' | 'rejected'): Promise<void> {
    const reason = await this.confirm.ask({
      title: `${decision === 'approved' ? 'Approve' : 'Reject'} this revision?`,
      message: `${this.aed.transform(r.amount)} effective ${r.effectiveDate}, requested by ${r.requestedByName}. You cannot decide your own request.`,
      confirmLabel: decision === 'approved' ? 'Approve' : 'Reject', reasonLabel: 'Reason (required)',
    });
    if (typeof reason !== 'string') return;
    try { await firstValueFrom(this.api.decideRevision(r.id, { decision, reason, version: r.version })); this.notify.success(`Revision ${decision}.`); }
    catch (e) { this.notify.error(toApiError(e)); }
    await this.refresh();
  }

  private async refresh(): Promise<void> { await this.load(); this.changed.emit(); }
}
