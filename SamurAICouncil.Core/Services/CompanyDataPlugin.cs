using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using SamurAICouncil.Core.Interfaces;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Semantic Kernel plugin that provides company data query capabilities.
/// Enables OpenAI function calling to query the ContosoRetailDW database.
/// </summary>
public class CompanyDataPlugin
{
    private readonly ICompanyDataService _dataService;

    public CompanyDataPlugin(ICompanyDataService dataService)
    {
        _dataService = dataService;
    }

    [KernelFunction("query_company_data")]
    [Description("Query company financial, sales, product, customer, and inventory data from the database. " +
                 "Use this for questions about sales figures, revenue, products, customers, stores, and inventory. " +
                 "The database contains retail data from 2007-2009 including product categories like computers, " +
                 "cameras, cell phones, TVs, audio equipment, games, and home appliances.")]
    public async Task<string> QueryCompanyDataAsync(
        [Description("Natural language question about company data. Examples: " +
                     "'What were total sales in 2008?', " +
                     "'Which product category has the highest revenue?', " +
                     "'Show me top 10 customers by purchase amount', " +
                     "'What is the average order value by store type?'")]
        string query,
        CancellationToken cancellationToken = default)
    {
        var result = await _dataService.QueryCompanyDataAsync(query, cancellationToken);

        // Format the result for the LLM
        return FormatResultForLlm(result);
    }

    [KernelFunction("get_company_data_schema")]
    [Description("Get information about what company data is available to query. " +
                 "Use this when you need to understand what data exists before querying.")]
    public string GetCompanyDataSchema()
    {
        return _dataService.GetSchemaDocumentation();
    }

    private static string FormatResultForLlm(CompanyDataResult result)
    {
        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.ErrorMessage
            });
        }

        // For small result sets, return all data
        // For larger sets, provide a summary
        if (result.RowCount <= 20)
        {
            return JsonSerializer.Serialize(new
            {
                success = true,
                sql = result.GeneratedSql,
                rowCount = result.RowCount,
                data = result.Data
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        // For larger result sets, include sample and summary
        var sample = result.Data?.Take(10).ToList();
        return JsonSerializer.Serialize(new
        {
            success = true,
            sql = result.GeneratedSql,
            rowCount = result.RowCount,
            note = $"Showing first 10 of {result.RowCount} rows",
            data = sample
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
