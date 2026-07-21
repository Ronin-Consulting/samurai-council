import { TestBed } from '@angular/core/testing';
import { ChartDisplay } from './chart-display';
import { ChartModalService } from '../services/chart-modal.service';
import { ChartRecommendation } from '../models';

describe('ChartDisplay', () => {
  it('expand() opens the chart modal with the chart title and current options', () => {
    TestBed.configureTestingModule({ imports: [ChartDisplay] });
    const fixture = TestBed.createComponent(ChartDisplay);
    const chart: ChartRecommendation = {
      type: 'Bar',
      title: 'Revenue by Region',
      labels: ['a', 'b'],
      series: [{ name: 's', values: [1, 2] }],
    };
    fixture.componentRef.setInput('chart', chart);

    fixture.componentInstance.expand();

    const chartModal = TestBed.inject(ChartModalService);
    expect(chartModal.state()?.title).toBe('Revenue by Region');
    expect(chartModal.state()?.options).toEqual(fixture.componentInstance.options());
  });
});
