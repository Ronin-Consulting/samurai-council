import { Component, input } from '@angular/core';
import { StatData } from '../models';

/** One or a few headline numbers (KPI row). Used for ChartType 'Stat'. */
@Component({
  selector: 'app-stat-tile',
  template: `
    <div class="flex flex-wrap gap-3 my-3">
      @for (s of stats(); track s.label) {
        <div class="flex-1 min-w-[150px] rounded-xl border border-neutral-200 dark:border-neutral-700 p-4">
          <div class="text-xs uppercase tracking-wide text-neutral-500">{{ s.label }}</div>
          <div class="mt-1 text-3xl font-semibold tabular-nums text-neutral-900 dark:text-neutral-50">{{ format(s) }}</div>
          @if (s.caption) { <div class="mt-1 text-xs text-neutral-500">{{ s.caption }}</div> }
          @if (s.deltaPercent != null) {
            <div class="mt-1 text-xs" [style.color]="s.deltaPercent >= 0 ? '#0ca30c' : '#d03b3b'">
              {{ s.deltaPercent >= 0 ? '▲' : '▼' }} {{ absPct(s.deltaPercent) }}%
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class StatTile {
  stats = input.required<StatData[]>();

  absPct(d: number): string { return Math.abs(d).toFixed(1); }

  format(s: StatData): string {
    const v = s.value;
    const n = Math.abs(v) >= 1000
      ? new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(v)
      : new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(v);
    const unit = s.unit ?? '';
    if (unit === '$') return '$' + n;
    if (unit === '%') return n + '%';
    return unit ? `${n} ${unit}` : n;
  }
}
