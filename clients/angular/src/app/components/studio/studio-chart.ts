import { Component, computed, inject, input } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartRecommendation } from '../../models';
import { studioChartOption } from '../../util';
import { ThemeService } from '../../services/theme.service';
import { ChartModalService } from '../../services/chart-modal.service';
import { StudioCard } from './studio-card';

/** Pre-made chart card (all ECharts forms) — the recommendation only seeds data + title. */
@Component({
  selector: 'app-studio-chart',
  imports: [NgxEchartsDirective, StudioCard],
  template: `
    <app-studio-card [title]="chart().title" [badge]="chart().type">
      <button
        headerActions
        (click)="expand()"
        class="rounded-lg p-1 text-neutral-500 transition-colors hover:bg-neutral-100 hover:text-neutral-800 dark:hover:bg-neutral-800 dark:hover:text-neutral-200"
        aria-label="Expand chart"
      >⤢</button>
      <div echarts [options]="options()" class="h-80 w-full"></div>
    </app-studio-card>
  `,
})
export class StudioChart {
  private theme = inject(ThemeService);
  private chartModal = inject(ChartModalService);
  chart = input.required<ChartRecommendation>();
  options = computed(() => studioChartOption(this.chart(), this.theme.isDark()));

  expand(): void {
    this.chartModal.open(this.chart().title, this.options(), this.chart().type);
  }
}
