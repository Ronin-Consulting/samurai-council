import { Component } from '@angular/core';
import { VisualDisplay } from './visual-display';
import { ChartRecommendation } from '../models';

/** Dev/design showcase of every visualization form with sample data. Route: /gallery */
@Component({
  selector: 'app-gallery-page',
  imports: [VisualDisplay],
  template: `
    <div class="mx-auto max-w-5xl p-6">
      <h1 class="mb-1 text-2xl font-bold text-red-600">Visualization Gallery</h1>
      <p class="mb-6 text-sm text-neutral-500">Every form the text-to-SQL pipeline can produce, with sample data.</p>
      <div class="grid gap-6 md:grid-cols-2">
        @for (s of samples; track $index) {
          <div class="rounded-xl border border-neutral-200 dark:border-neutral-700 p-4">
            <div class="mb-2 text-xs uppercase tracking-wide text-neutral-500">{{ s.type }}</div>
            <app-visual-display [recommendation]="s" />
          </div>
        }
      </div>
    </div>
  `,
})
export class GalleryPage {
  private readonly channels = ['Store', 'Online', 'Reseller', 'Catalog'];
  private readonly months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'];

  samples: ChartRecommendation[] = [
    { type: 'Stat', title: '', labels: [], series: [],
      stats: [
        { label: 'Total Revenue', value: 4111233535, unit: '$', caption: '2008', deltaPercent: 12.3 },
        { label: 'Orders', value: 1250000, caption: 'FY2008', deltaPercent: -3.1 },
        { label: 'Avg Order', value: 3289, unit: '$' },
      ] },
    { type: 'Bar', title: 'Sales by Channel', labels: this.channels, xAxisLabel: 'Channel', yAxisLabel: 'Sales ($)',
      series: [{ name: 'Sales', values: [2228460306, 937069111, 600175899, 345528219] }] },
    { type: 'HorizontalBar', title: 'Top Products by Revenue', xAxisLabel: 'Revenue ($)', yAxisLabel: 'Product',
      labels: ['Litware Refrigerator X980 Blue', 'Litware Refrigerator X980 Grey', 'Adventure Works 26" LCD', 'Contoso Washer WM3200'],
      series: [{ name: 'Revenue', values: [19643042, 19261283, 15220110, 12880540] }] },
    { type: 'GroupedBar', title: 'Sales by Channel & Year', labels: this.channels, xAxisLabel: 'Channel', yAxisLabel: 'Sales ($)',
      series: [
        { name: '2007', values: [1980000000, 820000000, 540000000, 310000000] },
        { name: '2008', values: [2228460306, 937069111, 600175899, 345528219] },
      ] },
    { type: 'StackedBar', title: 'Revenue Composition by Quarter', labels: ['Q1', 'Q2', 'Q3', 'Q4'], xAxisLabel: 'Quarter', yAxisLabel: 'Sales ($)',
      series: [
        { name: 'Store', values: [520, 560, 590, 610] },
        { name: 'Online', values: [210, 230, 250, 260] },
        { name: 'Reseller', values: [140, 150, 155, 160] },
      ] },
    { type: 'Line', title: 'Monthly Sales Trend 2008', labels: this.months, xAxisLabel: 'Month', yAxisLabel: 'Sales ($M)',
      series: [{ name: 'Sales', values: [312, 298, 340, 365, 355, 390] }] },
    { type: 'Area', title: 'Cumulative Revenue', labels: this.months, xAxisLabel: 'Month', yAxisLabel: 'Revenue ($M)',
      series: [{ name: 'Revenue', values: [312, 610, 950, 1315, 1670, 2060] }] },
    { type: 'Pie', title: 'Share by Channel', labels: this.channels, series: [{ name: 'Sales', values: [54, 23, 15, 8] }] },
    { type: 'Donut', title: 'Share by Channel', labels: this.channels, series: [{ name: 'Sales', values: [54, 23, 15, 8] }] },
    { type: 'Scatter', title: 'Unit Price vs Quantity', xAxisLabel: 'Unit Price ($)', yAxisLabel: 'Quantity',
      labels: [], series: [{ name: 'Orders', values: [],
        points: [{ x: 12.5, y: 3 }, { x: 45, y: 1 }, { x: 8.2, y: 9 }, { x: 30, y: 4 }, { x: 60, y: 2 }, { x: 19, y: 6 }, { x: 5, y: 12 }] }] },
    { type: 'Table', title: 'Products', labels: [], series: [],
      table: {
        columns: ['Product', 'Category', 'Revenue'],
        rows: [
          ['Litware Refrigerator X980 Blue', 'Home Appliances', '19,643,042'],
          ['Adventure Works 26" LCD', 'TV and Video', '15,220,110'],
          ['Contoso Washer WM', 'Home Appliances', '12,880,540'],
          ['Fabrikam Trendsetter', 'Cameras', '9,410,220'],
        ],
      } },
  ];
}
