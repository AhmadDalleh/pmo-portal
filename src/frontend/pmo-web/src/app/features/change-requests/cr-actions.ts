import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { GovernanceApi } from '../../core/api/governance.api';
import { toApiError } from '../../core/http/api-error';
import { ChangeRequest } from '../../core/models/governance.models';
import { Confirm } from '../../shared/confirm-dialog/confirm.service';
import { Notify } from '../../shared/notify';

type Decision = 'approve' | 'reject' | 'return';

const WORDS: Record<Decision, { verb: string; past: string; effect: string }> = {
  approve: { verb: 'Approve', past: 'approved', effect: 'The requester and project owner are notified. The budget does not change by itself.' },
  reject: { verb: 'Reject', past: 'rejected', effect: 'The request is closed and cannot be reopened.' },
  return: { verb: 'Return', past: 'returned', effect: 'The requester can edit it and submit it again.' },
};

/** The confirm-then-call steps shared by the approvals queue and the change request page. */
@Injectable({ providedIn: 'root' })
export class CrActions {
  private readonly api = inject(GovernanceApi);
  private readonly confirm = inject(Confirm);
  private readonly notify = inject(Notify);

  /** Returns true when the decision was saved. 403 / 409 messages from the server are shown as they are. */
  async decide(cr: ChangeRequest, decision: Decision): Promise<boolean> {
    const w = WORDS[decision];
    const reason = await this.confirm.ask({
      title: `${w.verb} ${cr.code}?`, message: `"${cr.title}". ${w.effect}`, confirmLabel: w.verb, reasonLabel: 'Reason (required)',
    });
    if (typeof reason !== 'string') return false;
    try {
      await firstValueFrom(this.api.decide(cr.id, { decision, reason, version: cr.version }));
      this.notify.success(`${cr.code} ${w.past}.`);
      return true;
    } catch (e) {
      this.notify.error(toApiError(e));
      return true;   // the record changed or was refused: reload either way so the screen shows the truth
    }
  }

  async submit(cr: ChangeRequest): Promise<void> {
    if (!(await this.confirm.ask({ title: `Submit ${cr.code}?`, message: 'It goes to the project manager for a decision. You cannot edit it while it is submitted.', confirmLabel: 'Submit' }))) return;
    try { await firstValueFrom(this.api.submit(cr.id, cr.version)); this.notify.success('Submitted.'); } catch (e) { this.notify.error(toApiError(e)); }
  }

  async withdraw(cr: ChangeRequest): Promise<void> {
    const reason = await this.confirm.ask({ title: `Withdraw ${cr.code}?`, message: 'A withdrawn request is closed for good.', confirmLabel: 'Withdraw', reasonLabel: 'Reason (required)' });
    if (typeof reason !== 'string') return;
    try { await firstValueFrom(this.api.withdraw(cr.id, cr.version, reason)); this.notify.success('Withdrawn.'); } catch (e) { this.notify.error(toApiError(e)); }
  }
}
