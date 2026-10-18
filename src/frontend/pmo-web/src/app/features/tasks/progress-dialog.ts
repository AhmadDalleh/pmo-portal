import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { TasksApi } from '../../core/api/tasks.api';
import { toApiError } from '../../core/http/api-error';
import { TaskItem } from '../../core/models/delivery.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

export interface ProgressDialogData { task: TaskItem; canCancel: boolean }

/** Story C2: the only thing a Contributor may change on their own task. Project progress then follows from SQL on the next read. */
@Component({
  selector: 'app-progress-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>Update progress</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <p><strong>{{ data.task.title }}</strong><br /><span class="muted">{{ data.task.projectCode }} · due {{ data.task.dueDate }}</span></p>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Status</mat-label>
          <mat-select formControlName="status">
            <mat-option value="todo">To do</mat-option><mat-option value="in_progress">In progress</mat-option>
            <mat-option value="blocked">Blocked</mat-option><mat-option value="done">Done</mat-option>
            @if (data.canCancel) { <mat-option value="cancelled">Cancelled</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Progress (%)</mat-label>
          <input matInput type="number" min="0" max="100" formControlName="progressPct" />
          <mat-hint>Choosing "Done" sets 100% automatically.</mat-hint>
          <mat-error>{{ err('progressPct') }}</mat-error>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class ProgressDialog {
  protected readonly data = inject<ProgressDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<ProgressDialog, boolean>>(MatDialogRef);
  private readonly api = inject(TasksApi);
  private readonly notify = inject(Notify);

  protected readonly saving = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    status: [this.data.task.status],
    progressPct: [this.data.task.progressPct, [Validators.required, Validators.min(0), Validators.max(100)]],
  });

  protected err(name: string): string { return fieldError(this.form.get(name)); }

  protected async save(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.updateProgress(this.data.task.id, {
        status: this.form.controls.status.value, progressPct: Number(this.form.controls.progressPct.value), version: this.data.task.version,
      }));
      this.notify.success('Progress saved.');
      this.ref.close(true);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form, err)) this.notify.error(err);   // 403 / 409 explain themselves
      if (err.status === 409) this.ref.close(false);
    } finally {
      this.saving.set(false);
    }
  }
}
