using System.Text.Json;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// LLM tool for querying company data from the ContosoRetailDW database.
/// </summary>
public class CompanyDataTool : ILlmTool
{
    private readonly ICompanyDataService _dataService;

    public CompanyDataTool(ICompanyDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>
    /// Gets the chart recommendation from the last query execution.
    /// </summary>
    public ChartRecommendation? LastChartRecommendation { get; private set; }

    public string Name => "query_company_data";

    public string Description => "Query company financial, sales, product, customer, and inventory data from the database. " +
                                 "Use this for questions about sales figures, revenue, products, customers, stores, and inventory. " +
                                 "The database contains retail data from 2007-2009.";

    public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            query = new
            {
                type = "string",
                description = "Natural language question about company data. Examples: " +
                              "'What were total sales in 2008?', " +
                              "'Which product category has the highest revenue?', " +
                              "'Show me top 10 customers by purchase amount'"
            }
        },
        required = new[] { "query" }
    });

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
    {
        LastChartRecommendation = null;

        var query = input.GetProperty("query").GetString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Query is required" });
        }

        var result = await _dataService.QueryCompanyDataAsync(query, cancellationToken);

        // Generate chart recommendation if query was successful
        if (result.Success && result.Data is { Count: > 0 })
        {
            LastChartRecommendation = await _dataService.GenerateChartRecommendationAsync(
                query, result.Data, cancellationToken);
        }

        return FormatResult(result, LastChartRecommendation);
    }

    private static string FormatResult(CompanyDataResult result, ChartRecommendation? chart)
    {
        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.ErrorMessage
            });
        }

        var options = new JsonSerializerOptions { WriteIndented = true };

        // For small result sets, return all data
        if (result.RowCount <= 20)
        {
            return JsonSerializer.Serialize(new
            {
                success = true,
                sql = result.GeneratedSql,
                rowCount = result.RowCount,
                data = result.Data,
                chart
            }, options);
        }

        // For larger result sets, include sample and summary
        var sample = result.Data?.Take(10).ToList();
        return JsonSerializer.Serialize(new
        {
            success = true,
            sql = result.GeneratedSql,
            rowCount = result.RowCount,
            note = $"Showing first 10 of {result.RowCount} rows",
            data = sample,
            chart
        }, options);
    }
}
