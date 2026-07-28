using System.Globalization;
using System.Reflection;
using SamurAICouncil.Core.Models;
using SkiaSharp;

namespace SamurAICouncil.Core.Services.Export;

/// <summary>
/// Rasterizes a <see cref="ChartRecommendation"/> to PNG bytes using hand-drawn SkiaSharp 2D
/// primitives (no third-party charting library). Used to embed chart images into the Word/PDF
/// exports, where an interactive ECharts-style renderer isn't available.
/// </summary>
public class ChartImageRenderer : IChartImageRenderer
{
    // Layout: reserve space at the top for the title, at the bottom for x-axis / category labels,
    // and on the left for y-axis / category labels (used by HorizontalBar for its category column).
    private const float TitleAreaHeight = 40f;
    private const float BottomAxisHeight = 30f;
    private const float LeftAxisWidth = 50f;
    // Wide enough to fit a long formatted value label (e.g. "1,234,567,890") drawn left-aligned
    // just past the last data point on a Line/Area chart, plus half of a long last-category label
    // (e.g. "December") that's center-anchored right at the plot's right edge - both would otherwise
    // clip against the canvas boundary.
    private const float RightMargin = 100f;

    private const int MaxSeriesColors = 8;
    private const float MarkerRadius = 5f; // diameter 10px, satisfies the "≥8px" marker spec.
    private const float BarCornerRadius = 4f;

    private static readonly SKColor[] SeriesColors = ReportPalette.ChartSeriesHex.Select(ParseHex).ToArray();
    private static readonly SKColor SurfaceColor = ParseHex(ReportPalette.ChartSurfaceHex);
    private static readonly SKColor GridColor = ParseHex(ReportPalette.ChartGridHex);
    private static readonly SKColor PrimaryTextColor = ParseHex(ReportPalette.ChartPrimaryTextHex);
    private static readonly SKColor MutedTextColor = ParseHex(ReportPalette.ChartMutedTextHex);

    private static readonly Lazy<SKTypeface> RegularTypefaceLazy = new(() => LoadEmbeddedTypeface(bold: false));
    private static readonly Lazy<SKTypeface> BoldTypefaceLazy = new(() => LoadEmbeddedTypeface(bold: true));

    private static SKTypeface RegularTypeface => RegularTypefaceLazy.Value;
    private static SKTypeface BoldTypeface => BoldTypefaceLazy.Value;

    private enum BarMode { Simple, Grouped, Stacked }

    private enum BarCorner { None, Top, Bottom, Left, Right }

    /// <inheritdoc />
    public byte[]? RenderToPng(ChartRecommendation chart, int width = 900, int height = 540)
    {
        if (chart is null || width <= 0 || height <= 0)
        {
            return null;
        }

        if (!HasPlottableData(chart))
        {
            return null;
        }

        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SurfaceColor);

            var plotRect = new SKRect(LeftAxisWidth, TitleAreaHeight, width - RightMargin, height - BottomAxisHeight);
            if (plotRect.Width < 10 || plotRect.Height < 10)
            {
                // Degenerate/very small requested size: fall back to using the whole canvas so we
                // still produce *something* rather than throwing.
                plotRect = new SKRect(2, 2, width - 2, height - 2);
            }

            DrawTitle(canvas, chart.Title);

