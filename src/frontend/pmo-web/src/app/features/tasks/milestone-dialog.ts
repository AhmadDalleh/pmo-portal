import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { TasksApi } from '../../core/api/tasks.api';
import { toApiError } from '../../core/http/api-error';
import { Milestone } from '../../core/models/delivery.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

export interface MilestoneDialogData { projectId: number; milestone: Milestone | null }

/** Story C5: create or rename a milestone and set its due date. */
@Component({
  selector: 'app-milestone-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ data.milestone ? 'Edit milestone' : 'New milestone' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <mat-form-field appearance="outline" class="full"><mat-label>Name</mat-label><input matInput formControlName="name" required /><mat-error>{{ err('name') }}</mat-error></mat-form-field>
        <mat-form-field appearance="outline" class="full"><mat-label>Due date</mat-label><input matInput type="date" formControlName="dueDate" required /><mat-error>{{ err('dueDate') }}</mat-error></mat-form-field>
        <mat-form-field appearance="outline" class="full"><mat-label>Description</mat-label><textarea matInput rows="2" formControlName="description"></textarea></mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end"><button mat-button type="button" mat-dialog-close>Cancel</button><button mat-flat-button type="submit" [disabled]="saving()">Save</button></mat-dialog-actions>
    </form>
  `,
})
export class MilestoneDialog {
  protected readonly data = inject<MilestoneDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<MilestoneDialog, boolean>>(MatDialogRef);
  private readonly api = inject(TasksApi);
  private readonly notify = inject(Notify);

  protected readonly saving = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: [this.data.milestone?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    dueDate: [this.data.milestone?.dueDate ?? '', Validators.required],
    description: [this.data.milestone?.description ?? ''],
  });

  protected err(name: string): string { return fieldError(this.form.get(name)); }

  protected async save(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const body = { name: v.name, description: v.description || null, dueDate: v.dueDate };
    this.saving.set(true);
    try {
      const m = this.data.milestone;
      await firstValueFrom(m ? this.api.updateMilestone(m.id, { ...body, version: m.version }) : this.api.createMilestone(this.data.projectId, body));
      this.notify.success('Milestone saved.');
      this.ref.close(true);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form, err)) this.notify.error(err);
      if (err.status === 409) this.ref.close(false);
    } finally { this.saving.set(false); }
  }
}
