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
import { Milestone, TASK_PRIORITIES, TASK_STATUSES, TaskItem } from '../../core/models/delivery.models';
import { Member } from '../../core/models/project.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

export interface TaskDialogData { projectId: number; task: TaskItem | null; members: Member[]; milestones: Milestone[] }

const dueAfterStart = (g: AbstractControl): ValidationErrors | null => {
  const start = g.get('startDate')?.value as string;
  const due = g.get('dueDate')?.value as string;
  return start && due && due < start ? { dateOrder: true } : null;
};

/** Story C1: a project manager creates or edits a task. Choosing "done" forces progress to 100 on the server. */
@Component({
  selector: 'app-task-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ data.task ? 'Edit task' : 'New task' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <mat-form-field appearance="outline" class="full"><mat-label>Title</mat-label><input matInput formControlName="title" required /><mat-error>{{ err('title') }}</mat-error></mat-form-field>
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Assignee</mat-label>
            <mat-select formControlName="assigneeId"><mat-option [value]="null">Unassigned</mat-option>@for (m of data.members; track m.userId) { <mat-option [value]="m.userId">{{ m.fullName }}</mat-option> }</mat-select>
            <mat-error>{{ err('assigneeId') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Milestone</mat-label>
            <mat-select formControlName="milestoneId"><mat-option [value]="null">None</mat-option>@for (m of data.milestones; track m.id) { <mat-option [value]="m.id">{{ m.name }}</mat-option> }</mat-select>
            <mat-error>{{ err('milestoneId') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Status</mat-label><mat-select formControlName="status">@for (s of statuses; track s) { <mat-option [value]="s">{{ s.replace('_', ' ') }}</mat-option> }</mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Priority</mat-label><mat-select formControlName="priority">@for (p of priorities; track p) { <mat-option [value]="p">{{ p }}</mat-option> }</mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Start date</mat-label><input matInput type="date" formControlName="startDate" /></mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Due date</mat-label><input matInput type="date" formControlName="dueDate" required />
            <mat-error>{{ err('dueDate') || (form.hasError('dateOrder') ? 'Due date must be on or after the start date.' : '') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Weight (1-10)</mat-label><input matInput type="number" min="1" max="10" formControlName="weight" /><mat-error>{{ err('weight') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Progress (%)</mat-label><input matInput type="number" min="0" max="100" formControlName="progressPct" /><mat-error>{{ err('progressPct') }}</mat-error></mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full"><mat-label>Description</mat-label><textarea matInput rows="2" formControlName="description"></textarea></mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class TaskDialog {
  protected readonly data = inject<TaskDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<TaskDialog, boolean>>(MatDialogRef);
  private readonly api = inject(TasksApi);
  private readonly notify = inject(Notify);

  protected readonly statuses = TASK_STATUSES;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly saving = signal(false);

  private readonly t = this.data.task;
  protected readonly form = inject(FormBuilder).group(
    {
      title: [this.t?.title ?? '', [Validators.required, Validators.maxLength(200)]],
      description: [this.t?.description ?? ''],
      assigneeId: [this.t?.assigneeId ?? (null as number | null)],
      milestoneId: [this.t?.milestoneId ?? (null as number | null)],
      status: [this.t?.status ?? 'todo'],
      priority: [this.t?.priority ?? 'medium'],
      startDate: [this.t?.startDate ?? ''],
      dueDate: [this.t?.dueDate ?? '', Validators.required],
      weight: [this.t?.weight ?? 1, [Validators.required, Validators.min(1), Validators.max(10)]],
      progressPct: [this.t?.progressPct ?? 0, [Validators.required, Validators.min(0), Validators.max(100)]],
    },
    { validators: dueAfterStart },
  );

  protected err(name: string): string { return fieldError(this.form.get(name)); }

  protected async save(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const body = {
      title: v.title!, description: v.description || null, assigneeId: v.assigneeId, milestoneId: v.milestoneId, status: v.status!, priority: v.priority!,
      startDate: v.startDate || null, dueDate: v.dueDate!, weight: Number(v.weight), progressPct: Number(v.progressPct),
    };
    this.saving.set(true);
    try {
      await firstValueFrom(this.t ? this.api.update(this.t.id, { ...body, version: this.t.version }) : this.api.create(this.data.projectId, body));
      this.notify.success('Task saved.');
      this.ref.close(true);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form as never, err)) this.notify.error(err);
      if (err.status === 409) this.ref.close(false);   // stale copy: the list reloads with the current version
    } finally {
      this.saving.set(false);
    }
  }
}
