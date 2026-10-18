import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/api/finance.api';
import { toApiError } from '../../core/http/api-error';
import { LookupItem } from '../../core/models/common.models';
import { Cost } from '../../core/models/finance.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

export interface CostDialogData { projectId: number; cost: Cost | null; categories: LookupItem[] }

/**
 * Story F5: record or edit a draft cost. A draft never counts toward spend until it is posted.
 * The Idempotency-Key is created when the dialog opens, so pressing Save twice, or retrying after a timeout, cannot create two costs.
 */
@Component({
  selector: 'app-cost-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ data.cost ? 'Edit draft cost' : 'New draft cost' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Category</mat-label>
            <mat-select formControlName="categoryId" required>@for (c of data.categories; track c.id) { <mat-option [value]="c.id">{{ c.name }}</mat-option> }</mat-select>
            <mat-error>{{ err('categoryId') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Date incurred</mat-label><input matInput type="date" formControlName="txnDate" required /><mat-error>{{ err('txnDate') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Amount (AED)</mat-label><input matInput type="number" min="0.01" step="0.01" formControlName="amount" required /><mat-error>{{ err('amount') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Reference no.</mat-label><input matInput formControlName="referenceNo" /><mat-error>{{ err('referenceNo') }}</mat-error></mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full"><mat-label>Description</mat-label><input matInput formControlName="description" required /><mat-error>{{ err('description') }}</mat-error></mat-form-field>
        <mat-form-field appearance="outline" class="full"><mat-label>Vendor</mat-label><input matInput formControlName="vendorName" /><mat-error>{{ err('vendorName') }}</mat-error></mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end"><button mat-button type="button" mat-dialog-close>Cancel</button><button mat-flat-button type="submit" [disabled]="saving()">Save draft</button></mat-dialog-actions>
    </form>
  `,
})
export class CostDialog {
  protected readonly data = inject<CostDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<CostDialog, boolean>>(MatDialogRef);
  private readonly api = inject(FinanceApi);
  private readonly notify = inject(Notify);

  private readonly idempotencyKey = crypto.randomUUID();
  protected readonly saving = signal(false);
  private readonly c = this.data.cost;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    categoryId: [this.c?.categoryId ?? 0, [Validators.required, Validators.min(1)]],
    txnDate: [this.c?.txnDate ?? '', Validators.required],
    amount: [this.c?.amount ?? 0, [Validators.required, Validators.min(0.01)]],
    description: [this.c?.description ?? '', [Validators.required, Validators.maxLength(300)]],
    vendorName: [this.c?.vendorName ?? '', Validators.maxLength(150)],
    referenceNo: [this.c?.referenceNo ?? '', Validators.maxLength(50)],
  });

  protected err(name: string): string { return fieldError(this.form.get(name)); }

  protected async save(): Promise<void> {
    if (this.form.invalid || this.saving()) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const body = { categoryId: v.categoryId, txnDate: v.txnDate, amount: Number(v.amount), description: v.description, vendorName: v.vendorName || null, referenceNo: v.referenceNo || null };
    this.saving.set(true);
    try {
      await firstValueFrom(this.c ? this.api.updateDraft(this.c.id, { ...body, version: this.c.version }) : this.api.createDraft(this.data.projectId, body, this.idempotencyKey));
      this.notify.success('Draft saved. It counts toward spend once it is posted.');
      this.ref.close(true);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form, err)) this.notify.error(err);
      if (err.status === 409) this.ref.close(false);
    } finally { this.saving.set(false); }
  }
}
