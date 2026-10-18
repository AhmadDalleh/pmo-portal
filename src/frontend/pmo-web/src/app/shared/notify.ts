import { Injectable, inject } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '../core/models/common.models';

/** Toasts for success and for errors that are not tied to a form field. */
@Injectable({ providedIn: 'root' })
export class Notify {
  private readonly snack = inject(MatSnackBar);

  success(message: string): void {
    this.snack.open(message, 'Close', { duration: 4000 });
  }

  /** 403 and 409 show the server's own explanation; the reference lets support find the log line. */
  error(e: ApiError): void {
    const ref = e.correlationId ? ` (ref ${e.correlationId.slice(0, 8)})` : '';
    this.snack.open((e.detail ?? 'The request failed.') + ref, 'Close', { duration: 9000 });
  }
}

/**
 * Puts the server's field messages (ProblemDetails.errors, camelCase keys) onto the matching form controls.
 * Returns false when nothing matched, so the caller can show a toast instead.
 */
export function applyServerErrors(form: { controls: Record<string, AbstractControl> }, e: ApiError): boolean {
  let matched = false;
  for (const [field, messages] of Object.entries(e.errors ?? {})) {
    const control = form.controls[field];
    if (control) {
      control.setErrors({ server: messages[0] });
      control.markAsTouched();
      matched = true;
    }
  }
  return matched;
}

/** First message for a control: server text first, then the usual client rules. */
export function fieldError(control: AbstractControl | null): string {
  if (!control?.errors) return '';
  const e = control.errors;
  if (e['server']) return String(e['server']);
  if (e['required']) return 'This is required.';
  if (e['min']) return `Must be at least ${e['min'].min}.`;
  if (e['max']) return `Must be at most ${e['max'].max}.`;
  if (e['maxlength']) return `At most ${e['maxlength'].requiredLength} characters.`;
  if (e['pattern']) return 'Invalid format.';
  if (e['dateOrder']) return 'The end must be on or after the start.';
  return 'Invalid value.';
}
