using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Transforms data and LLM responses into chart recommendations.
/// </summary>
public partial class ChartDataTransformer : IChartDataTransformer
{
    private readonly ILogger<ChartDataTransformer> _logger;
    private const int MaxDataPoints = 20;
    private const int MaxLabelLength = 15;  // Reduced from 20 to prevent clipping with rotated labels
    private const int MaxBarChartCategories = 15;
    private const int MaxPieChartSlices = 6;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChartDataTransformer(ILogger<ChartDataTransformer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ChartRecommendation? ParseChartRecommendation(string llmChartJson)
    {
        if (string.IsNullOrWhiteSpace(llmChartJson))
        {
            return null;
        }

        try
        {
            // Try to extract JSON from markdown code blocks if present
            var jsonContent = ExtractJsonFromResponse(llmChartJson);

            // Try parsing as a wrapper object with "chart" property
            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("chart", out var chartElement))
            {
                var chart = JsonSerializer.Deserialize<ChartRecommendation>(
                    chartElement.GetRawText(), JsonOptions);
                return ValidateAndTruncate(chart);
            }

            // Try parsing directly as ChartRecommendation
            var directChart = JsonSerializer.Deserialize<ChartRecommendation>(jsonContent, JsonOptions);
            return ValidateAndTruncate(directChart);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse chart recommendation JSON");
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsValidChart(ChartRecommendation? chart)
    {
        if (chart == null || chart.Type == ChartType.None)
        {
            return false;
        }

        // Non-chart visuals: validate their own payloads.
        if (chart.Type == ChartType.Stat)
        {
            if (chart.Stats is not { Length: > 0 })
            {
                _logger.LogWarning("Stat rejected: no stats");
                return false;
            }
            return true;
        }

        if (chart.Type == ChartType.Table)
        {
            if (chart.Table is null || chart.Table.Columns.Length == 0 || chart.Table.Rows.Length == 0)
            {
                _logger.LogWarning("Table rejected: no columns/rows");
                return false;
            }
            return true;
        }

        // Scatter uses (x,y) points rather than value arrays.
        if (chart.Type == ChartType.Scatter)
        {
            var hasPoints = chart.Series.Any(s => s.Points is { Length: >= 2 });
            if (!hasPoints) _logger.LogWarning("Scatter rejected: needs >= 2 points");
            return hasPoints;
        }

        // Must have at least one series with data
        if (chart.Series.Length == 0)
        {
            _logger.LogWarning("Chart rejected: No series data");
            return false;
        }

        // All series must have values
        if (chart.Series.Any(s => s.Values.Length == 0))
        {
            _logger.LogWarning("Chart rejected: Series has no values");
            return false;
        }

        // Single data point - not worth charting
        if (chart.Series.All(s => s.Values.Length == 1))
        {
            _logger.LogWarning("Chart rejected: Single data point, not worth charting");
            return false;
        }

        // All-zero values - nothing to visualize
        if (chart.Series.All(s => s.Values.All(v => v == 0)))
        {
            _logger.LogWarning("Chart rejected: All values are zero");
            return false;
        }

        // Pie charts cannot have negative values
        if (chart.Type is ChartType.Pie or ChartType.Donut)
        {
            if (chart.Series.Any(s => s.Values.Any(v => v < 0)))
            {
                _logger.LogWarning("Chart rejected: Pie/Donut charts cannot have negative values");
                return false;
            }
        }

        // For bar/line charts, labels should match value count
        if (chart.Type is ChartType.Bar or ChartType.Line)
        {
            var firstSeriesLength = chart.Series[0].Values.Length;
            if (chart.Labels.Length > 0 && chart.Labels.Length != firstSeriesLength)
            {
                _logger.LogWarning(
                    "Chart labels count ({LabelCount}) doesn't match values count ({ValueCount})",
                    chart.Labels.Length, firstSeriesLength);
                // Still valid, we can work with it
            }
        }

        return true;
    }

    /// <summary>
    /// Extracts JSON content from markdown code blocks or returns raw content.
    /// </summary>
    private static string ExtractJsonFromResponse(string response)
    {
        // Try to find JSON in code block
        var match = JsonCodeBlockRegex().Match(response);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Try to find any JSON object
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            return response[jsonStart..(jsonEnd + 1)];
        }

        return response;
    }

    /// <summary>
    /// Validates and truncates chart data to prevent performance and readability issues.
    /// </summary>
    private ChartRecommendation? ValidateAndTruncate(ChartRecommendation? chart)
    {
        if (chart == null || chart.Type == ChartType.None)
        {
            return chart;
        }

        // Non-value visuals carry their own payload — no label/value truncation.
        if (chart.Type is ChartType.Stat or ChartType.Table or ChartType.Scatter)
        {
            return chart;
        }

        var maxPoints = GetMaxDataPointsForChartType(chart.Type);
        var valueCount = chart.Series.Length > 0 ? chart.Series[0].Values.Length : 0;
        var needsTruncation = chart.Series.Any(s => s.Values.Length > maxPoints);
        var needsLabelTruncation = chart.Labels.Any(l => l.Length > MaxLabelLength);
        var needsLabelGeneration = chart.Labels.Length == 0 && valueCount > 0;

        // Generate numbered labels if missing
        var workingLabels = needsLabelGeneration
            ? GenerateNumberedLabels(valueCount)
            : chart.Labels;

        if (needsLabelGeneration)
        {
            _logger.LogInformation(
                "Generated {Count} numbered labels for chart missing labels",
                valueCount);
        }

        if (!needsTruncation && !needsLabelTruncation && !needsLabelGeneration)
        {
            return chart;
        }

        var truncatedLabels = workingLabels
            .Take(maxPoints)
            .Select(TruncateLabel)
            .ToArray();

        var truncatedSeries = chart.Series
            .Select(s => s with { Values = s.Values.Take(maxPoints).ToArray() })
            .ToArray();

        if (needsTruncation)
        {
            _logger.LogInformation(
                "Truncating {ChartType} chart data from {Count} to {MaxCount} points",
                chart.Type, chart.Series.Max(s => s.Values.Length), maxPoints);
        }

        if (needsLabelTruncation)
        {
            var longLabels = workingLabels.Count(l => l.Length > MaxLabelLength);
            _logger.LogInformation(
                "Truncating {Count} long labels to max {MaxLength} characters",
                longLabels, MaxLabelLength);
        }

        return chart with
        {
            Labels = truncatedLabels,
            Series = truncatedSeries
        };
    }

    /// <summary>
    /// Generates numbered labels when chart is missing labels.
    /// </summary>
    private static string[] GenerateNumberedLabels(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => $"Item {i}")
            .ToArray();
    }

