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

/**
 * Validated dataviz categorical palette (light + dark), fixed order, never cycled.
 * A 9th+ series should be folded into "Other" upstream, not assigned a generated hue.
 */
export const PALETTE_LIGHT = ['#2a78d6', '#1baf7a', '#eda100', '#008300', '#4a3aa7', '#e34948', '#e87ba4', '#eb6834'];
export const PALETTE_DARK = ['#3987e5', '#199e70', '#c98500', '#008300', '#9085e9', '#e66767', '#d55181', '#d95926'];

interface Ink { series: string[]; surface: string; primary: string; muted: string; grid: string; }
function ink(dark: boolean): Ink {
  return dark
    ? { series: PALETTE_DARK, surface: '#1a1a19', primary: '#ffffff', muted: '#898781', grid: '#2c2c2a' }
    : { series: PALETTE_LIGHT, surface: '#fcfcfb', primary: '#0b0b0b', muted: '#898781', grid: '#e1e0d9' };
}

/** Compact large numbers for axis ticks: 1500000000 → "1.5B". */
export function abbreviate(v: number): string {
  const a = Math.abs(v);
  if (a >= 1e9) return (v / 1e9).toFixed(a % 1e9 ? 1 : 0) + 'B';
  if (a >= 1e6) return (v / 1e6).toFixed(a % 1e6 ? 1 : 0) + 'M';
  if (a >= 1e3) return (v / 1e3).toFixed(a % 1e3 ? 1 : 0) + 'K';
  return String(v);
}

/** Average label length heuristic → auto-horizontal for a plain Bar with long labels. */
function longLabels(chart: ChartRecommendation): boolean {
  if (!chart.labels?.length) return false;
  return chart.labels.reduce((s, l) => s + (l?.length ?? 0), 0) / chart.labels.length > 12;
}

/** Map a ChartRecommendation to an Apache ECharts `option` object for ngx-echarts. */
export function chartToEChartsOption(chart: ChartRecommendation, dark: boolean): any {
  const c = ink(dark);
  const t = chart.type;
  const isPie = t === 'Pie' || t === 'Donut';
  const base: any = {
    color: c.series,
    title: chart.title ? { text: chart.title, left: 'center', textStyle: { color: c.primary, fontSize: 14, fontWeight: 600 } } : undefined,
    tooltip: { trigger: isPie || t === 'Scatter' ? 'item' : 'axis' },
    grid: { left: '3%', right: '4%', bottom: chart.series.length > 1 ? 32 : 8, top: chart.title ? 44 : 16, containLabel: true },
    textStyle: { color: c.muted },
    legend: chart.series.length > 1 ? { bottom: 0, textStyle: { color: c.muted } } : undefined,
  };

  if (isPie) {
    const values = chart.series?.[0]?.values ?? [];
    return {
      ...base,
      // Left-align the title so it doesn't collide with the top slice's leader label.
      title: chart.title ? { text: chart.title, left: 'left', top: 0, textStyle: { color: c.primary, fontSize: 14, fontWeight: 600 } } : undefined,
      legend: { bottom: 0, textStyle: { color: c.muted } },
      series: [{
        type: 'pie',
        radius: t === 'Donut' ? ['42%', '66%'] : '64%',
        center: ['50%', '52%'],
        data: chart.labels.map((l, i) => ({ name: l, value: values[i] ?? 0 })),
        label: { color: c.muted },
        itemStyle: { borderColor: c.surface, borderWidth: 2 }, // 2px surface gap between slices
      }],
    };
  }

  if (t === 'Scatter') {
    return {
      ...base,
      xAxis: { type: 'value', name: chart.xAxisLabel ?? undefined, axisLabel: { color: c.muted }, splitLine: { lineStyle: { color: c.grid } } },
      yAxis: { type: 'value', name: chart.yAxisLabel ?? undefined, axisLabel: { color: c.muted }, splitLine: { lineStyle: { color: c.grid } } },
      series: chart.series.map((s) => ({
        name: s.name,
        type: 'scatter',
        symbolSize: 10,
        data: (s.points ?? []).map((p) => [p.x, p.y, p.label]),
        itemStyle: { borderColor: c.surface, borderWidth: 1 },
      })),
    };
  }

  // Category charts: Bar / HorizontalBar / GroupedBar / StackedBar / Line / Area
  const horizontal = t === 'HorizontalBar' || (t === 'Bar' && longLabels(chart));
  const isBar = t === 'Bar' || t === 'HorizontalBar' || t === 'GroupedBar' || t === 'StackedBar';
  const isStacked = t === 'StackedBar';
  const isArea = t === 'Area';
  const barRadius = horizontal ? [0, 4, 4, 0] : [4, 4, 0, 0];

  // The category axis always carries the xAxisLabel; the value axis the yAxisLabel —
  // orientation only moves which screen axis each sits on, not the label's meaning.
  const category: any = {
    type: 'category',
    data: chart.labels,
    // Drop the category-axis name when it would collide: horizontal orientation, or a
    // bottom legend (multi-series). The category tick labels already identify the axis.
    name: horizontal || chart.series.length > 1 ? undefined : (chart.xAxisLabel ?? undefined),
    nameLocation: 'middle',
    nameGap: 30,
    nameTextStyle: { color: c.muted },
    axisLabel: { color: c.muted, rotate: !horizontal && longLabels(chart) ? 30 : 0 },
    axisLine: { lineStyle: { color: c.grid } },
  };
  const value: any = {
    type: 'value',
    name: chart.yAxisLabel ?? undefined,
    nameLocation: 'middle',
    nameGap: horizontal ? 30 : 58,
    nameTextStyle: { color: c.muted },
    axisLabel: { color: c.muted, formatter: (v: number) => abbreviate(v) },
    splitLine: { lineStyle: { color: c.grid } },
  };

  return {
    ...base,
    xAxis: horizontal ? value : category,
    yAxis: horizontal ? category : value,
    series: chart.series.map((s) => ({
      name: s.name,
      type: isBar ? 'bar' : 'line',
      stack: isStacked ? 'total' : undefined,
      smooth: !isBar,
      lineStyle: isBar ? undefined : { width: 2 },
      symbolSize: isBar ? undefined : 8,
      areaStyle: isArea ? { opacity: 0.25 } : undefined,
      itemStyle: isBar ? { borderRadius: isStacked ? 0 : barRadius } : undefined,
      data: s.values,
    })),
  };
}

