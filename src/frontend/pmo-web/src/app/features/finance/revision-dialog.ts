import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/api/finance.api';
import { toApiError } from '../../core/http/api-error';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

/** Story F3: ask for a dated budget change (positive = more budget, negative = reduction). */
@Component({
  selector: 'app-revision-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>Request a budget revision</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field appearance="outline"><mat-label>Amount (AED, negative = reduction)</mat-label><input matInput type="number" step="0.01" formControlName="amount" required /><mat-error>{{ err('amount') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Effective date</mat-label><input matInput type="date" formControlName="effectiveDate" required /><mat-error>{{ err('effectiveDate') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Linked approved change request (id)</mat-label><input matInput type="number" formControlName="changeRequestId" /><mat-error>{{ err('changeRequestId') }}</mat-error></mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full"><mat-label>Reason</mat-label><textarea matInput rows="3" formControlName="reason" required></textarea><mat-error>{{ err('reason') }}</mat-error></mat-form-field>
        <p class="muted">It counts toward the budget only after an administrator approves it, and only from its effective date.</p>
      </mat-dialog-content>
      <mat-dialog-actions align="end"><button mat-button type="button" mat-dialog-close>Cancel</button><button mat-flat-button type="submit" [disabled]="saving()">Request</button></mat-dialog-actions>
    </form>
  `,
})
export class RevisionDialog {
  protected readonly data = inject<{ projectId: number }>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<RevisionDialog, boolean>>(MatDialogRef);
  private readonly api = inject(FinanceApi);
  private readonly notify = inject(Notify);

  protected readonly saving = signal(false);
  protected readonly form = inject(FormBuilder).group({
    amount: [null as number | null, [Validators.required]],
    effectiveDate: ['', Validators.required],
    changeRequestId: [null as number | null],
    reason: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  protected err(name: string): string { return fieldError(this.form.get(name)); }

  protected async save(): Promise<void> {
    if (this.form.invalid || Number(this.form.controls.amount.value) === 0) { this.form.controls.amount.setErrors({ server: 'The amount cannot be zero.' }); this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.requestRevision(this.data.projectId, {
        amount: Number(v.amount), reason: v.reason!, effectiveDate: v.effectiveDate!, changeRequestId: v.changeRequestId ? Number(v.changeRequestId) : null,
      }));
      this.notify.success('Revision requested.');
      this.ref.close(true);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form as never, err)) this.notify.error(err);
    } finally { this.saving.set(false); }
  }
}
