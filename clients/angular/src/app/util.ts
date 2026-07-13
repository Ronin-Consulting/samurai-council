import { ChartRecommendation } from './models';

/** Short, friendly model name from a "provider/model" id (ported from the Blazor helpers). */
export function getShortModelName(model: string): string {
  if (!model) return 'Unknown';
  const parts = model.split('/');
  const m = parts.length > 1 ? parts[parts.length - 1] : model;
  if (m.startsWith('gpt-4o')) return 'GPT-4o';
  if (m.startsWith('gpt-4.1')) return 'GPT-4.1';
  if (m.startsWith('gpt-4')) return 'GPT-4';
  if (m.startsWith('gpt-5')) return 'GPT-5';
  if (m.startsWith('gpt-3.5')) return 'GPT-3.5';
  if (m.startsWith('claude-opus')) return 'Claude Opus';
  if (m.startsWith('claude-sonnet')) return 'Claude Sonnet';
  if (m.startsWith('claude-haiku')) return 'Claude Haiku';
  if (m.startsWith('claude-3-opus')) return 'Claude Opus';
  if (m.startsWith('claude-3-sonnet')) return 'Claude Sonnet';
  if (m.startsWith('gemini-2.5-pro')) return 'Gemini 2.5 Pro';
  if (m.startsWith('gemini-2.5-flash')) return 'Gemini 2.5 Flash';
  if (m.startsWith('gemini-2')) return 'Gemini 2';
  if (m.startsWith('gemini')) return 'Gemini';
  return m.length > 25 ? m.slice(0, 25) + '…' : m;
}

export function getProvider(model: string): string {
  return (model.split('/')[0] || '').toLowerCase();
}

/** Tailwind bg color class per provider for avatars/chips. */
export function getProviderColor(model: string): string {
  switch (getProvider(model)) {
    case 'openai': return 'bg-emerald-600';
    case 'anthropic': return 'bg-orange-600';
    case 'google': return 'bg-blue-600';
    default: return 'bg-neutral-500';
  }
}

export function getProviderInitial(model: string): string {
  const p = getProvider(model);
  return p ? p[0].toUpperCase() : '?';
}

/** Replace anonymous labels ("Response A") in Stage 2 text with **short model names**. */
export function deAnonymize(text: string, labelToModel: Record<string, string> | undefined | null): string {
  if (!text || !labelToModel) return text ?? '';
  let out = text;
  for (const [label, model] of Object.entries(labelToModel)) {
    const re = new RegExp(label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g');
    out = out.replace(re, `**${getShortModelName(model)}**`);
  }
  return out;
}

/** Average label length heuristic → horizontal bar when labels are long (ported from IChartDataTransformer). */
function shouldUseHorizontalBar(chart: ChartRecommendation): boolean {
  if (!chart.labels?.length) return false;
  const avg = chart.labels.reduce((s, l) => s + (l?.length ?? 0), 0) / chart.labels.length;
  return avg > 12;
}

/** Map a ChartRecommendation to an Apache ECharts `option` object for ngx-echarts. */
export function chartToEChartsOption(chart: ChartRecommendation, dark: boolean): any {
  const axisColor = dark ? '#a3a3a3' : '#525252';
  const splitColor = dark ? '#333' : '#e5e5e5';
  const palette = ['#c62828', '#1565c0', '#2e7d32', '#f9a825', '#6a1b9a', '#00838f', '#ef6c00', '#4527a0'];
  const base: any = {
    color: palette,
    title: chart.title ? { text: chart.title, left: 'center', textStyle: { color: dark ? '#f5f5f5' : '#171717', fontSize: 14 } } : undefined,
    tooltip: { trigger: chart.type === 'Pie' || chart.type === 'Donut' ? 'item' : 'axis' },
    grid: { left: '3%', right: '4%', bottom: '3%', top: chart.title ? 48 : 24, containLabel: true },
    textStyle: { color: axisColor },
  };

  if (chart.type === 'Pie' || chart.type === 'Donut') {
    const values = chart.series?.[0]?.values ?? [];
    return {
      ...base,
      legend: { bottom: 0, textStyle: { color: axisColor } },
      series: [{
        type: 'pie',
        radius: chart.type === 'Donut' ? ['40%', '70%'] : '65%',
        center: ['50%', '46%'],
        data: chart.labels.map((l, i) => ({ name: l, value: values[i] ?? 0 })),
        label: { color: axisColor },
      }],
    };
  }

  const horizontal = chart.type === 'Bar' && shouldUseHorizontalBar(chart);
  const category = { type: 'category', data: chart.labels, axisLabel: { color: axisColor, rotate: horizontal ? 0 : (shouldUseHorizontalBar(chart) ? 30 : 0) }, name: chart.xAxisLabel ?? undefined };
  const value = { type: 'value', axisLabel: { color: axisColor }, splitLine: { lineStyle: { color: splitColor } }, name: chart.yAxisLabel ?? undefined };

  return {
    ...base,
    legend: chart.series.length > 1 ? { bottom: 0, textStyle: { color: axisColor } } : undefined,
    xAxis: horizontal ? value : category,
    yAxis: horizontal ? category : value,
    series: chart.series.map((s) => ({
      name: s.name,
      type: chart.type === 'Line' ? 'line' : 'bar',
      smooth: chart.type === 'Line',
      data: s.values,
    })),
  };
}
