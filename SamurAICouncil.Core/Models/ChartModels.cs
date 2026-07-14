using System.Text.Json.Serialization;

namespace SamurAICouncil.Core.Models;

/// <summary>
/// Represents a chart recommendation from the LLM.
/// </summary>
public record ChartRecommendation
{
    /// <summary>
    /// Type of chart to display.
    /// </summary>
    [JsonPropertyName("type")]
    public ChartType Type { get; init; } = ChartType.None;

    /// <summary>
    /// Title to display above the chart.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Labels for the X-axis (bar/line) or segments (pie).
    /// </summary>
    [JsonPropertyName("labels")]
    public string[] Labels { get; init; } = [];

    /// <summary>
    /// Data series to display in the chart.
    /// </summary>
    [JsonPropertyName("series")]
    public ChartSeriesData[] Series { get; init; } = [];

    /// <summary>
    /// Optional label for the X-axis.
    /// </summary>
    [JsonPropertyName("xAxisLabel")]
    public string? XAxisLabel { get; init; }

    /// <summary>
    /// Optional label for the Y-axis.
    /// </summary>
    [JsonPropertyName("yAxisLabel")]
    public string? YAxisLabel { get; init; }

    /// <summary>
    /// Single value / KPI tiles (for <see cref="ChartType.Stat"/>).
    /// </summary>
    [JsonPropertyName("stats")]
    public StatData[]? Stats { get; init; }

    /// <summary>
    /// Tabular result (for <see cref="ChartType.Table"/>).
    /// </summary>
    [JsonPropertyName("table")]
    public TableData? Table { get; init; }
}

/// <summary>
/// Represents a data series in a chart.
/// </summary>
public record ChartSeriesData
{
    /// <summary>
    /// Name of the series (used in legend).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Data values for this series (bar/line/area/pie).
    /// </summary>
    [JsonPropertyName("values")]
    public double[] Values { get; init; } = [];

    /// <summary>
    /// X/Y points for scatter series (used instead of <see cref="Values"/> when <see cref="ChartType.Scatter"/>).
    /// </summary>
    [JsonPropertyName("points")]
    public ChartPoint[]? Points { get; init; }
}

/// <summary>
/// A single (x, y) point for a scatter chart.
/// </summary>
public record ChartPoint
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}

/// <summary>
/// A single headline number / KPI tile.
/// </summary>
public record StatData
{
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; init; }

    /// <summary>Optional unit/prefix (e.g. "$", "%", "units").</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>Optional supporting caption.</summary>
    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    /// <summary>Optional change vs a baseline, in percent (drives an up/down delta).</summary>
    [JsonPropertyName("deltaPercent")]
    public double? DeltaPercent { get; init; }

    /// <summary>Optional trend values for a small sparkline.</summary>
    [JsonPropertyName("sparkline")]
    public double[]? Sparkline { get; init; }
}

/// <summary>
/// A tabular result set for the <see cref="ChartType.Table"/> visual.
/// </summary>
public record TableData
{
    [JsonPropertyName("columns")]
    public string[] Columns { get; init; } = [];

    [JsonPropertyName("rows")]
    public string[][] Rows { get; init; } = [];
}

/// <summary>
/// Types of charts that can be displayed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChartType
{
    /// <summary>
    /// No chart should be displayed.
    /// </summary>
    None,

    /// <summary>
    /// Vertical bar/column chart for category comparisons.
    /// </summary>
    Bar,

    /// <summary>
    /// Horizontal bar chart (for many or long-named categories).
    /// </summary>
    HorizontalBar,

    /// <summary>
    /// Grouped (clustered) bar chart comparing multiple series per category.
    /// </summary>
    GroupedBar,

    /// <summary>
    /// Stacked bar chart for part-to-whole across categories.
    /// </summary>
    StackedBar,

    /// <summary>
    /// Line chart for trends over time.
    /// </summary>
    Line,

    /// <summary>
    /// Area chart for a single trend over time (filled line).
    /// </summary>
    Area,

    /// <summary>
    /// Pie chart for proportions.
    /// </summary>
    Pie,

    /// <summary>
    /// Donut chart (pie with center hole).
    /// </summary>
    Donut,

    /// <summary>
    /// Scatter plot for correlation between two measures.
    /// </summary>
    Scatter,

    /// <summary>
    /// One or a few headline numbers (KPI tiles) — not a plotted chart.
    /// </summary>
    Stat,

    /// <summary>
    /// A data table — for detail listings or more classes than a chart can carry.
    /// </summary>
    Table
}