    /// <summary>
    /// Gets the maximum recommended data points for a chart type.
    /// </summary>
    private static int GetMaxDataPointsForChartType(ChartType type) => type switch
    {
        ChartType.Pie => MaxPieChartSlices,
        ChartType.Donut => MaxPieChartSlices,
        ChartType.Bar => MaxBarChartCategories,
        ChartType.HorizontalBar => MaxBarChartCategories,
        ChartType.GroupedBar => MaxBarChartCategories,
        ChartType.StackedBar => MaxBarChartCategories,
        ChartType.Line => MaxDataPoints,
        ChartType.Area => MaxDataPoints,
        _ => MaxDataPoints
    };

    /// <summary>
    /// Truncates a label to the maximum length with ellipsis.
    /// </summary>
    public static string TruncateLabel(string label)
    {
        if (string.IsNullOrEmpty(label) || label.Length <= MaxLabelLength)
        {
            return label;
        }

        // Try to truncate at a word boundary
        var truncated = label[..(MaxLabelLength - 1)];
        var lastSpace = truncated.LastIndexOf(' ');

        if (lastSpace > MaxLabelLength / 2)
        {
            // Truncate at word boundary if it's not too short
            return truncated[..lastSpace] + "…";
        }

        return truncated + "…";
    }

    /// <summary>
    /// Checks if labels are too long and chart might benefit from horizontal orientation.
    /// </summary>
    public bool ShouldUseHorizontalBar(ChartRecommendation? chart)
    {
        if (chart == null || chart.Type != ChartType.Bar || chart.Labels.Length == 0)
        {
            return false;
        }

        var averageLabelLength = chart.Labels.Average(l => l.Length);
        return averageLabelLength > 12;
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?([\s\S]*?)\n?```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonCodeBlockRegex();
}
