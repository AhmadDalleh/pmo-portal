import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { toApiError } from '../../core/http/api-error';
import { CostCategory } from '../../core/models/finance.models';
import { Notify, applyServerErrors, fieldError } from '../../shared/notify';

/** Story F2: create a category (code and name) or rename one. The code never changes once created. */
@Component({
  selector: 'app-category-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Rename ' + data.code : 'New cost category' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        @if (!data) {
          <mat-form-field appearance="outline" class="full">
            <mat-label>Code</mat-label><input matInput formControlName="code" placeholder="TRAINING" required />
            <mat-hint>Letters, digits and _; stored in capitals.</mat-hint><mat-error>{{ err('code') }}</mat-error>
          </mat-form-field>
        }
        <mat-form-field appearance="outline" class="full"><mat-label>Name</mat-label><input matInput formControlName="name" required /><mat-error>{{ err('name') }}</mat-error></mat-form-field>
        @if (serverMessage()) { <p class="neg" role="alert">{{ serverMessage() }}</p> }
      </mat-dialog-content>
      <mat-dialog-actions align="end"><button mat-button type="button" mat-dialog-close>Cancel</button><button mat-flat-button type="submit" [disabled]="saving()">Save</button></mat-dialog-actions>
    </form>
  `,
})
export class CategoryDialog {
  protected readonly data = inject<CostCategory | null>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<CategoryDialog, boolean>>(MatDialogRef);
  private readonly api = inject(AdminApi);
  private readonly notify = inject(Notify);

  protected readonly saving = signal(false);
  protected readonly serverMessage = signal('');
  protected readonly form = inject(FormBuilder).nonNullable.group({
    code: [this.data?.code ?? '', this.data ? [] : [Validators.required, Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9_]+$/)]],
    name: [this.data?.name ?? '', [Validators.required, Validators.maxLength(100)]],
  });

  protected err(name: string): string { return fieldError(this.form.get(name)); }

  protected async save(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.serverMessage.set('');
    try {
      await firstValueFrom(this.data ? this.api.updateCategory(this.data.id, v.name, this.data.isActive) : this.api.createCategory(v.code, v.name));
      this.notify.success('Category saved.');
      this.ref.close(true);
    } catch (e) {
      const err = toApiError(e);
      if (!applyServerErrors(this.form, err)) this.serverMessage.set(err.detail ?? 'The request failed.');   // e.g. 409: code or name already used
    } finally { this.saving.set(false); }
  }
}
