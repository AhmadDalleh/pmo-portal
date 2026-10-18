import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ApiError, LoadStatus } from '../../core/models/common.models';

/**
 * Wraps any list, card or chart with the three states every async view needs:
 * loading (skeleton bar), empty ("No data for these filters") and error (message, correlation id, Retry).
 */
@Component({
  selector: 'app-async-state',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    @switch (status()) {
      @case ('loading') {
        <div class="state" role="status" aria-live="polite">
          <mat-progress-bar mode="indeterminate" aria-label="Loading" />
          <span class="muted">Loading...</span>
        </div>
      }
      @case ('error') {
        <div class="state error" role="alert">
          <mat-icon aria-hidden="true">error_outline</mat-icon>
          <div>
            <strong>{{ error()?.detail ?? 'Something went wrong.' }}</strong>
            @if (error()?.correlationId) {
              <div class="muted">Reference: {{ error()?.correlationId }}</div>
            }
          </div>
          <button mat-stroked-button type="button" (click)="retry.emit()">Retry</button>
        </div>
      }
      @default {
        @if (empty()) {
          <div class="state muted" role="status"><mat-icon aria-hidden="true">inbox</mat-icon> {{ emptyText() }}</div>
        } @else {
          <ng-content />
        }
      }
    }
  `,
  styles: `
    .state { display: flex; align-items: center; gap: 12px; padding: 16px; flex-wrap: wrap; }
    .state.error { border: 1px solid var(--mat-sys-error); border-radius: 8px; color: var(--mat-sys-error); }
    .muted { color: var(--mat-sys-on-surface-variant); }
    mat-progress-bar { flex: 1 1 100%; }
  `,
})
export class AsyncState {
  readonly status = input<LoadStatus>('idle');
  readonly empty = input(false);
  readonly error = input<ApiError | null>(null);
  readonly emptyText = input('No data for these filters.');
  readonly retry = output<void>();
}
