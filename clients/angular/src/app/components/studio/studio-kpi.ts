import { Component, input } from '@angular/core';
import { StatData } from '../../models';
import { StudioCard } from './studio-card';

/** Pre-made KPI card (accent-bar tiles). Used for ChartType 'Stat'. */
@Component({
  selector: 'app-studio-kpi',
  imports: [StudioCard],
  template: `
    <app-studio-card title="Key figures" badge="KPI">
      <div class="flex flex-wrap gap-4">
        @for (s of stats(); track s.label) {
          <div class="relative min-w-[160px] flex-1 rounded-xl bg-white p-4 pl-5 dark:bg-neutral-800/60">
            <span class="absolute left-0 top-3 bottom-3 w-1 rounded bg-red-600"></span>
            <div class="text-xs uppercase tracking-wide text-neutral-500">{{ s.label }}</div>
            <div class="mt-1 text-3xl font-bold tabular-nums text-neutral-900 dark:text-neutral-50">{{ fmt(s) }}</div>
            @if (s.caption) { <div class="mt-1 text-xs text-neutral-500">{{ s.caption }}</div> }
          </div>
        }
      </div>
    </app-studio-card>
  `,
})
export class StudioKpi {
  stats = input.required<StatData[]>();

  fmt(s: StatData): string {
    const n = Math.abs(s.value) >= 1000
      ? new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(s.value)
      : new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(s.value);
    const u = s.unit ?? '';
    return u === '$' ? '$' + n : u === '%' ? n + '%' : u ? `${n} ${u}` : n;
  }
}
