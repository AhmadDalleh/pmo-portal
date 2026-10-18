import { Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { GovernanceApi } from '../../core/api/governance.api';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { ChangeRequest, ChangeRequestHistoryItem } from '../../core/models/governance.models';
import { AsyncState } from '../../shared/async-state/async-state';
import { AedPipe } from '../../shared/format.pipes';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { CrActions } from './cr-actions';
import { CrDialog } from './cr-dialog';

/** Stories E1, E3, E4: one change request, its action buttons for this user, and the full timeline. */
@Component({
  selector: 'app-change-request-page',
  imports: [AsyncState, StatusBadge, MatButtonModule, MatIconModule, RouterLink, AedPipe],
  template: `
    <div class="page">
      <app-async-state [status]="status()" [error]="error()" (retry)="load()">
        @if (cr(); as c) {
          <div class="row">
            <h1 class="grow"><span class="muted">{{ c.code }}</span> {{ c.title }}</h1>
            <app-status-badge [value]="c.status" />
          </div>
          <p class="muted"><a [routerLink]="['/projects', c.projectId, 'change-requests']">{{ c.projectCode }}</a> · {{ c.type }} · raised by {{ c.requestedByName }}</p>

          <div class="row actions">
            @if (isRequester() && (c.status === 'draft' || c.status === 'returned')) {
              <button mat-stroked-button type="button" (click)="edit()"><mat-icon aria-hidden="true">edit</mat-icon> Edit</button>
              <button mat-flat-button type="button" (click)="submit()"><mat-icon aria-hidden="true">send</mat-icon> Submit</button>
              <button mat-button type="button" (click)="withdraw()">Withdraw</button>
            }
            @if (canDecide()) {
              <button mat-flat-button type="button" (click)="decide('approve')"><mat-icon aria-hidden="true">thumb_up</mat-icon> Approve</button>
              <button mat-stroked-button type="button" (click)="decide('return')"><mat-icon aria-hidden="true">undo</mat-icon> Return</button>
              <button mat-stroked-button type="button" (click)="decide('reject')"><mat-icon aria-hidden="true">thumb_down</mat-icon> Reject</button>
            }
            @if (c.status === 'submitted' && isRequester()) { <span class="muted">Waiting for a decision. You cannot decide your own request.</span> }
          </div>

          <div class="panel">
            <p>{{ c.description }}</p>
            <p class="muted">
              Cost impact: {{ c.costImpact === null ? 'not stated' : (c.costImpact | aed) }} (information only) ·
              Schedule impact: {{ c.scheduleImpactDays === null ? 'not stated' : c.scheduleImpactDays + ' days' }}
            </p>
            @if (c.decidedByName) { <p><strong>Decision by {{ c.decidedByName }}:</strong> {{ c.decisionReason }}</p> }
          </div>

          <h2>Timeline</h2>
          <ol class="timeline" aria-label="Status history">
            @for (h of history(); track h.id) {
              <li>
                <app-status-badge [value]="h.toStatus" />
                <span>{{ h.fromStatus ? 'from ' + h.fromStatus : 'created' }} by <strong>{{ h.actorName }}</strong> on {{ h.occurredAt.slice(0, 16).replace('T', ' ') }}</span>
                @if (h.reason) { <div class="reason">"{{ h.reason }}"</div> }
              </li>
            }
          </ol>
        }
      </app-async-state>
    </div>
  `,
  styles: `
    .actions { margin: 8px 0 12px; }
    .timeline { list-style: none; padding: 0; margin: 0; border-left: 2px solid var(--mat-sys-outline-variant); }
    .timeline li { padding: 6px 0 10px 14px; display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .reason { flex-basis: 100%; margin-left: 4px; color: var(--mat-sys-on-surface-variant); font-style: italic; }
  `,
})
export class ChangeRequestPage {
  readonly id = input.required<string>();

  private readonly api = inject(GovernanceApi);
  private readonly auth = inject(AuthService);
  private readonly actions = inject(CrActions);
  private readonly dialog = inject(MatDialog);

  protected readonly cr = signal<ChangeRequest | null>(null);
  protected readonly history = signal<ChangeRequestHistoryItem[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  constructor() { effect(() => { this.id(); void this.load(); }); }

  protected isRequester(): boolean { return this.cr()?.requestedById === this.auth.user()?.id; }

  /** Cosmetic: the server decides. A manager of the project (or an administrator) who did not raise it. */
  protected canDecide(): boolean {
    const c = this.cr();
    return !!c && c.status === 'submitted' && !this.isRequester() && this.auth.canManage(c.projectId);
  }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const id = Number(this.id());
      const [cr, history] = await Promise.all([firstValueFrom(this.api.get(id)), firstValueFrom(this.api.history(id))]);
      this.cr.set(cr);
      this.history.set(history);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async edit(): Promise<void> {
    const c = this.cr()!;
    await firstValueFrom(this.dialog.open(CrDialog, { data: { projectId: c.projectId, request: c }, width: '680px', maxWidth: '96vw' }).afterClosed());
    await this.load();
  }

  protected async submit(): Promise<void> { await this.actions.submit(this.cr()!); await this.load(); }
  protected async withdraw(): Promise<void> { await this.actions.withdraw(this.cr()!); await this.load(); }
  protected async decide(d: 'approve' | 'reject' | 'return'): Promise<void> { if (await this.actions.decide(this.cr()!, d)) await this.load(); }
}
