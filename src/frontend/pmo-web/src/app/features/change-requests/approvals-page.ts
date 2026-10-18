import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { GovernanceApi } from '../../core/api/governance.api';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { ChangeRequest } from '../../core/models/governance.models';
import { Column, DataTable } from '../../shared/data-table/data-table';
import { CrActions } from './cr-actions';

/** Story E2/E3: submitted change requests of the projects this person manages, never their own. Each decision asks for a reason first. */
@Component({
  selector: 'app-approvals-page',
  imports: [DataTable, MatButtonModule, MatIconModule],
  template: `
    <div class="page">
      <h1>Approvals</h1>
      <p class="muted">Change requests waiting for your decision. Requests you raised yourself never appear here.</p>
      <app-data-table label="Change requests to decide" [columns]="columns" [rows]="rows()" [total]="total()" [page]="page()" [pageSize]="pageSize()" [status]="status()" [error]="error()"
                      emptyText="Nothing is waiting for a decision." [actions]="rowActions" (pageChange)="onPage($event)" (retry)="load()" />
      <ng-template #rowActions let-c>
        <button mat-icon-button type="button" [attr.aria-label]="'Approve ' + c.code" (click)="decide(c, 'approve')"><mat-icon aria-hidden="true">thumb_up</mat-icon></button>
        <button mat-icon-button type="button" [attr.aria-label]="'Return ' + c.code" (click)="decide(c, 'return')"><mat-icon aria-hidden="true">undo</mat-icon></button>
        <button mat-icon-button type="button" [attr.aria-label]="'Reject ' + c.code" (click)="decide(c, 'reject')"><mat-icon aria-hidden="true">thumb_down</mat-icon></button>
        <button mat-icon-button type="button" [attr.aria-label]="'Open ' + c.code" (click)="open(c)"><mat-icon aria-hidden="true">open_in_new</mat-icon></button>
      </ng-template>
    </div>
  `,
})
export class ApprovalsPage {
  private readonly api = inject(GovernanceApi);
  private readonly actions = inject(CrActions);
  private readonly router = inject(Router);

  protected readonly rows = signal<ChangeRequest[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  protected readonly columns: Column<ChangeRequest>[] = [
    { key: 'code', header: 'Code', value: (c) => c.code },
    { key: 'project', header: 'Project', value: (c) => c.projectCode },
    { key: 'title', header: 'Title', value: (c) => c.title },
    { key: 'type', header: 'Type', value: (c) => c.type },
    { key: 'requester', header: 'Requested by', value: (c) => c.requestedByName },
    { key: 'submitted', header: 'Submitted', value: (c) => c.submittedAt?.slice(0, 10) },
  ];

  constructor() { void this.load(); }

  protected onPage(p: { page: number; pageSize: number }): void { this.page.set(p.page); this.pageSize.set(p.pageSize); void this.load(); }
  protected open(c: ChangeRequest): void { void this.router.navigate(['/change-requests', c.id]); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const result = await firstValueFrom(this.api.queue(this.page(), this.pageSize()));
      this.rows.set(result.items);
      this.total.set(result.totalCount);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async decide(c: ChangeRequest, decision: 'approve' | 'reject' | 'return'): Promise<void> {
    if (await this.actions.decide(c, decision)) await this.load();
  }
}
