import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { GovernanceApi } from '../../core/api/governance.api';
import { toApiError } from '../../core/http/api-error';
import { CR_TYPES, ChangeRequest } from '../../core/models/governance.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

export interface CrDialogData { projectId: number; request: ChangeRequest | null }

/** Story E1: raise a change request, or edit one that is still a draft or was returned. */
@Component({
  selector: 'app-cr-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ data.request ? 'Edit ' + data.request.code : 'New change request' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <mat-form-field appearance="outline" class="full"><mat-label>Title</mat-label><input matInput formControlName="title" required /><mat-error>{{ err('title') }}</mat-error></mat-form-field>
        <mat-form-field appearance="outline" class="full"><mat-label>Description</mat-label><textarea matInput rows="4" formControlName="description" required></textarea><mat-error>{{ err('description') }}</mat-error></mat-form-field>
        <div class="form-grid">
          <mat-form-field appearance="outline"><mat-label>Type</mat-label><mat-select formControlName="type">@for (t of types; track t) { <mat-option [value]="t">{{ t }}</mat-option> }</mat-select><mat-error>{{ err('type') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Cost impact (AED, information only)</mat-label><input matInput type="number" step="0.01" formControlName="costImpact" /><mat-error>{{ err('costImpact') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Schedule impact (days)</mat-label><input matInput type="number" formControlName="scheduleImpactDays" /><mat-error>{{ err('scheduleImpactDays') }}</mat-error></mat-form-field>
        </div>
        <p class="muted">Approving a change request never changes the budget by itself. A project manager can link an approved one to a budget revision.</p>
      </mat-dialog-content>
      <mat-dialog-actions align="end"><button mat-button type="button" mat-dialog-close>Cancel</button><button mat-flat-button type="submit" [disabled]="saving()">Save as draft</button></mat-dialog-actions>
    </form>
  `,
})
export class CrDialog {
  protected readonly data = inject<CrDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<CrDialog, ChangeRequest>>(MatDialogRef);
  private readonly api = inject(GovernanceApi);
  private readonly notify = inject(Notify);

  protected readonly types = CR_TYPES;
  protected readonly saving = signal(false);
  private readonly r = this.data.request;
  protected readonly form = inject(FormBuilder).group({
    title: [this.r?.title ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.r?.description ?? '', Validators.required],
    type: [this.r?.type ?? 'scope', Validators.required],
    costImpact: [this.r?.costImpact ?? (null as number | null)],
    scheduleImpactDays: [this.r?.scheduleImpactDays ?? (null as number | null)],
  });

  protected err(name: string): string { return fieldError(this.form.get(name)); }

  protected async save(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const body = {
      title: v.title!, description: v.description!, type: v.type!,
      costImpact: v.costImpact === null || v.costImpact === undefined || (v.costImpact as unknown) === '' ? null : Number(v.costImpact),
      scheduleImpactDays: v.scheduleImpactDays === null || v.scheduleImpactDays === undefined || (v.scheduleImpactDays as unknown) === '' ? null : Number(v.scheduleImpactDays),
    };
    this.saving.set(true);
    try {
      const saved = await firstValueFrom(this.r ? this.api.update(this.r.id, { ...body, version: this.r.version }) : this.api.create(this.data.projectId, body));
      this.notify.success(`${saved.code} saved.`);
      this.ref.close(saved);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form as never, err)) this.notify.error(err);
      if (err.status === 409) this.ref.close(undefined);
    } finally { this.saving.set(false); }
  }
}
