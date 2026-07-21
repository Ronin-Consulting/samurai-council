import { Component, computed, inject, input } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartRecommendation } from '../models';
import { chartToEChartsOption } from '../util';
import { ThemeService } from '../services/theme.service';
import { ChartModalService } from '../services/chart-modal.service';

@Component({
  selector: 'app-chart-display',
  imports: [NgxEchartsDirective],
  template: `
    @if (visible()) {
      <div class="relative rounded-lg border border-neutral-200 dark:border-neutral-700 p-2 my-3">
        <button
          (click)="expand()"
          class="absolute right-2 top-2 z-10 rounded-lg bg-white/80 p-1 text-neutral-500 shadow-sm transition-colors hover:bg-neutral-100 hover:text-neutral-800 dark:bg-neutral-900/80 dark:text-neutral-400 dark:hover:bg-neutral-800 dark:hover:text-neutral-200"
          aria-label="Expand chart"
        >⤢</button>
        <div echarts [options]="options()" class="h-80 w-full"></div>
      </div>
    }
  `,
})
export class ChartDisplay {
  private theme = inject(ThemeService);
  private chartModal = inject(ChartModalService);
  chart = input.required<ChartRecommendation>();

  visible = computed(() => {
    const c = this.chart();
    if (!c || c.type === 'None') return false;
    if (c.type === 'Scatter') return c.series?.some((s) => (s.points?.length ?? 0) > 0) ?? false;
    return (c.labels?.length ?? 0) > 0 && (c.series?.length ?? 0) > 0;
  });

  options = computed(() => chartToEChartsOption(this.chart(), this.theme.isDark()));

  expand(): void {
    this.chartModal.open(this.chart().title, this.options());
  }
}
