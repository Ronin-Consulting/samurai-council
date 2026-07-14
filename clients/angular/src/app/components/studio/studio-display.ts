import { Component, input } from '@angular/core';
import { ChartRecommendation } from '../../models';
import { StudioChart } from './studio-chart';
import { StudioKpi } from './studio-kpi';
import { StudioTable } from './studio-table';

/** V2 dispatcher: routes a (deterministically-classified) recommendation to a pre-made Studio card. */
@Component({
  selector: 'app-studio-display',
  imports: [StudioChart, StudioKpi, StudioTable],
  template: `
    @if (recommendation(); as r) {
      @switch (r.type) {
        @case ('Stat') { @if (r.stats?.length) { <app-studio-kpi [stats]="r.stats!" /> } }
        @case ('Table') { @if (r.table) { <app-studio-table [table]="r.table!" /> } }
        @case ('None') {}
        @default { <app-studio-chart [chart]="r" /> }
      }
    }
  `,
})
export class StudioDisplay {
  recommendation = input.required<ChartRecommendation>();
}
