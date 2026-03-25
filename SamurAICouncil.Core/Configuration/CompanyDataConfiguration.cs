namespace SamurAICouncil.Core.Configuration;

/// <summary>
/// Configuration for the Company Data Service (Text-to-SQL).
/// </summary>
public class CompanyDataConfiguration
{
    public const string SectionName = "CompanyData";

    /// <summary>
    /// Connection string for the ContosoRetailDW SQL Server database.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of rows to return from queries.
    /// </summary>
    public int MaxRows { get; set; } = 100;

    /// <summary>
    /// Query timeout in seconds.
    /// </summary>
    public int QueryTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Model configuration for SQL generation.
    /// Uses a fast, capable model for Text-to-SQL.
    /// </summary>
    public ModelConfiguration SqlGenerationModel { get; set; } = new()
    {
        Provider = "openai",
        ModelId = "gpt-4o-mini",
        DisplayName = "GPT-4o Mini (SQL)"
    };

    /// <summary>
    /// Maximum tokens for SQL generation response.
    /// Default is 16384 which provides ample room for complex queries.
    /// </summary>
    public int MaxTokens { get; set; } = 16384;

    /// <summary>
    /// Whether the service is enabled. Disabled if connection string is empty.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ConnectionString);
}
