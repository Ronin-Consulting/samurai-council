using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Rasterizes a chart recommendation to PNG bytes for embedding in exported reports.
/// </summary>
public interface IChartImageRenderer
{
    /// <summary>
    /// Rasterizes <paramref name="chart"/> to PNG bytes at the given pixel size.
    /// Returns null if <paramref name="chart"/>.Type is None/Stat/Table (nothing to draw)
    /// or the chart has no plottable data.
    /// </summary>
    byte[]? RenderToPng(ChartRecommendation chart, int width = 900, int height = 540);
}
