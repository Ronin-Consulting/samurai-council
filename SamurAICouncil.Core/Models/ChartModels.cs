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
    /// Data values for this series.
    /// </summary>
    [JsonPropertyName("values")]
    public double[] Values { get; init; } = [];
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
    /// Vertical bar chart for category comparisons.
    /// </summary>
    Bar,

    /// <summary>
    /// Line chart for trends over time.
    /// </summary>
    Line,

    /// <summary>
    /// Pie chart for proportions.
    /// </summary>
    Pie,

    /// <summary>
    /// Donut chart (pie with center hole).
    /// </summary>
    Donut
}
