using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Interfaces;

/// <summary>
/// Service for querying company data using natural language (Text-to-SQL).
/// Provides read-only access to the ContosoRetailDW database.
/// </summary>
public interface ICompanyDataService
{
    /// <summary>
    /// Query company data using a natural language question.
    /// Generates SQL from the question and executes it against the database.
    /// </summary>
    /// <param name="naturalLanguageQuery">The question to answer about company data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing generated SQL, data, and any errors.</returns>
    Task<CompanyDataResult> QueryCompanyDataAsync(
        string naturalLanguageQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the schema documentation for the ContosoRetailDW database.
    /// Used to provide context to LLMs for SQL generation.
    /// </summary>
    /// <returns>Markdown-formatted schema documentation.</returns>
    string GetSchemaDocumentation();

    /// <summary>
    /// Generates a chart recommendation for the given query result.
    /// </summary>
    /// <param name="originalQuery">The original natural language query.</param>
    /// <param name="data">The query result data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chart recommendation, or null if no chart is appropriate.</returns>
    Task<ChartRecommendation?> GenerateChartRecommendationAsync(
        string originalQuery,
        IReadOnlyList<Dictionary<string, object?>> data,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a company data query operation.
/// </summary>
/// <param name="Success">Whether the query executed successfully.</param>
/// <param name="GeneratedSql">The SQL query generated from natural language (null if generation failed).</param>
/// <param name="Data">The query results as a list of dictionaries (column name -> value).</param>
/// <param name="ErrorMessage">Error message if the query failed.</param>
/// <param name="RowCount">Number of rows returned.</param>
public record CompanyDataResult(
    bool Success,
    string? GeneratedSql,
    IReadOnlyList<Dictionary<string, object?>>? Data,
    string? ErrorMessage,
    int RowCount)
{
    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static CompanyDataResult Ok(string sql, IReadOnlyList<Dictionary<string, object?>> data)
        => new(true, sql, data, null, data.Count);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static CompanyDataResult Error(string message, string? sql = null)
        => new(false, sql, null, message, 0);
}
