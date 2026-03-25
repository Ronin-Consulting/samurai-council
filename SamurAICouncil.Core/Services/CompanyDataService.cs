using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Service for querying company data using natural language (Text-to-SQL).
/// Uses an LLM to generate SQL from natural language queries and executes
/// them against the ContosoRetailDW SQL Server database.
/// </summary>
public partial class CompanyDataService : ICompanyDataService
{
    private readonly ILogger<CompanyDataService> _logger;
    private readonly ILlmService _llmService;
    private readonly CompanyDataConfiguration _config;
    private readonly string _schemaDocumentation;

    // Regex patterns for SQL validation
    [GeneratedRegex(@"^\s*(SELECT|WITH)\s", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectOnlyPattern();

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|TRUNCATE|EXEC|EXECUTE|GRANT|REVOKE|DENY)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousKeywordsPattern();

    [GeneratedRegex(@";\s*\w", RegexOptions.None)]
    private static partial Regex MultiStatementPattern();

    // Pattern to extract SQL from markdown code blocks (handles text before/after)
    [GeneratedRegex(@"```sql\s*([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex SqlCodeBlockPattern();

    // Pattern to extract SQL from generic code blocks
    [GeneratedRegex(@"```\s*(SELECT[\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex GenericCodeBlockPattern();

    public CompanyDataService(
        ILogger<CompanyDataService> logger,
        ILlmService llmService,
        IOptions<CompanyDataConfiguration> config)
    {
        _logger = logger;
        _llmService = llmService;
        _config = config.Value;
        _schemaDocumentation = LoadSchemaDocumentation();

        _logger.LogInformation(
            "CompanyDataService initialized. Enabled: {Enabled}, MaxRows: {MaxRows}, Timeout: {Timeout}s",
            _config.IsEnabled, _config.MaxRows, _config.QueryTimeoutSeconds);
    }

    public async Task<CompanyDataResult> QueryCompanyDataAsync(
        string naturalLanguageQuery,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsEnabled)
        {
            return CompanyDataResult.Error("Company data service is not configured. Please set the connection string.");
        }

        if (string.IsNullOrWhiteSpace(naturalLanguageQuery))
        {
            return CompanyDataResult.Error("Query cannot be empty.");
        }

        _logger.LogInformation("Processing natural language query: {Query}", naturalLanguageQuery);

        string? sql = null;
        try
        {
            // Step 1: Generate SQL from natural language
            sql = await GenerateSqlAsync(naturalLanguageQuery, cancellationToken);
            if (sql == null)
            {
                return CompanyDataResult.Error("Failed to generate SQL query. Please try rephrasing your question.");
            }

            _logger.LogInformation("Generated SQL: {Sql}", sql);

            // Step 2: Validate SQL is safe
            var validationError = ValidateSql(sql);
            if (validationError != null)
            {
                _logger.LogWarning("SQL validation failed: {Error}", validationError);
                return CompanyDataResult.Error(validationError, sql);
            }

            // Step 3: Execute SQL
            var data = await ExecuteSqlAsync(sql, cancellationToken);

            _logger.LogInformation("Query returned {RowCount} rows", data.Count);

            return CompanyDataResult.Ok(sql, data);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL execution error for query: {Sql}", sql);
            return CompanyDataResult.Error($"Database error: {SanitizeErrorMessage(ex.Message)}", sql);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Query was cancelled");
            return CompanyDataResult.Error("Query was cancelled or timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in CompanyDataService");
            return CompanyDataResult.Error("An unexpected error occurred while processing your query.");
        }
    }

    public string GetSchemaDocumentation() => _schemaDocumentation;

    public async Task<ChartRecommendation?> GenerateChartRecommendationAsync(
        string originalQuery,
        IReadOnlyList<Dictionary<string, object?>> data,
        CancellationToken cancellationToken = default)
    {
        // Don't generate charts for empty data or single-value results
        if (data.Count == 0)
        {
            return null;
        }

        // Single row with single column = single value, no chart needed
        if (data.Count == 1 && data[0].Count == 1)
        {
            return null;
        }

        try
        {
            var sampleData = data.Take(10).ToList();
            var dataJson = JsonSerializer.Serialize(sampleData, new JsonSerializerOptions { WriteIndented = true });

            var systemPrompt = """
                You are a data visualization expert. Analyze the query and data to determine if a chart would be helpful.

                ## Chart Type Selection Guide

                **BAR CHART** - Use for comparing categories:
                - Sales/revenue by region, country, store, product
                - Top N items ranked by value
                - Category comparisons (e.g., "sales by channel")
                - Best for 3-15 categories

                **LINE CHART** - Use for trends over time:
                - Monthly, quarterly, yearly trends
                - Data with date/time columns (year, month, quarter)
                - Sequential data showing change over time
                - Best for 5-20 time points

                **PIE CHART** - Use for proportions of a whole:
                - Percentage breakdowns (e.g., "market share", "distribution")
                - Part-to-whole relationships
                - Best for 2-6 categories (7+ becomes unreadable)
                - ALL VALUES MUST BE POSITIVE (no negative numbers!)

                **NONE** - Do NOT create a chart when:
                - Single value result (e.g., "total revenue is $X")
                - Text-only data with no numeric values
                - More than 15 categories with long labels
                - Data that doesn't benefit from visualization
                - Error or empty results
                - All values are zero
                - Data contains negative values for pie charts

                ## Chart Title Guidelines

                Create DESCRIPTIVE, CONCISE titles (max 50 characters):
                - Good: "Top 10 Stores by Revenue"
                - Good: "Monthly Sales Trend 2008"
                - Good: "Sales by Channel"
                - Bad: "Chart" or "Data" (too generic)
                - Bad: "Sales Revenue Amount by Store Name for Top 10" (too long)

                Include:
                - The metric being measured (Sales, Profit, Revenue)
                - The grouping dimension (by Country, by Month, by Product)
                - Any filter applied (Top 10, 2008, Q1)

                ## CRITICAL: Label Guidelines

                Labels must be SHORT and READABLE:
                - Maximum 15-20 characters per label
                - Abbreviate long names:
                  - "Contoso North America Store #1234" → "NA #1234"
                  - "Adventure Works Cycling Company" → "Adventure Works"
                  - "United States of America" → "USA"
                  - "Fourth Quarter 2008" → "Q4 2008"
                  - "January 2008" → "Jan 08"
                - For store names: Use store number or short identifier
                - For product names: Use brand + short name, not full description
                - For geographic data: Use country codes (USA, UK, DE, FR, JP)

                ## Axis Labels

                Always include axis labels when relevant:
                - xAxisLabel: What the X-axis represents (e.g., "Store", "Month", "Country")
                - yAxisLabel: What the Y-axis represents with units (e.g., "Revenue ($)", "Units Sold", "Profit (%)")

                ## Number Formatting in Series

                Format numbers appropriately:
                - Currency: Round to whole dollars (no cents), e.g., 1234567 not 1234567.89
                - Percentages: Use 1 decimal place, e.g., 23.5
                - Quantities: Use whole numbers

                ## Data Limits
                - Maximum 15 data points for bar charts (more becomes unreadable)
                - Maximum 20 data points for line charts
                - Maximum 6 slices for pie charts
                - If data exceeds limits, use "none" or suggest filtering

                ## Response Format
                Return ONLY valid JSON (no markdown, no explanation):
                {
                  "type": "bar|line|pie|none",
                  "title": "Short Descriptive Title",
                  "labels": ["Short1", "Short2", ...],
                  "series": [
                    { "name": "Series Name", "values": [100, 200, ...] }
                  ],
                  "xAxisLabel": "Category",
                  "yAxisLabel": "Value ($)"
                }

                If no chart is appropriate, return: { "type": "none" }
                """;

            var userPrompt = $"""
                Original question: {originalQuery}

                Data ({data.Count} rows, showing first {sampleData.Count}):
                {dataJson}

                Analyze this data and recommend a chart if appropriate.
                """;

            var messages = new List<ChatMessage>
            {
                ChatMessage.System(systemPrompt),
                ChatMessage.User(userPrompt)
            };

            var response = await _llmService.QueryModelAsync(
                _config.SqlGenerationModel,
                messages,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            return ParseChartRecommendation(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate chart recommendation");
            return null;
        }
    }

    private ChartRecommendation? ParseChartRecommendation(string response)
    {
        try
        {
            // Extract JSON from response (handle markdown code blocks)
            var jsonContent = ExtractJsonFromChartResponse(response);

            var chart = JsonSerializer.Deserialize<ChartRecommendation>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (chart == null || chart.Type == ChartType.None)
            {
                return null;
            }

            // Validate chart has required data
            if (chart.Series.Length == 0 || chart.Series.All(s => s.Values.Length == 0))
            {
                return null;
            }

            _logger.LogInformation("Generated {ChartType} chart recommendation: {Title}",
                chart.Type, chart.Title);

            return chart;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse chart recommendation JSON: {Response}", response);
            return null;
        }
    }

    private static string ExtractJsonFromChartResponse(string response)
    {
        var text = response.Trim();

        // Remove markdown code blocks if present
        if (text.StartsWith("```"))
        {
            var lines = text.Split('\n');
            var jsonLines = lines.SkipWhile(l => l.StartsWith("```"))
                                 .TakeWhile(l => !l.StartsWith("```"))
                                 .ToList();
            text = string.Join('\n', jsonLines);
        }

        // Find JSON object
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return text[start..(end + 1)];
        }

        return text;
    }

    private async Task<string?> GenerateSqlAsync(string query, CancellationToken cancellationToken)
    {
        var systemPrompt = $"""
            You are a SQL expert. Generate a T-SQL query for Microsoft SQL Server to answer the user's question.

            ## Database Schema
            {_schemaDocumentation}

            ## Critical Column Rules
            - ONLY use columns explicitly listed in the schema above
            - DO NOT invent or guess column names - if unsure, use only documented columns
            - DimCurrency has: CurrencyKey, CurrencyLabel, CurrencyName, CurrencyDescription (NO CurrencySymbol!)
            - FactInventory uses: OnHandQuantity, OnOrderQuantity (NOT UnitsOnHand, UnitsOrdered, TotalSold!)
            - DimGeography: country column is RegionCountryName (NOT CountryRegionName, Country, or CountryName!)
            - DimDate.Datekey is datetime type - use it directly for date joins
            - For currency display, use CurrencyLabel (e.g., 'USD', 'EUR')

            ## Table Selection
            - Use FactSales for aggregated daily sales (store + online combined)
            - Use FactOnlineSales for transaction-level online orders with customer details
            - Use V_CustomerOrders for basket analysis with customer demographics
            - Use V_ProductForecast for product forecasting by category

            ## Query Rules
            1. ONLY generate SELECT queries - no INSERT, UPDATE, DELETE, DROP, or any data modification
            2. Use proper JOINs to connect fact and dimension tables via foreign keys
            3. If user asks for "top N" or "N best/worst", use TOP N (e.g., "top 10" = TOP 10)
            4. Otherwise limit results to {_config.MaxRows} rows maximum using TOP
            5. Use table aliases for clarity (e.g., f for fact tables, d for dimensions)
            6. Include relevant columns that help answer the question
            7. Format monetary values appropriately
            8. Order results in a meaningful way (DESC for rankings, ASC for chronological)
            9. For year filtering: WHERE d.CalendarYear = 2008

            ## CRITICAL: Quarter and Month Filtering
            - CalendarQuarter values are YYYYQ format (20081, 20082, etc.) - DO NOT use 1, 2, 3, 4!
            - CalendarMonth values are YYYYMM format (200801, 200802, etc.) - DO NOT use 1-12!
            - For Q1: Use d.CalendarQuarterLabel = 'Q1' OR MONTH(d.Datekey) IN (1, 2, 3)
            - For Q2: Use d.CalendarQuarterLabel = 'Q2' OR MONTH(d.Datekey) IN (4, 5, 6)
            - For Q3: Use d.CalendarQuarterLabel = 'Q3' OR MONTH(d.Datekey) IN (7, 8, 9)
            - For Q4: Use d.CalendarQuarterLabel = 'Q4' OR MONTH(d.Datekey) IN (10, 11, 12)

            ## IMPORTANT: Avoid SQL Server Reserved Keywords as Column Aliases
            Do NOT use these as column aliases: RowCount, Count, Key, Index, Value, Name, Type, Status
            Instead use: TotalCount, RecordCount, ItemCount, RecordKey, ItemIndex, ItemValue, ItemName, ItemType, ItemStatus

            ## Output
            Return ONLY the SQL query, no explanations or markdown code blocks.
            """;

        var messages = new List<ChatMessage>
        {
            ChatMessage.System(systemPrompt),
            ChatMessage.User(query)
        };

        var response = await _llmService.QueryModelAsync(
            _config.SqlGenerationModel,
            messages,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        // Extract SQL from response - handle various formats
        var sql = ExtractSqlFromResponse(response);
        return sql;
    }

    /// <summary>
    /// Extracts SQL from LLM response, handling markdown code blocks and explanatory text.
    /// </summary>
    private string ExtractSqlFromResponse(string response)
    {
        var text = response.Trim();

        // Try to extract from ```sql ... ``` code block first
        var sqlBlockMatch = SqlCodeBlockPattern().Match(text);
        if (sqlBlockMatch.Success)
        {
            _logger.LogDebug("Extracted SQL from ```sql code block");
            return sqlBlockMatch.Groups[1].Value.Trim();
        }

        // Try to extract from generic ``` ... ``` code block containing SELECT
        var genericBlockMatch = GenericCodeBlockPattern().Match(text);
        if (genericBlockMatch.Success)
        {
            _logger.LogDebug("Extracted SQL from generic code block");
            return genericBlockMatch.Groups[1].Value.Trim();
        }

        // Handle simple case: response starts with ``` (no sql marker)
        if (text.StartsWith("```"))
        {
            text = text[3..];
            if (text.EndsWith("```"))
            {
                text = text[..^3];
            }
            return text.Trim();
        }

        // Handle case: response ends with ```
        if (text.EndsWith("```"))
        {
            text = text[..^3].Trim();
        }

        // If response starts with SELECT or WITH, assume it's raw SQL
        if (SelectOnlyPattern().IsMatch(text))
        {
            return text;
        }

        // Last resort: look for SELECT statement anywhere in the text
        var selectIndex = text.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        if (selectIndex >= 0)
        {
            _logger.LogDebug("Extracted SQL starting from SELECT keyword at position {Position}", selectIndex);
            return text[selectIndex..].Trim();
        }

        // Return as-is if no SQL found (will fail validation)
        return text;
    }

    private string? ValidateSql(string sql)
    {
        // Check it starts with SELECT or WITH (for CTEs)
        if (!SelectOnlyPattern().IsMatch(sql))
        {
            return "Only SELECT queries are allowed.";
        }

        // Check for dangerous keywords
        if (DangerousKeywordsPattern().IsMatch(sql))
        {
            return "Query contains prohibited keywords.";
        }

        // Check for multiple statements
        if (MultiStatementPattern().IsMatch(sql))
        {
            return "Multiple SQL statements are not allowed.";
        }

        return null;
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteSqlAsync(
        string sql,
        CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<string, object?>>();

        await using var connection = new SqlConnection(_config.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = _config.QueryTimeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columnNames = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        var rowCount = 0;
        while (await reader.ReadAsync(cancellationToken) && rowCount < _config.MaxRows)
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columnNames[i]] = value;
            }
            results.Add(row);
            rowCount++;
        }

        return results;
    }

    private static string LoadSchemaDocumentation()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "SamurAICouncil.Core.Resources.ContosoRetailDW_Schema.md";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Fallback: try to load from file system (for development)
            var fallbackPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                "ContosoRetailDW_Schema.md");

            if (File.Exists(fallbackPath))
            {
                return File.ReadAllText(fallbackPath);
            }

            return GetFallbackSchema();
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string SanitizeErrorMessage(string message)
    {
        // Remove potentially sensitive information from error messages
        // Don't expose table/column names in errors to end users
        if (message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        {
            return "The query references an invalid table or column.";
        }

        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "The query took too long to execute.";
        }

        return "A database error occurred.";
    }

    private static string GetFallbackSchema() => """
        # ContosoRetailDW Schema (Summary)

        ## Fact Tables
        - FactSales: Sales transactions (DateKey, ProductKey, StoreKey, SalesAmount, SalesQuantity)
        - FactInventory: Inventory snapshots

        ## Dimension Tables
        - DimProduct: Products (ProductKey, ProductName, BrandName, ClassName)
        - DimCustomer: Customers (CustomerKey, FirstName, LastName, City, Gender)
        - DimStore: Stores (StoreKey, StoreName, StoreType)
        - DimDate: Dates (DateKey, CalendarYear, CalendarMonth, CalendarQuarter)
        - DimChannel: Sales channels
        - DimPromotion: Promotions

        Data range: 2007-2009
        """;
}
