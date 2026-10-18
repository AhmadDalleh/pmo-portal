import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface ConfirmData {
  title: string;
  /** Say exactly what will happen, e.g. "Post AED 12,500.00 to PRJ-002? Posted costs cannot be edited." */
  message: string;
  confirmLabel?: string;
  /** When set, the user must type a reason; the dialog then returns the text instead of true. */
  reasonLabel?: string;
}

/** Result: false/undefined = cancelled, true = confirmed, string = confirmed with a reason. */
export type ConfirmResult = boolean | string | undefined;

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
      @if (data.reasonLabel) {
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ data.reasonLabel }}</mat-label>
          <textarea matInput rows="3" [formControl]="reason" required></textarea>
          @if (reason.invalid && reason.touched) { <mat-error>This is required.</mat-error> }
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="ref.close(false)">Cancel</button>
      <button mat-flat-button type="button" (click)="confirm()">{{ data.confirmLabel ?? 'Confirm' }}</button>
    </mat-dialog-actions>
  `,
  styles: `.full { width: 100%; min-width: 320px; }`,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmData>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<ConfirmDialog, ConfirmResult>>(MatDialogRef);
  protected readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] });

  protected confirm(): void {
    if (!this.data.reasonLabel) {
      this.ref.close(true);
    } else if (this.reason.valid && this.reason.value.trim()) {
      this.ref.close(this.reason.value.trim());
    } else {
      this.reason.markAsTouched();
    }
  }
}