            switch (chart.Type)
            {
                case ChartType.Bar:
                    DrawBarFamily(canvas, chart, plotRect, BarMode.Simple, horizontal: false);
                    break;
                case ChartType.HorizontalBar:
                    DrawBarFamily(canvas, chart, plotRect, BarMode.Simple, horizontal: true);
                    break;
                case ChartType.GroupedBar:
                    DrawBarFamily(canvas, chart, plotRect, BarMode.Grouped, horizontal: false);
                    break;
                case ChartType.StackedBar:
                    DrawBarFamily(canvas, chart, plotRect, BarMode.Stacked, horizontal: false);
                    break;
                case ChartType.Line:
                    DrawLineOrArea(canvas, chart, plotRect, filled: false);
                    break;
                case ChartType.Area:
                    DrawLineOrArea(canvas, chart, plotRect, filled: true);
                    break;
                case ChartType.Pie:
                    DrawPieOrDonut(canvas, chart, plotRect, donut: false);
                    break;
                case ChartType.Donut:
                    DrawPieOrDonut(canvas, chart, plotRect, donut: true);
                    break;
                case ChartType.Scatter:
                    DrawScatter(canvas, chart, plotRect);
                    break;
                default:
                    // None/Stat/Table are filtered out by HasPlottableData already.
                    return null;
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static bool HasPlottableData(ChartRecommendation chart)
    {
        if (chart.Type is ChartType.None or ChartType.Stat or ChartType.Table)
        {
            return false;
        }

        if (chart.Series is null || chart.Series.Length == 0)
        {
            return false;
        }

        if (chart.Type == ChartType.Scatter)
        {
            return chart.Series.Any(s => s.Points is { Length: > 0 });
        }

        return chart.Series.Any(s => s.Values is { Length: > 0 });
    }

    // ---- Bar family (Bar / HorizontalBar / GroupedBar / StackedBar) ----------------------------

    private static void DrawBarFamily(SKCanvas canvas, ChartRecommendation chart, SKRect plotRect, BarMode mode, bool horizontal)
    {
        if (mode == BarMode.Simple)
        {
            var series = chart.Series.FirstOrDefault(s => s.Values is { Length: > 0 });
            if (series is null)
            {
                return;
            }

            var categories = BuildCategoryLabels(chart.Labels, series.Values.Length);
            DrawSimpleBars(canvas, plotRect, categories, series.Values, horizontal);
            return;
        }

        var seriesList = CapSeries(chart.Series, MaxSeriesColors);
        if (seriesList.Count == 0)
        {
            return;
        }

        var categoryCount = seriesList.Max(s => s.Values.Length);
        if (categoryCount == 0)
        {
            return;
        }

        var cats = BuildCategoryLabels(chart.Labels, categoryCount);

        if (mode == BarMode.Grouped)
        {
            DrawGroupedBars(canvas, plotRect, cats, seriesList);
        }
        else
        {
            DrawStackedBars(canvas, plotRect, cats, seriesList);
        }
    }

    private static void DrawSimpleBars(SKCanvas canvas, SKRect plotRect, string[] categories, double[] values, bool horizontal)
    {
        var n = values.Length;
        if (n == 0)
        {
            return;
        }

        var maxVal = Math.Max(values.Max(), 0);
        var minVal = Math.Min(values.Min(), 0);
        var range = maxVal - minVal;
        if (range <= 0)
        {
            range = 1;
        }

        var color = SeriesColors[0];

        if (!horizontal)
        {
            DrawHorizontalGridlines(canvas, plotRect);

            const float labelHeadroom = 16f;
            var usableTop = plotRect.Top + labelHeadroom;
            var usableBottom = plotRect.Bottom;
            var usableHeight = usableBottom - usableTop;
            var zeroY = usableBottom - (float)((0 - minVal) / range) * usableHeight;

            var slot = plotRect.Width / n;
            var barWidth = Math.Clamp(slot - 4f, 2f, 24f);

            for (var i = 0; i < n; i++)
            {
                var v = values[i];
                var xCenter = plotRect.Left + slot * i + slot / 2f;
                var barExtent = (float)(Math.Abs(v) / range) * usableHeight;
                var top = v >= 0 ? zeroY - barExtent : zeroY;
                var bottom = v >= 0 ? zeroY : zeroY + barExtent;
                var rect = new SKRect(xCenter - barWidth / 2f, top, xCenter + barWidth / 2f, bottom);
                DrawBarRect(canvas, rect, color, v >= 0 ? BarCorner.Top : BarCorner.Bottom);

                DrawText(canvas, FormatValue(v), xCenter, v >= 0 ? top - 4f : bottom + 12f, SKTextAlign.Center, MutedTextColor, RegularTypeface, 11f);
                DrawText(canvas, TruncateForAxis(categories[i]), xCenter, plotRect.Bottom + 16f, SKTextAlign.Center, MutedTextColor, RegularTypeface, 11f);
            }
        }
        else
        {
            DrawVerticalGridlines(canvas, plotRect);

            var usableLeft = plotRect.Left;
            var usableRight = plotRect.Right - 8f;
            var usableWidth = usableRight - usableLeft;
            var zeroX = usableLeft + (float)((0 - minVal) / range) * usableWidth;

            var slot = plotRect.Height / n;
            var barHeight = Math.Clamp(slot - 4f, 2f, 24f);

            for (var i = 0; i < n; i++)
            {
                var v = values[i];
                var yCenter = plotRect.Top + slot * i + slot / 2f;
                var barExtent = (float)(Math.Abs(v) / range) * usableWidth;
                var left = v >= 0 ? zeroX : zeroX - barExtent;
                var right = v >= 0 ? zeroX + barExtent : zeroX;
                var rect = new SKRect(left, yCenter - barHeight / 2f, right, yCenter + barHeight / 2f);
                DrawBarRect(canvas, rect, color, v >= 0 ? BarCorner.Right : BarCorner.Left);

                DrawText(canvas, FormatValue(v), v >= 0 ? right + 4f : left - 4f, yCenter + 4f, v >= 0 ? SKTextAlign.Left : SKTextAlign.Right, MutedTextColor, RegularTypeface, 11f);
                DrawText(canvas, TruncateForAxis(categories[i]), plotRect.Left - 6f, yCenter + 4f, SKTextAlign.Right, MutedTextColor, RegularTypeface, 11f);
            }
        }
    }

    private static void DrawGroupedBars(SKCanvas canvas, SKRect plotRect, string[] categories, List<(string Name, double[] Values, SKColor Color)> seriesList)
    {
        var n = categories.Length;
        if (n == 0)
        {
            return;
        }

        double maxVal = 0;
        double minVal = 0;
        foreach (var s in seriesList)
        {
            foreach (var v in s.Values)
            {
                maxVal = Math.Max(maxVal, v);
                minVal = Math.Min(minVal, v);
            }
        }

        var range = maxVal - minVal;
        if (range <= 0)
        {
            range = 1;
        }

        DrawHorizontalGridlines(canvas, plotRect);

        const float labelHeadroom = 16f;
        var usableTop = plotRect.Top + labelHeadroom;
        var usableBottom = plotRect.Bottom;
        var usableHeight = usableBottom - usableTop;
        var zeroY = usableBottom - (float)((0 - minVal) / range) * usableHeight;

        var seriesCount = seriesList.Count;
        var bandWidth = plotRect.Width / n;
        var groupPadding = bandWidth * 0.15f;
        var availableGroupWidth = Math.Max(bandWidth - groupPadding * 2f, 4f);
        var barWidth = Math.Min(24f, availableGroupWidth / seriesCount - 2f);
        if (barWidth < 1f)
        {
            barWidth = Math.Max(1f, availableGroupWidth / seriesCount - 1f);
        }

        for (var c = 0; c < n; c++)
        {
            var bandLeft = plotRect.Left + bandWidth * c + groupPadding;
            for (var s = 0; s < seriesCount; s++)
            {
                var values = seriesList[s].Values;
                if (c >= values.Length)
                {
                    continue;
                }

                var v = values[c];
                var xCenter = bandLeft + (barWidth + 2f) * s + barWidth / 2f;
                var barExtent = (float)(Math.Abs(v) / range) * usableHeight;
                var top = v >= 0 ? zeroY - barExtent : zeroY;
                var bottom = v >= 0 ? zeroY : zeroY + barExtent;
                var rect = new SKRect(xCenter - barWidth / 2f, top, xCenter + barWidth / 2f, bottom);
                DrawBarRect(canvas, rect, seriesList[s].Color, v >= 0 ? BarCorner.Top : BarCorner.Bottom);
            }

            var catCenter = plotRect.Left + bandWidth * c + bandWidth / 2f;
            DrawText(canvas, TruncateForAxis(categories[c]), catCenter, plotRect.Bottom + 16f, SKTextAlign.Center, MutedTextColor, RegularTypeface, 11f);
        }

        if (seriesCount >= 2)
        {
            DrawLegend(canvas, plotRect, seriesList.Select(s => (s.Name, s.Color)));
        }
    }

    private static void DrawStackedBars(SKCanvas canvas, SKRect plotRect, string[] categories, List<(string Name, double[] Values, SKColor Color)> seriesList)
    {
        var n = categories.Length;
        if (n == 0)
        {
            return;
        }

        var totals = new double[n];
        for (var c = 0; c < n; c++)
        {
            foreach (var s in seriesList)
            {
                if (c < s.Values.Length)
                {
                    totals[c] += Math.Max(s.Values[c], 0);
                }
            }
        }

        var maxTotal = totals.DefaultIfEmpty(0).Max();
        if (maxTotal <= 0)
        {
            maxTotal = 1;
        }

        DrawHorizontalGridlines(canvas, plotRect);

        const float labelHeadroom = 16f;
        var usableTop = plotRect.Top + labelHeadroom;
        var usableBottom = plotRect.Bottom;
        var usableHeight = usableBottom - usableTop;

        var slot = plotRect.Width / n;
        var barWidth = Math.Clamp(slot - 4f, 2f, 24f);

        for (var c = 0; c < n; c++)
        {
            var segments = new List<(double Value, SKColor Color)>();
            foreach (var s in seriesList)
            {
                if (c < s.Values.Length && s.Values[c] > 0)
                {
                    segments.Add((s.Values[c], s.Color));
                }
            }

            var xCenter = plotRect.Left + slot * c + slot / 2f;
            var cursor = usableBottom;

            for (var j = 0; j < segments.Count; j++)
            {
                var segHeight = (float)(segments[j].Value / maxTotal) * usableHeight;
                var top = cursor - segHeight;
                var rect = new SKRect(xCenter - barWidth / 2f, top, xCenter + barWidth / 2f, cursor);
                DrawBarRect(canvas, rect, segments[j].Color, j == segments.Count - 1 ? BarCorner.Top : BarCorner.None);

                if (j < segments.Count - 1)
                {
                    // 2px surface-color gap between stacked segments (no stroke on the bars themselves).
                    using var gapPaint = new SKPaint { Color = SurfaceColor, StrokeWidth = 2f, IsAntialias = false };
                    canvas.DrawLine(xCenter - barWidth / 2f, top, xCenter + barWidth / 2f, top, gapPaint);
                }

                cursor = top;
            }

            DrawText(canvas, TruncateForAxis(categories[c]), xCenter, plotRect.Bottom + 16f, SKTextAlign.Center, MutedTextColor, RegularTypeface, 11f);
        }

        if (seriesList.Count >= 2)
        {
            DrawLegend(canvas, plotRect, seriesList.Select(s => (s.Name, s.Color)));
        }
    }

    private static void DrawBarRect(SKCanvas canvas, SKRect rect, SKColor color, BarCorner corner)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var tl = SKPoint.Empty;
        var tr = SKPoint.Empty;
        var br = SKPoint.Empty;
        var bl = SKPoint.Empty;

        switch (corner)
        {
            case BarCorner.Top:
                tl = tr = new SKPoint(BarCornerRadius, BarCornerRadius);
                break;
            case BarCorner.Bottom:
                bl = br = new SKPoint(BarCornerRadius, BarCornerRadius);
                break;
            case BarCorner.Right:
                tr = br = new SKPoint(BarCornerRadius, BarCornerRadius);
                break;
            case BarCorner.Left:
                tl = bl = new SKPoint(BarCornerRadius, BarCornerRadius);
                break;
        }

        using var roundRect = new SKRoundRect();
        roundRect.SetRectRadii(rect, [tl, tr, br, bl]);

        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(roundRect, paint);
    }

