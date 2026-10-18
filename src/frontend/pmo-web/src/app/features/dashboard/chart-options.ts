import { CategorySpend, MonthlyPoint } from '../../core/models/finance.models';
import { OverdueByOwner, ProjectFinanceBar, RiskMatrixCell } from '../../core/models/dashboard.models';

const thousands = (v: number) => (Math.abs(v) >= 1_000_000 ? `${(v / 1_000_000).toFixed(1)}M` : `${Math.round(v / 1000)}k`);
const axisMoney = { type: 'value' as const, axisLabel: { formatter: thousands } };
const aed = (v: unknown) => `AED ${Math.round(Number(v)).toLocaleString('en-AE')}`;

/** Visual 1: budget vs actual vs EAC for each project (grouped bars). */
export function budgetVsActualOption(rows: ProjectFinanceBar[]) {
  return {
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: aed }, legend: { bottom: 0 }, grid: { left: 56, right: 12, top: 16, bottom: 56 },
    xAxis: { type: 'category', data: rows.map((r) => r.code), axisLabel: { rotate: rows.length > 6 ? 35 : 0 } }, yAxis: axisMoney,
    series: [
      { name: 'Current budget', type: 'bar', data: rows.map((r) => r.currentBudget) },
      { name: 'Actual spend', type: 'bar', data: rows.map((r) => r.actualSpend) },
      { name: 'EAC', type: 'bar', data: rows.map((r) => r.eac) },
    ],
  };
}

/** Visual 2: actual spend per month and the forecast for the months after the as-of date. */
export function monthlyOption(rows: MonthlyPoint[]) {
  return {
    tooltip: { trigger: 'axis', valueFormatter: aed }, legend: { bottom: 0 }, grid: { left: 56, right: 12, top: 16, bottom: rows.length > 12 ? 72 : 36 },
    xAxis: { type: 'category', data: rows.map((r) => r.month.slice(0, 7)), axisLabel: { rotate: rows.length > 12 ? 40 : 0 } }, yAxis: axisMoney,
    series: [
      { name: 'Actual', type: 'bar', stack: 'spend', data: rows.map((r) => r.actualSpend) },
      { name: 'Forecast', type: 'bar', stack: 'spend', data: rows.map((r) => r.forecast), itemStyle: { opacity: 0.55 } },
    ],
  };
}

/** Visual 3: spend by category (donut). The table behind it is the cost drill-down. */
export function categoryOption(rows: CategorySpend[]) {
  return {
    tooltip: { trigger: 'item', valueFormatter: aed }, legend: { bottom: 0 },
    series: [{ name: 'Actual spend', type: 'pie', radius: ['42%', '68%'], center: ['50%', '44%'], label: { formatter: '{b}: {d}%' }, data: rows.map((r) => ({ name: r.name, value: r.actualSpend })) }],
  };
}

/** Visual 4: overdue open tasks per assignee (horizontal bars, most overdue on top). */
export function overdueOption(rows: OverdueByOwner[]) {
  const sorted = [...rows].reverse();
  return {
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } }, grid: { left: 130, right: 24, top: 8, bottom: 24 },
    xAxis: { type: 'value', minInterval: 1 }, yAxis: { type: 'category', data: sorted.map((r) => r.ownerName) },
    series: [{ name: 'Overdue tasks', type: 'bar', data: sorted.map((r) => r.overdueTasks), label: { show: true, position: 'right' } }],
  };
}

/** Visual 5: 5 x 5 risk matrix; each cell shows the number of open items and opens the list behind it. */
export function matrixOption(cells: RiskMatrixCell[]) {
  const max = Math.max(1, ...cells.map((c) => c.openCount));
  return {
    tooltip: { formatter: (p: { data: number[] }) => `Probability ${p.data[1] + 1}, impact ${p.data[0] + 1}: ${p.data[2]} open` },
    grid: { left: 70, right: 16, top: 16, bottom: 52 },
    xAxis: { type: 'category', name: 'Impact', nameLocation: 'middle', nameGap: 28, data: ['1', '2', '3', '4', '5'], splitArea: { show: true } },
    yAxis: { type: 'category', name: 'Probability', nameLocation: 'middle', nameGap: 44, data: ['1', '2', '3', '4', '5'], splitArea: { show: true } },
    visualMap: { show: false, min: 0, max, inRange: { color: ['#f4f4f6', '#fdd0a2', '#e8590c', '#b3261e'] } },
    series: [{
      type: 'heatmap', data: cells.map((c) => [c.impact - 1, c.probability - 1, c.openCount]),
      label: { show: true, formatter: (p: { data: number[] }) => (p.data[2] ? String(p.data[2]) : '') }, itemStyle: { borderColor: '#fff', borderWidth: 2 },
    }],
  };
}
