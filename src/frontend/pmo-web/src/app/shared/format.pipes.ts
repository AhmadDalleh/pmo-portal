import { Pipe, PipeTransform } from '@angular/core';

const aed = new Intl.NumberFormat('en-AE', { style: 'currency', currency: 'AED', currencyDisplay: 'code', maximumFractionDigits: 0 });
const aedCents = new Intl.NumberFormat('en-AE', { style: 'currency', currency: 'AED', currencyDisplay: 'code', minimumFractionDigits: 2 });

/** 1234567.5 -> "AED 1,234,568". Units are always visible; use "cents" for transaction lists. */
@Pipe({ name: 'aed' })
export class AedPipe implements PipeTransform {
  transform(value: number | null | undefined, precision: 'whole' | 'cents' = 'whole'): string {
    if (value === null || value === undefined) {
      return '-';
    }
    return (precision === 'cents' ? aedCents : aed).format(value).replace(/ /g, ' ');
  }
}

/** A fraction (0.92) or a percentage already in points (63.2), shown as "92.0%". Missing values show "-". */
@Pipe({ name: 'pct' })
export class PctPipe implements PipeTransform {
  transform(value: number | null | undefined, unit: 'fraction' | 'points' = 'points'): string {
    if (value === null || value === undefined) {
      return '-';
    }
    return `${(unit === 'fraction' ? value * 100 : value).toFixed(1)}%`;
  }
}
