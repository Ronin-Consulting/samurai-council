import { Component, computed, inject, input } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartRecommendation } from '../models';
import { chartToEChartsOption } from '../util';
import { ThemeService } from '../services/theme.service';

@Component({
  selector: 'app-chart-display',
  imports: [NgxEchartsDirective],
  template: `
    @if (visible()) {
      <div class="rounded-lg border border-neutral-200 dark:border-neutral-700 p-2 my-3">
        <div echarts [options]="options()" class="h-80 w-full"></div>
      </div>
    }
  `,
})
export class ChartDisplay {
  private theme = inject(ThemeService);
  chart = input.required<ChartRecommendation>();

  visible = computed(() => {
    const c = this.chart();
    return !!c && c.type !== 'None' && (c.labels?.length ?? 0) > 0 && (c.series?.length ?? 0) > 0;
  });

  options = computed(() => chartToEChartsOption(this.chart(), this.theme.isDark()));
}
