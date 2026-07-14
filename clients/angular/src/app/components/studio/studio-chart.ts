import { Component, computed, inject, input } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartRecommendation } from '../../models';
import { studioChartOption } from '../../util';
import { ThemeService } from '../../services/theme.service';
import { StudioCard } from './studio-card';

/** Pre-made chart card (all ECharts forms) — the recommendation only seeds data + title. */
@Component({
  selector: 'app-studio-chart',
  imports: [NgxEchartsDirective, StudioCard],
  template: `
    <app-studio-card [title]="chart().title" [badge]="chart().type">
      <div echarts [options]="options()" class="h-80 w-full"></div>
    </app-studio-card>
  `,
})
export class StudioChart {
  private theme = inject(ThemeService);
  chart = input.required<ChartRecommendation>();
  options = computed(() => studioChartOption(this.chart(), this.theme.isDark()));
}
