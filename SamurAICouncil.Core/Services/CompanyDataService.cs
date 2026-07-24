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
        // Nothing to visualize.
        if (data.Count == 0)
        {
            return null;
        }
        // Single-value / near-single results are no longer discarded — they become Stat tiles.

        try
        {
            var sampleData = data.Take(10).ToList();
            var dataJson = JsonSerializer.Serialize(sampleData, new JsonSerializerOptions { WriteIndented = true });

            var systemPrompt = """
                You are a data visualization expert. Pick the BEST form for the data's job.
                Choose the form first (what must the reader do?), then fill its payload.

                ## Job → form

                - **STAT** — one, or a few (2-4), headline numbers (e.g. "total revenue in 2008",
                  "revenue and orders"). A single value is a Stat, never a one-bar chart.
                - **TABLE** — a detail listing, or more than ~7 categories that all matter, or
                  mixed text columns that don't reduce to one number per row. Prefer a table over
                  cramming too much into a chart. (You only pick type=table + title; the rows are
                  filled from the real result set automatically — do NOT echo rows.)
                - **BAR** — compare magnitude across 3-15 categories (sales by channel, top N).
                - **HORIZONTALBAR** — same, but labels are long or there are many categories.
                - **GROUPEDBAR** — compare 2-4 distinct series per category, side by side
                  (e.g. sales by channel AND year). One `series` entry per series.
                - **STACKEDBAR** — part-to-whole across categories (composition that sums to a total).
                - **LINE** — a trend over time (5-20 time points; one or more series).
                - **AREA** — a single trend over time where the filled magnitude matters.
                - **PIE / DONUT** — proportions of a whole, 2-6 positive slices only.
                - **SCATTER** — correlation between two measures (x vs y), using `points`.
                - **NONE** — text-only data, all zeros, or nothing worth showing.

                Rules: never a dual-axis chart; pie/donut values must be positive; keep to one measure
                per axis. For multi-series comparisons use grouped/stacked bar, not two charts.

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
                Return ONLY valid JSON (no markdown, no explanation). `type` is one of:
                bar | horizontalBar | groupedBar | stackedBar | line | area | pie | donut | scatter | stat | table | none

                Category charts (bar/horizontalBar/groupedBar/stackedBar/line/area/pie/donut):
                {
                  "type": "bar",
                  "title": "Short Descriptive Title",
                  "labels": ["Short1", "Short2"],
                  "series": [ { "name": "Series Name", "values": [100, 200] } ],
                  "xAxisLabel": "Category",
                  "yAxisLabel": "Value ($)"
                }
                (grouped/stacked bar: include one object per series in "series".)

                Scatter:
                { "type": "scatter", "title": "...", "xAxisLabel": "Unit Price ($)", "yAxisLabel": "Quantity",
                  "series": [ { "name": "Orders", "points": [ { "x": 12.5, "y": 3, "label": "opt" } ] } ] }

                Stat (one to four KPIs — read the numbers straight from the data):
                { "type": "stat", "title": "...", "stats": [ { "label": "Total Revenue", "value": 4111233535, "unit": "$", "caption": "2008" } ] }

                Table (pick type + title only; rows are filled automatically):
                { "type": "table", "title": "Products" }

                If nothing is worth showing, return: { "type": "none" }
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

            var recommendation = ParseChartRecommendation(response);
            if (recommendation is null)
            {
                return null;
            }

            // Fill non-chart payloads from the REAL result set (avoid LLM hallucination of rows/values).
            if (recommendation.Type == ChartType.Table)
            {
                recommendation = recommendation with { Table = BuildTable(data) };
            }
            else if (recommendation.Type == ChartType.Stat &&
                     (recommendation.Stats is null || recommendation.Stats.Length == 0))
            {
                recommendation = recommendation with { Stats = BuildStats(data) };
            }

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate chart recommendation");
            return null;
        }
    }

    private const int MaxTableColumns = 12;

    /// <summary>Builds a table payload from the actual query result (columns from keys, formatted cells).</summary>
    private static TableData BuildTable(IReadOnlyList<Dictionary<string, object?>> data)
    {
        var columns = data[0].Keys.Take(MaxTableColumns).ToArray();
        var rows = data
            .Select(row => columns.Select(c => FormatCell(row.TryGetValue(c, out var v) ? v : null)).ToArray())
            .ToArray();
        return new TableData { Columns = columns, Rows = rows };
    }

    /// <summary>Builds stat tiles from a single-row result: one tile per numeric column (max 4).</summary>
    private static StatData[] BuildStats(IReadOnlyList<Dictionary<string, object?>> data)
    {
        var row = data[0];
        var stats = new List<StatData>();
        foreach (var (key, value) in row)
        {
            if (TryToDouble(value, out var num))
            {
                stats.Add(new StatData { Label = key, Value = num });
            }
            if (stats.Count == 4) break;
        }
        // Fallback: if no numeric columns, show the first cell as a text-less stat.
        if (stats.Count == 0 && row.Count > 0)
        {
            var first = row.First();
            stats.Add(new StatData { Label = first.Key, Value = 0, Caption = FormatCell(first.Value) });
        }
        return stats.ToArray();
    }

    private static bool TryToDouble(object? value, out double result)
    {
        switch (value)
        {
            case null: result = 0; return false;
            case double d: result = d; return true;
            case float f: result = f; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case decimal m: result = (double)m; return true;
            case System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number:
                result = je.GetDouble(); return true;
            default:
                return double.TryParse(value.ToString(), out result);
        }
    }

    private static string FormatCell(object? value)
    {
        if (value is null) return string.Empty;
        if (value is System.Text.Json.JsonElement je)
        {
            return je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => je.GetRawText(),
                System.Text.Json.JsonValueKind.String => je.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                System.Text.Json.JsonValueKind.Null => string.Empty,
                _ => je.GetRawText()
            };
        }
        if (value is double or float or decimal)
        {
            return Convert.ToDouble(value).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
        return value.ToString() ?? string.Empty;
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

            // Value-based charts require at least one non-empty value series.
            // Stat/Table/Scatter carry their own payloads (filled/validated elsewhere).
            var valueBased = chart.Type is not (ChartType.Stat or ChartType.Table or ChartType.Scatter);
            if (valueBased && (chart.Series.Length == 0 || chart.Series.All(s => s.Values.Length == 0)))
            {
                return null;
            }

            _logger.LogInformation("Generated {ChartType} visualization recommendation: {Title}",
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
            - For currency display, use CurrencyName (e.g., 'USD', 'EUR') - CurrencyLabel is a numeric ETL code, not a display code
            - V_SalesAnalysis is already fully joined and flat - query it ALONE (FROM V_SalesAnalysis, nothing else).
              Do NOT JOIN it to FactSales, DimDate, DimStore, DimGeography, or DimProduct - it already has
              Year/Quarter/MonthName/SaleDate, CountryName, CategoryName/BrandName, and CurrencyName as columns.

            ## Table Selection
            - PREFER V_SalesAnalysis over manually joining FactSales for questions about sales by geography,
              product, channel, time period, or currency - it already has clean, pre-joined columns
              (CountryName, CategoryName, SubcategoryName, BrandName, ChannelName, StoreName, Quarter,
              MonthName, CurrencyName, PromotionName, etc.). Only join raw fact/dimension tables yourself
              if the question needs a column V_SalesAnalysis doesn't have.
            - Use FactSales directly only for aggregations V_SalesAnalysis can't answer (store + online combined)
            - Use FactOnlineSales for transaction-level online orders with customer details
            - Use V_CustomerOrders for basket analysis with customer demographics
            - Use V_ProductForecast for product forecasting by category
            - Use V_SalesAnalysis for general sales reporting by geography/product/channel/time/currency - avoids manual joins

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
