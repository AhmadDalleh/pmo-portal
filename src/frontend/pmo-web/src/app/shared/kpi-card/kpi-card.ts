import { Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

/**
 * One headline number. The value arrives already computed by the API (never calculated here).
 * `how` is the "How is this calculated?" text (story G7). When `clickable`, the card is a button that opens the drill-down.
 */
@Component({
  selector: 'app-kpi-card',
  imports: [MatIconModule, MatTooltipModule],
  template: `
    <button type="button" class="card" [class.static]="!clickable()" [disabled]="!clickable()" (click)="select.emit()"
            [attr.aria-label]="label() + ': ' + value() + (clickable() ? '. Open the records behind this number.' : '')">
      <span class="top">
        <span class="label">{{ label() }}</span>
        <mat-icon class="info" [matTooltip]="how()" matTooltipPosition="above" aria-hidden="true">info_outline</mat-icon>
      </span>
      <span class="value" [class.bad]="tone() === 'bad'" [class.warn]="tone() === 'warn'">{{ value() }}</span>
      @if (hint()) { <span class="hint">{{ hint() }}</span> }
    </button>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .card { all: unset; box-sizing: border-box; display: flex; flex-direction: column; gap: 4px; width: 100%; height: 100%; padding: 12px 14px;
            border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; background: var(--mat-sys-surface-container-lowest); cursor: pointer; }
    .card:hover:not(.static) { background: var(--mat-sys-surface-container); }
    .card:focus-visible { outline: 2px solid var(--mat-sys-primary); outline-offset: 2px; }
    .static { cursor: default; }
    .top { display: flex; justify-content: space-between; align-items: center; gap: 6px; }
    .label { font-size: 12px; color: var(--mat-sys-on-surface-variant); }
    .info { font-size: 16px; width: 16px; height: 16px; color: var(--mat-sys-on-surface-variant); }
    .value { font-size: 22px; font-weight: 600; overflow-wrap: anywhere; }
    .value.bad { color: #b3261e; }
    .value.warn { color: #8a5a00; }
    .hint { font-size: 11px; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class KpiCard {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly hint = input('');
  readonly how = input('');
  readonly tone = input<'normal' | 'warn' | 'bad'>('normal');
  readonly clickable = input(false);
  readonly select = output<void>();
}
