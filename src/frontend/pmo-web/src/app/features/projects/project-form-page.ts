import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/http/api-error';
import { LookupItem, UserLookup } from '../../core/models/common.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

/** The planned end must not be before the start: the same rule the API enforces (story B1). */
export function endAfterStart(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value as string;
  const end = group.get('plannedEndDate')?.value as string;
  return start && end && end < start ? { dateOrder: true } : null;
}

/** Story B1: create a project with its owner and initial budget. */
@Component({
  selector: 'app-project-form-page',
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <div class="page">
      <h1>New project</h1>
      <form [formGroup]="form" (ngSubmit)="submit()" class="panel">
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Code</mat-label>
            <input matInput formControlName="code" placeholder="PRJ-011" required />
            <mat-error>{{ err('code') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Name</mat-label>
            <input matInput formControlName="name" required />
            <mat-error>{{ err('name') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Department</mat-label>
            <mat-select formControlName="departmentId" required>
              @for (d of departments(); track d.id) { <mat-option [value]="d.id">{{ d.name }}</mat-option> }
            </mat-select>
            <mat-error>{{ err('departmentId') }}</mat-error>
          </mat-form-field>
          @if (auth.isAdmin()) {
            <mat-form-field appearance="outline">
              <mat-label>Owner (Project Manager)</mat-label>
              <mat-select formControlName="ownerId" required>
                @for (u of managers(); track u.id) { <mat-option [value]="u.id">{{ u.fullName }}</mat-option> }
              </mat-select>
              <mat-error>{{ err('ownerId') }}</mat-error>
            </mat-form-field>
          }
          <mat-form-field appearance="outline">
            <mat-label>Start date</mat-label>
            <input matInput type="date" formControlName="startDate" required />
            <mat-error>{{ err('startDate') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Planned end date</mat-label>
            <input matInput type="date" formControlName="plannedEndDate" required />
            <mat-error>{{ err('plannedEndDate') || (form.hasError('dateOrder') ? 'The end must be on or after the start.' : '') }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Initial approved budget (AED)</mat-label>
            <input matInput type="number" min="0" step="0.01" formControlName="initialBudget" required />
            <mat-error>{{ err('initialBudget') }}</mat-error>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Description</mat-label>
          <textarea matInput rows="3" formControlName="description"></textarea>
        </mat-form-field>
        <div class="row">
          <button mat-flat-button type="submit" [disabled]="saving()">Create project</button>
          <a mat-button routerLink="/projects">Cancel</a>
        </div>
      </form>
    </div>
  `,
})
export class ProjectFormPage {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ProjectsApi);
  private readonly router = inject(Router);
  private readonly notify = inject(Notify);

  protected readonly departments = signal<LookupItem[]>([]);
  protected readonly managers = signal<UserLookup[]>([]);
  protected readonly saving = signal(false);

  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      code: ['', [Validators.required, Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9-]+$/)]],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      departmentId: [0, [Validators.required, Validators.min(1)]],
      ownerId: [0],
      startDate: ['', Validators.required],
      plannedEndDate: ['', Validators.required],
      initialBudget: [0, [Validators.required, Validators.min(0)]],
      description: [''],
    },
    { validators: endAfterStart },
  );

  constructor() {
    void firstValueFrom(this.api.departments()).then((d) => this.departments.set(d));
    if (this.auth.isAdmin()) {
      void firstValueFrom(this.api.users('ProjectManager')).then((u) => this.managers.set(u));
    }
  }

  protected err(name: string): string {
    return fieldError(this.form.get(name));
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.saving.set(true);
    try {
      const created = await firstValueFrom(this.api.create({
        code: v.code, name: v.name, description: v.description || null, departmentId: v.departmentId,
        ownerId: this.auth.isAdmin() ? v.ownerId : null, startDate: v.startDate, plannedEndDate: v.plannedEndDate, initialBudget: v.initialBudget,
      }));
      this.notify.success(`Project ${created.code} created.`);
      await this.router.navigate(['/projects', created.id]);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form, err)) this.notify.error(err);
    } finally {
      this.saving.set(false);
    }
  }
}
