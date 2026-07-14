import { chartToEChartsOption, deAnonymize, getShortModelName } from './util';
import { ChartRecommendation } from './models';

describe('util', () => {
  it('shortens provider/model ids', () => {
    expect(getShortModelName('openai/gpt-4o')).toBe('GPT-4o');
    expect(getShortModelName('openai/claude-opus-4-1')).toBe('Claude Opus');
    expect(getShortModelName('openai/gemini-2.5-flash')).toBe('Gemini 2.5 Flash');
  });

  it('de-anonymizes Stage 2 labels to short model names', () => {
    const out = deAnonymize('Response A is best, then Response B', {
      'Response A': 'openai/gpt-4o',
      'Response B': 'openai/gemini-2.5-flash',
    });
    expect(out).toContain('**GPT-4o**');
    expect(out).toContain('**Gemini 2.5 Flash**');
  });

  it('maps a Bar chart to an ECharts option', () => {
    const chart: ChartRecommendation = {
      type: 'Bar', title: 'T', labels: ['a', 'b'],
      series: [{ name: 's', values: [1, 2] }],
    };
    const o = chartToEChartsOption(chart, true);
    expect(o.series[0].type).toBe('bar');
    expect(o.xAxis.data).toEqual(['a', 'b']);
    expect(o.series[0].data).toEqual([1, 2]);
  });

  it('maps a Donut chart to a pie series with a ring radius', () => {
    const chart: ChartRecommendation = {
      type: 'Donut', title: '', labels: ['x', 'y'],
      series: [{ name: 's', values: [3, 4] }],
    };
    const o = chartToEChartsOption(chart, false);
    expect(o.series[0].type).toBe('pie');
    expect(Array.isArray(o.series[0].radius)).toBe(true);
    expect(o.series[0].data).toEqual([{ name: 'x', value: 3 }, { name: 'y', value: 4 }]);
  });

  it('maps HorizontalBar by swapping category to the y-axis', () => {
    const chart: ChartRecommendation = { type: 'HorizontalBar', title: '', labels: ['a', 'b'], series: [{ name: 's', values: [1, 2] }] };
    const o = chartToEChartsOption(chart, false);
    expect(o.yAxis.type).toBe('category');
    expect(o.xAxis.type).toBe('value');
    expect(o.series[0].type).toBe('bar');
  });

  it('maps StackedBar with stack set and GroupedBar without', () => {
    const stacked = chartToEChartsOption({ type: 'StackedBar', title: '', labels: ['a'], series: [{ name: 'x', values: [1] }, { name: 'y', values: [2] }] }, false);
    expect(stacked.series.every((s: any) => s.stack === 'total')).toBe(true);
    const grouped = chartToEChartsOption({ type: 'GroupedBar', title: '', labels: ['a'], series: [{ name: 'x', values: [1] }, { name: 'y', values: [2] }] }, false);
    expect(grouped.series.every((s: any) => s.stack === undefined)).toBe(true);
  });

  it('maps Area to a filled line', () => {
    const o = chartToEChartsOption({ type: 'Area', title: '', labels: ['a', 'b'], series: [{ name: 's', values: [1, 2] }] }, false);
    expect(o.series[0].type).toBe('line');
    expect(o.series[0].areaStyle).toBeTruthy();
  });

  it('maps Scatter points to [x,y] pairs on value axes', () => {
    const o = chartToEChartsOption({
      type: 'Scatter', title: '', labels: [],
      series: [{ name: 'p', values: [], points: [{ x: 1, y: 2 }, { x: 3, y: 4 }] }],
    }, false);
    expect(o.series[0].type).toBe('scatter');
    expect(o.xAxis.type).toBe('value');
    expect(o.series[0].data).toEqual([[1, 2, undefined], [3, 4, undefined]]);
  });
});