/**
 * V2 "Studio" ECharts styling — a deliberately distinct, more "productized" look for the
 * pre-made cards: value labels on single-series bars, dashed gridlines, circle legend icons,
 * thicker marks, and a donut center-total. Additive; the Classic `chartToEChartsOption` is untouched.
 */
export function studioChartOption(chart: ChartRecommendation, dark: boolean): any {
  const c = ink(dark);
  const t = chart.type;
  const isPie = t === 'Pie' || t === 'Donut';
  const single = chart.series.length <= 1;
  const base: any = {
    color: single ? [c.series[0]] : c.series, // single series = emphasis (one hue)
    tooltip: { trigger: isPie || t === 'Scatter' ? 'item' : 'axis' },
    grid: { left: '3%', right: '6%', bottom: chart.series.length > 1 ? 28 : 8, top: 12, containLabel: true },
    textStyle: { color: c.muted },
    legend: chart.series.length > 1 ? { bottom: 0, icon: 'circle', textStyle: { color: c.muted } } : undefined,
  };

  if (isPie) {
    const values = chart.series?.[0]?.values ?? [];
    const total = values.reduce((a, b) => a + b, 0);
    return {
      ...base,
      legend: { bottom: 0, icon: 'circle', textStyle: { color: c.muted } },
      graphic: t === 'Donut' && total
        ? [{ type: 'text', left: 'center', top: '42%', style: { text: abbreviate(total), fill: c.primary, font: '600 20px system-ui' } }]
        : undefined,
      series: [{
        type: 'pie',
        radius: t === 'Donut' ? ['52%', '72%'] : '70%',
        center: ['50%', '48%'],
        data: chart.labels.map((l, i) => ({ name: l, value: values[i] ?? 0 })),
        label: { color: c.muted, formatter: '{b}: {d}%' },
        itemStyle: { borderColor: c.surface, borderWidth: 3 },
      }],
    };
  }

  if (t === 'Scatter') {
    return {
      ...base,
      xAxis: { type: 'value', name: chart.xAxisLabel ?? undefined, nameLocation: 'middle', nameGap: 28, axisLabel: { color: c.muted }, splitLine: { lineStyle: { color: c.grid, type: 'dashed' } } },
      yAxis: { type: 'value', name: chart.yAxisLabel ?? undefined, nameLocation: 'middle', nameGap: 40, axisLabel: { color: c.muted }, splitLine: { lineStyle: { color: c.grid, type: 'dashed' } } },
      series: chart.series.map((s) => ({ name: s.name, type: 'scatter', symbolSize: 12, data: (s.points ?? []).map((p) => [p.x, p.y]) })),
    };
  }

  const horizontal = t === 'HorizontalBar' || (t === 'Bar' && longLabels(chart));
  const isBar = t === 'Bar' || t === 'HorizontalBar' || t === 'GroupedBar' || t === 'StackedBar';
  const isStacked = t === 'StackedBar';
  const isArea = t === 'Area';
  const category: any = { type: 'category', data: chart.labels, axisTick: { show: false }, axisLine: { lineStyle: { color: c.grid } }, axisLabel: { color: c.muted, rotate: !horizontal && longLabels(chart) ? 30 : 0 } };
  const value: any = { type: 'value', axisLabel: { color: c.muted, formatter: (v: number) => abbreviate(v) }, splitLine: { lineStyle: { color: c.grid, type: 'dashed' } } };

  return {
    ...base,
    xAxis: horizontal ? value : category,
    yAxis: horizontal ? category : value,
    series: chart.series.map((s) => ({
      name: s.name,
      type: isBar ? 'bar' : 'line',
      stack: isStacked ? 'total' : undefined,
      smooth: !isBar,
      lineStyle: isBar ? undefined : { width: 3 },
      symbol: 'circle',
      symbolSize: isBar ? undefined : 7,
      areaStyle: isArea ? { opacity: 0.2 } : undefined,
      itemStyle: isBar ? { borderRadius: isStacked ? 0 : (horizontal ? [0, 6, 6, 0] : [6, 6, 0, 0]) } : undefined,
      label: isBar && single ? { show: true, position: horizontal ? 'right' : 'top', color: c.muted, formatter: (p: any) => abbreviate(p.value) } : undefined,
      data: s.values,
    })),
  };
}