    // ---- Line / Area -----------------------------------------------------------------------------

    private static void DrawLineOrArea(SKCanvas canvas, ChartRecommendation chart, SKRect plotRect, bool filled)
    {
        var seriesList = CapSeries(chart.Series, MaxSeriesColors);
        if (seriesList.Count == 0)
        {
            return;
        }

        var categoryCount = seriesList.Max(s => s.Values.Length);
        if (categoryCount == 0)
        {
            return;
        }

        var categories = BuildCategoryLabels(chart.Labels, categoryCount);

        var maxVal = Math.Max(seriesList.SelectMany(s => s.Values).DefaultIfEmpty(0).Max(), 0);
        var minVal = Math.Min(seriesList.SelectMany(s => s.Values).DefaultIfEmpty(0).Min(), 0);
        var range = maxVal - minVal;
        if (range <= 0)
        {
            range = 1;
        }

        DrawHorizontalGridlines(canvas, plotRect);

        var usableTop = plotRect.Top + 10f;
        var usableBottom = plotRect.Bottom;
        var usableHeight = usableBottom - usableTop;
        var xStep = categoryCount > 1 ? plotRect.Width / (categoryCount - 1) : 0f;

        float ValueToY(double v) => usableBottom - (float)((v - minVal) / range) * usableHeight;
        float IndexToX(int i) => categoryCount > 1 ? plotRect.Left + xStep * i : plotRect.MidX;

        foreach (var series in seriesList)
        {
            if (series.Values.Length == 0)
            {
                continue;
            }

            var points = new SKPoint[series.Values.Length];
            for (var i = 0; i < series.Values.Length; i++)
            {
                points[i] = new SKPoint(IndexToX(i), ValueToY(series.Values[i]));
            }

            if (filled)
            {
                var fillBuilder = new SKPathBuilder();
                fillBuilder.MoveTo(points[0].X, usableBottom);
                foreach (var p in points)
                {
                    fillBuilder.LineTo(p);
                }

                fillBuilder.LineTo(points[^1].X, usableBottom);
                fillBuilder.Close();
                using var fillPath = fillBuilder.Detach();

                using var fillPaint = new SKPaint { Color = series.Color.WithAlpha((byte)Math.Round(255 * 0.10)), IsAntialias = true, Style = SKPaintStyle.Fill };
                canvas.DrawPath(fillPath, fillPaint);
            }

            if (points.Length >= 2)
            {
                var lineBuilder = new SKPathBuilder();
                lineBuilder.MoveTo(points[0]);
                for (var i = 1; i < points.Length; i++)
                {
                    lineBuilder.LineTo(points[i]);
                }

                using var linePath = lineBuilder.Detach();

                using var linePaint = new SKPaint
                {
                    Color = series.Color,
                    StrokeWidth = 2f,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeJoin = SKStrokeJoin.Round,
                    StrokeCap = SKStrokeCap.Round,
                };
                canvas.DrawPath(linePath, linePaint);
            }

            foreach (var p in points)
            {
                DrawMarker(canvas, p, series.Color);
            }

            var last = points[^1];
            DrawText(canvas, FormatValue(series.Values[^1]), last.X + 6f, last.Y - 8f, SKTextAlign.Left, MutedTextColor, RegularTypeface, 11f);
        }

        for (var i = 0; i < categoryCount; i++)
        {
            DrawText(canvas, TruncateForAxis(categories[i]), IndexToX(i), plotRect.Bottom + 16f, SKTextAlign.Center, MutedTextColor, RegularTypeface, 11f);
        }
    }

