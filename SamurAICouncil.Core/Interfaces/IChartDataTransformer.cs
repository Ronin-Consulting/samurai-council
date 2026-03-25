using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Interfaces;

/// <summary>
/// Transforms data and LLM responses into chart recommendations.
/// </summary>
public interface IChartDataTransformer
{
    /// <summary>
    /// Parses a chart recommendation from LLM JSON response.
    /// </summary>
    /// <param name="llmChartJson">JSON string containing chart recommendation.</param>
    /// <returns>Parsed chart recommendation, or null if parsing fails.</returns>
    ChartRecommendation? ParseChartRecommendation(string llmChartJson);

    /// <summary>
    /// Validates a chart recommendation for display.
    /// </summary>
    /// <param name="chart">Chart to validate.</param>
    /// <returns>True if chart is valid and can be displayed.</returns>
    bool IsValidChart(ChartRecommendation? chart);

    /// <summary>
    /// Determines if a bar chart should use horizontal orientation based on label length.
    /// </summary>
    /// <param name="chart">Chart to evaluate.</param>
    /// <returns>True if horizontal bar chart is recommended for better label readability.</returns>
    bool ShouldUseHorizontalBar(ChartRecommendation? chart);
}
