import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { TasksApi } from '../../core/api/tasks.api';
import { toApiError } from '../../core/http/api-error';
import { Risk } from '../../core/models/delivery.models';
import { Member } from '../../core/models/project.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

export interface RiskDialogData { projectId: number; risk: Risk | null; members: Member[] }

/** Same rule as the server: a score (probability x impact) of 15 or more needs a mitigation plan. */
const mitigationForHighScore = (g: AbstractControl): ValidationErrors | null =>
  Number(g.get('probability')?.value) * Number(g.get('impact')?.value) >= 15 && !String(g.get('mitigation')?.value ?? '').trim() ? { mitigationRequired: true } : null;

/** Story D1: record or edit a risk or issue. The score is shown live but calculated by the database. */
@Component({
  selector: 'app-risk-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ data.risk ? 'Edit risk or issue' : 'New risk or issue' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <mat-form-field appearance="outline" class="full"><mat-label>Title</mat-label><input matInput formControlName="title" required /><mat-error>{{ err('title') }}</mat-error></mat-form-field>
        <div class="form-grid">
          <mat-form-field appearance="outline"><mat-label>Type</mat-label><mat-select formControlName="type"><mat-option value="risk">Risk</mat-option><mat-option value="issue">Issue</mat-option></mat-select></mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Owner</mat-label>
            <mat-select formControlName="ownerId">@for (m of data.members; track m.userId) { <mat-option [value]="m.userId">{{ m.fullName }}</mat-option> }</mat-select>
            <mat-error>{{ err('ownerId') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Probability (1-5)</mat-label><input matInput type="number" min="1" max="5" formControlName="probability" /><mat-error>{{ err('probability') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Impact (1-5)</mat-label><input matInput type="number" min="1" max="5" formControlName="impact" /><mat-error>{{ err('impact') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select formControlName="status"><mat-option value="open">Open</mat-option><mat-option value="mitigating">Mitigating</mat-option><mat-option value="closed">Closed</mat-option></mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Due date</mat-label><input matInput type="date" formControlName="dueDate" /></mat-form-field>
        </div>
        <p class="muted">Score {{ score() }}{{ score() >= 15 ? ' - high exposure: a mitigation plan is required.' : '' }}</p>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Mitigation</mat-label><textarea matInput rows="2" formControlName="mitigation"></textarea>
          <mat-error>{{ err('mitigation') || (form.hasError('mitigationRequired') ? 'Required for a score of 15 or more.' : '') }}</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full"><mat-label>Description</mat-label><textarea matInput rows="2" formControlName="description"></textarea></mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end"><button mat-button type="button" mat-dialog-close>Cancel</button><button mat-flat-button type="submit" [disabled]="saving()">Save</button></mat-dialog-actions>
    </form>
  `,
})
export class RiskDialog {
  protected readonly data = inject<RiskDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<RiskDialog, boolean>>(MatDialogRef);
  private readonly api = inject(TasksApi);
  private readonly notify = inject(Notify);

  protected readonly saving = signal(false);
  private readonly r = this.data.risk;
  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      type: [this.r?.type ?? 'risk'],
      title: [this.r?.title ?? '', [Validators.required, Validators.maxLength(200)]],
      description: [this.r?.description ?? ''],
      ownerId: [this.r?.ownerId ?? this.data.members[0]?.userId ?? 0, Validators.required],
      probability: [this.r?.probability ?? 3, [Validators.required, Validators.min(1), Validators.max(5)]],
      impact: [this.r?.impact ?? 3, [Validators.required, Validators.min(1), Validators.max(5)]],
      mitigation: [this.r?.mitigation ?? ''],
      dueDate: [this.r?.dueDate ?? ''],
      status: [this.r?.status ?? 'open'],
    },
    { validators: mitigationForHighScore },
  );

  protected score(): number { return Number(this.form.controls.probability.value) * Number(this.form.controls.impact.value); }
  protected err(name: string): string { return fieldError(this.form.get(name)); }

  protected async save(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const body = {
      type: v.type, title: v.title, description: v.description || null, ownerId: v.ownerId, probability: Number(v.probability), impact: Number(v.impact),
      mitigation: v.mitigation || null, dueDate: v.dueDate || null, status: v.status,
    };
    this.saving.set(true);
    try {
      await firstValueFrom(this.r ? this.api.updateRisk(this.r.id, { ...body, version: this.r.version }) : this.api.createRisk(this.data.projectId, body));
      this.notify.success('Saved.');
      this.ref.close(true);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form, err)) this.notify.error(err);
      if (err.status === 409) this.ref.close(false);
    } finally { this.saving.set(false); }
  }
}