    private static void DrawMarker(SKCanvas canvas, SKPoint center, SKColor color)
    {
        using var ringPaint = new SKPaint { Color = SurfaceColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(center, MarkerRadius + 2f, ringPaint);

        using var fillPaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(center, MarkerRadius, fillPaint);
    }

    // ---- Pie / Donut -----------------------------------------------------------------------------

    private static void DrawPieOrDonut(SKCanvas canvas, ChartRecommendation chart, SKRect plotRect, bool donut)
    {
        var primary = chart.Series.FirstOrDefault(s => s.Values is { Length: > 0 });
        if (primary is null)
        {
            return;
        }

        var segments = CapSegments(chart.Labels, primary.Values, MaxSeriesColors);
        var total = segments.Sum(s => s.Value);
        if (total <= 0)
        {
            return;
        }

        const float legendWidth = 150f;
        var chartArea = new SKRect(plotRect.Left, plotRect.Top, Math.Max(plotRect.Left + 10, plotRect.Right - legendWidth), plotRect.Bottom);
        if (chartArea.Width < 40)
        {
            // Not enough room to split off a legend column; use the full plot area for the circle.
            chartArea = plotRect;
        }

        var cx = chartArea.MidX;
        var cy = chartArea.MidY;
        var outerRadius = Math.Min(chartArea.Width, chartArea.Height) / 2f - 4f;
        outerRadius = Math.Max(outerRadius, 4f);
        var innerRadius = donut ? outerRadius * 0.58f : 0f;

        var startAngle = -90f;
        var legendItems = new List<(string Label, SKColor Color)>();

        using var gapPaint = new SKPaint { Color = SurfaceColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };

        foreach (var seg in segments)
        {
            var sweep = (float)(seg.Value / total * 360.0);
            if (sweep <= 0)
            {
                continue;
            }

            var pathBuilder = new SKPathBuilder();
            var oval = new SKRect(cx - outerRadius, cy - outerRadius, cx + outerRadius, cy + outerRadius);

            if (donut)
            {
                var innerOval = new SKRect(cx - innerRadius, cy - innerRadius, cx + innerRadius, cy + innerRadius);
                pathBuilder.ArcTo(oval, startAngle, sweep, forceMoveTo: true);
                var innerEnd = AngleToPoint(cx, cy, innerRadius, startAngle + sweep);
                pathBuilder.LineTo(innerEnd);
                pathBuilder.ArcTo(innerOval, startAngle + sweep, -sweep, forceMoveTo: false);
                pathBuilder.Close();
            }
            else
            {
                pathBuilder.MoveTo(cx, cy);
                pathBuilder.ArcTo(oval, startAngle, sweep, forceMoveTo: false);
                pathBuilder.Close();
            }

            using var path = pathBuilder.Detach();

            using var paint = new SKPaint { Color = seg.Color, IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawPath(path, paint);
            canvas.DrawPath(path, gapPaint);

            var pct = seg.Value / total * 100.0;
            legendItems.Add(($"{TruncateForAxis(seg.Label)} {pct.ToString("0.#", CultureInfo.InvariantCulture)}%", seg.Color));

            startAngle += sweep;
        }

        DrawLegend(canvas, plotRect, legendItems);
    }

    private static SKPoint AngleToPoint(float cx, float cy, float radius, float angleDegrees)
    {
        var rad = angleDegrees * (MathF.PI / 180f);
        return new SKPoint(cx + radius * MathF.Cos(rad), cy + radius * MathF.Sin(rad));
    }

    // ---- Scatter ---------------------------------------------------------------------------------

    private static void DrawScatter(SKCanvas canvas, ChartRecommendation chart, SKRect plotRect)
    {
        var seriesWithPoints = chart.Series.Where(s => s.Points is { Length: > 0 }).ToList();
        if (seriesWithPoints.Count == 0)
        {
            return;
        }

        // Never cycle the palette: beyond 8 series we simply stop plotting further series rather
        // than reusing colors (scatter points can't be meaningfully folded into an "Other" series).
        var capped = seriesWithPoints.Take(MaxSeriesColors).ToList();
        var allPoints = capped.SelectMany(s => s.Points!).ToList();

        var minX = allPoints.Min(p => p.X);
        var maxX = allPoints.Max(p => p.X);
        var minY = allPoints.Min(p => p.Y);
        var maxY = allPoints.Max(p => p.Y);

        var rangeX = maxX - minX;
        if (rangeX <= 0)
        {
            rangeX = Math.Max(Math.Abs(maxX), 1);
        }

        var rangeY = maxY - minY;
        if (rangeY <= 0)
        {
            rangeY = Math.Max(Math.Abs(maxY), 1);
        }

        var padX = rangeX * 0.1;
        var padY = rangeY * 0.1;
        minX -= padX;
        maxX += padX;
        minY -= padY;
        maxY += padY;
        rangeX = maxX - minX;
        rangeY = maxY - minY;
        if (rangeX <= 0)
        {
            rangeX = 1;
        }

        if (rangeY <= 0)
        {
            rangeY = 1;
        }

        DrawHorizontalGridlines(canvas, plotRect);
        DrawVerticalGridlines(canvas, plotRect);

        var usableTop = plotRect.Top + 6f;
        var usableBottom = plotRect.Bottom;
        var usableLeft = plotRect.Left;
        var usableRight = plotRect.Right;

        SKPoint ToScreen(ChartPoint p)
        {
            var x = usableLeft + (float)((p.X - minX) / rangeX) * (usableRight - usableLeft);
            var y = usableBottom - (float)((p.Y - minY) / rangeY) * (usableBottom - usableTop);
            return new SKPoint(x, y);
        }

        for (var i = 0; i < capped.Count; i++)
        {
            var color = SeriesColors[i % SeriesColors.Length];
            foreach (var pt in capped[i].Points!)
            {
                DrawMarker(canvas, ToScreen(pt), color);
            }
        }

        if (capped.Count >= 2)
        {
            var items = capped.Select((s, i) => (
                Label: string.IsNullOrEmpty(s.Name) ? $"Series {i + 1}" : s.Name,
                Color: SeriesColors[i % SeriesColors.Length]));
            DrawLegend(canvas, plotRect, items);
        }
    }

    // ---- Shared drawing helpers -------------------------------------------------------------------

    private static void DrawTitle(SKCanvas canvas, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var display = title.Length > 90 ? title[..87] + "…" : title;
        DrawText(canvas, display, LeftAxisWidth, 26f, SKTextAlign.Left, PrimaryTextColor, BoldTypeface, 18f);
    }

    private static void DrawHorizontalGridlines(SKCanvas canvas, SKRect plotRect, int lines = 4)
    {
        using var paint = new SKPaint { Color = GridColor, StrokeWidth = 1f, IsAntialias = false };
        for (var i = 1; i < lines; i++)
        {
            var y = plotRect.Top + plotRect.Height * i / lines;
            canvas.DrawLine(plotRect.Left, y, plotRect.Right, y, paint);
        }

        using var basePaint = new SKPaint { Color = MutedTextColor, StrokeWidth = 1f, IsAntialias = false };
        canvas.DrawLine(plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom, basePaint);
    }

    private static void DrawVerticalGridlines(SKCanvas canvas, SKRect plotRect, int lines = 4)
    {
        using var paint = new SKPaint { Color = GridColor, StrokeWidth = 1f, IsAntialias = false };
        for (var i = 1; i < lines; i++)
        {
            var x = plotRect.Left + plotRect.Width * i / lines;
            canvas.DrawLine(x, plotRect.Top, x, plotRect.Bottom, paint);
        }

        using var basePaint = new SKPaint { Color = MutedTextColor, StrokeWidth = 1f, IsAntialias = false };
        canvas.DrawLine(plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom, basePaint);
    }

    private static void DrawLegend(SKCanvas canvas, SKRect plotRect, IEnumerable<(string Label, SKColor Color)> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            return;
        }

        const float swatch = 8f;
        const float rowHeight = 14f;
        const float padding = 6f;

        using var font = new SKFont(RegularTypeface, 10f);
        using var textPaint = new SKPaint { Color = MutedTextColor, IsAntialias = true };

        var maxTextWidth = list.Max(i => font.MeasureText(i.Label));
        var boxWidth = swatch + 4f + maxTextWidth + padding * 2;
        var boxHeight = list.Count * rowHeight + padding * 2;
        var x0 = plotRect.Right - boxWidth - 4f;
        var y0 = plotRect.Top + 4f;

        using var bgPaint = new SKPaint { Color = SurfaceColor.WithAlpha(235), IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawRect(new SKRect(x0, y0, x0 + boxWidth, y0 + boxHeight), bgPaint);

        for (var i = 0; i < list.Count; i++)
        {
            var rowY = y0 + padding + i * rowHeight;
            using var swatchPaint = new SKPaint { Color = list[i].Color, IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawRect(new SKRect(x0 + padding, rowY + 2f, x0 + padding + swatch, rowY + 2f + swatch), swatchPaint);
            canvas.DrawText(list[i].Label, x0 + padding + swatch + 4f, rowY + swatch, SKTextAlign.Left, font, textPaint);
        }
    }

    private static void DrawText(SKCanvas canvas, string? text, float x, float y, SKTextAlign align, SKColor color, SKTypeface typeface, float size)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(text, x, y, align, font, paint);
    }

    private static string[] BuildCategoryLabels(string[] labels, int count)
    {
        if (labels.Length == count)
        {
            return labels;
        }

        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = i < labels.Length ? labels[i] : (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        return result;
    }

    /// <summary>
    /// Caps the number of plotted series at <paramref name="max"/>, folding any extras beyond that
    /// into a synthetic "Other" series (element-wise sum), so the fixed 8-color palette never cycles.
    /// </summary>
    private static List<(string Name, double[] Values, SKColor Color)> CapSeries(ChartSeriesData[] series, int max)
    {
        var nonEmpty = series.Where(s => s.Values is { Length: > 0 }).ToList();
        if (nonEmpty.Count == 0)
        {
            return [];
        }

        if (nonEmpty.Count <= max)
        {
            return nonEmpty
                .Select((s, i) => (string.IsNullOrEmpty(s.Name) ? $"Series {i + 1}" : s.Name, s.Values, SeriesColors[i]))
                .ToList();
        }

        var kept = nonEmpty.Take(max - 1).ToList();
        var extra = nonEmpty.Skip(max - 1).ToList();
        var maxLen = nonEmpty.Max(s => s.Values.Length);
        var otherValues = new double[maxLen];
        foreach (var s in extra)
        {
            for (var i = 0; i < s.Values.Length; i++)
            {
                otherValues[i] += s.Values[i];
            }
        }

        var result = kept
            .Select((s, i) => (string.IsNullOrEmpty(s.Name) ? $"Series {i + 1}" : s.Name, s.Values, SeriesColors[i]))
            .ToList();
        result.Add(("Other", otherValues, SeriesColors[max - 1]));
        return result;
    }

    /// <summary>
    /// Caps the number of pie/donut segments at <paramref name="max"/>, folding extras into a
    /// synthetic "Other" segment. Negative values are clamped to zero (validated upstream too, but
    /// the renderer must never produce a negative sweep angle).
    /// </summary>
    private static List<(string Label, double Value, SKColor Color)> CapSegments(string[] labels, double[] rawValues, int max)
    {
        var clipped = rawValues.Select(v => Math.Max(v, 0)).ToArray();
        var labelsFull = BuildCategoryLabels(labels, clipped.Length);

        var pairs = new List<(string Label, double Value)>();
        for (var i = 0; i < clipped.Length; i++)
        {
            if (clipped[i] > 0)
            {
                pairs.Add((labelsFull[i], clipped[i]));
            }
        }

        if (pairs.Count == 0)
        {
            return [];
        }

        if (pairs.Count <= max)
        {
            return pairs.Select((p, i) => (p.Label, p.Value, SeriesColors[i])).ToList();
        }

        var kept = pairs.Take(max - 1).ToList();
        var otherSum = pairs.Skip(max - 1).Sum(p => p.Value);
        var result = kept.Select((p, i) => (p.Label, p.Value, SeriesColors[i])).ToList();
        result.Add(("Other", otherSum, SeriesColors[max - 1]));
        return result;
    }

    private static string TruncateForAxis(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return label ?? string.Empty;
        }

        return label.Length > 18 ? label[..17] + "…" : label;
    }

    private static string FormatValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "0";
        }

        if (Math.Abs(value) >= 1000)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        return Math.Abs(value % 1) < 0.001
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static SKColor ParseHex(string hex) => SKColor.Parse("#" + hex);

    private static SKTypeface LoadEmbeddedTypeface(bool bold)
    {
        var resourceName = bold
            ? "SamurAICouncil.Core.Resources.Fonts.DejaVuSans-Bold.ttf"
            : "SamurAICouncil.Core.Resources.Fonts.DejaVuSans.ttf";

        var assembly = typeof(ChartImageRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
        {
            var typeface = SKTypeface.FromStream(stream);
            if (typeface is not null)
            {
                return typeface;
            }
        }

        // TODO: this should never happen in a correctly-built package — DejaVu Sans (Regular +
        // Bold) is embedded as a resource (see SamurAICouncil.Core.csproj / Resources/Fonts).
        // Falling back to the platform default keeps rendering from throwing if that ever
        // changes, at the cost of losing the guarantee that a font is available in a
        // minimal/fontconfig-less Linux container.
        return SKTypeface.Default;
    }
}
