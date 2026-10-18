import { Component, input, output } from '@angular/core';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { AsyncState } from '../async-state/async-state';

/**
 * A titled card around one ECharts chart, with loading, empty and error states.
 * Chart clicks are forwarded as `pointClick` (the dashboard turns them into drill-downs).
 */
@Component({
  selector: 'app-chart-card',
  imports: [AsyncState, NgxEchartsDirective],
  template: `
    <section class="card" [attr.aria-label]="title()">
      <header>
        <h3>{{ title() }}</h3>
        @if (note()) { <span class="note">{{ note() }}</span> }
      </header>
      <app-async-state [status]="status()" [empty]="empty()" [error]="error()" [emptyText]="emptyText()" (retry)="retry.emit()">
        <div echarts class="chart" [options]="option()" [style.height.px]="heightPx()" [autoResize]="true" (chartInit)="onInit($event)" (chartClick)="pointClick.emit($event)"></div>
      </app-async-state>
    </section>
  `,
  styles: `
    .card { border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; padding: 12px 14px; background: var(--mat-sys-surface-container-lowest); height: 100%; box-sizing: border-box; }
    header { display: flex; justify-content: space-between; align-items: baseline; gap: 8px; }
    h3 { margin: 0 0 6px; font-size: 15px; font-weight: 600; }
    .note { font-size: 11px; color: var(--mat-sys-on-surface-variant); }
    .chart { width: 100%; }
  `,
})
export class ChartCard {
  readonly title = input.required<string>();
  readonly note = input('');
  readonly option = input.required<EChartsCoreOption>();
  readonly status = input<LoadStatus>('idle');
  readonly error = input<ApiError | null>(null);
  readonly empty = input(false);
  readonly emptyText = input('No data for these filters.');
  readonly heightPx = input(280, { alias: 'height' });
  readonly retry = output<void>();

  /** A chart created while its card is still being laid out (tabs, grids) can start with width 0; resize once the layout has settled. */
  protected onInit(chart: { resize: () => void }): void {
    setTimeout(() => chart.resize(), 0);
    setTimeout(() => chart.resize(), 350);
  }
  readonly pointClick = output<{ dataIndex: number; name: string; seriesName?: string; value?: unknown; data?: unknown }>();
}
