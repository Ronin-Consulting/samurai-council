import { Component, input } from '@angular/core';
import { ChartRecommendation } from '../models';
import { ChartDisplay } from './chart-display';
import { StatTile } from './stat-tile';
import { DataTable } from './data-table';

/**
 * Dispatches a ChartRecommendation to the right visual:
 * Stat -> KPI tiles, Table -> data table, None -> nothing, everything else -> ECharts chart.
 */
@Component({
  selector: 'app-visual-display',
  imports: [ChartDisplay, StatTile, DataTable],
  template: `
    @if (recommendation(); as r) {
      @switch (r.type) {
        @case ('Stat') { @if (r.stats?.length) { <app-stat-tile [stats]="r.stats!" /> } }
        @case ('Table') { @if (r.table) { <app-data-table [table]="r.table!" /> } }
        @case ('None') {}
        @default { <app-chart-display [chart]="r" /> }
      }
    }
  `,
})
export class VisualDisplay {
  recommendation = input.required<ChartRecommendation>();
}
