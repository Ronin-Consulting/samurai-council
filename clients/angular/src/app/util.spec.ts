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
});
