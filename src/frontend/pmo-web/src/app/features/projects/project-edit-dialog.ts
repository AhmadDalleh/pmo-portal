import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { toApiError } from '../../core/http/api-error';
import { UserLookup } from '../../core/models/common.models';
import { PROJECT_STATUSES, Project } from '../../core/models/project.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';
import { endAfterStart } from './project-form-page';

/** Story B2. Sends the project's Version back, so editing a stale copy is refused with 409. */
@Component({
  selector: 'app-project-edit-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>Edit {{ project.code }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="name" required /><mat-error>{{ err('name') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select formControlName="status">@for (s of statuses; track s) { <mat-option [value]="s">{{ s.replace('_', ' ') }}</mat-option> }</mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Owner</mat-label>
            <mat-select formControlName="ownerId">@for (u of managers(); track u.id) { <mat-option [value]="u.id">{{ u.fullName }}</mat-option> }</mat-select>
            <mat-error>{{ err('ownerId') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Start date</mat-label><input matInput type="date" formControlName="startDate" /><mat-error>{{ err('startDate') }}</mat-error></mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Planned end date</mat-label><input matInput type="date" formControlName="plannedEndDate" />
            <mat-error>{{ err('plannedEndDate') || (form.hasError('dateOrder') ? 'The end must be on or after the start.' : '') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Actual end date</mat-label><input matInput type="date" formControlName="actualEndDate" />
            <mat-error>{{ err('actualEndDate') }}</mat-error>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full"><mat-label>Description</mat-label><textarea matInput rows="3" formControlName="description"></textarea></mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class ProjectEditDialog {
  protected readonly project = inject<Project>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<ProjectEditDialog, Project>>(MatDialogRef);
  private readonly api = inject(ProjectsApi);
  private readonly notify = inject(Notify);

  protected readonly statuses = PROJECT_STATUSES;
  protected readonly managers = signal<UserLookup[]>([]);
  protected readonly saving = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      name: [this.project.name, [Validators.required, Validators.maxLength(200)]],
      status: [this.project.status],
      ownerId: [this.project.ownerId, Validators.required],
      startDate: [this.project.startDate, Validators.required],
      plannedEndDate: [this.project.plannedEndDate, Validators.required],
      actualEndDate: [this.project.actualEndDate ?? ''],
      description: [this.project.description ?? ''],
    },
    { validators: endAfterStart },
  );

  constructor() {
    void firstValueFrom(this.api.users('ProjectManager')).then((u) => this.managers.set(u));
  }

  protected err(name: string): string {
    return fieldError(this.form.get(name));
  }

  protected async save(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.api.update(this.project.id, {
        name: v.name, description: v.description || null, ownerId: v.ownerId, status: v.status, startDate: v.startDate,
        plannedEndDate: v.plannedEndDate, actualEndDate: v.actualEndDate || null, version: this.project.version,
      }));
      this.notify.success('Project saved.');
      this.ref.close(updated);
    } catch (e) {
      const err = toApiError(e);
      if (err.status === 409) { this.notify.error(err); this.ref.close(undefined); }   // someone else saved first: the page reloads
      else if (!applyServerErrors(this.form, err)) this.notify.error(err);
    } finally {
      this.saving.set(false);
    }
  }
}
