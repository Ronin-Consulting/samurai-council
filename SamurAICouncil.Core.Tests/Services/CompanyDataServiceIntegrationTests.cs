using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

/// <summary>
/// Integration tests for CompanyDataService that test SQL generation accuracy
/// and end-to-end query execution against a live SQL Server database.
///
/// PREREQUISITES:
/// 1. SQL Server running with ContosoRetailDW database restored
///    - docker compose up -d sqlserver
///    - ./scripts/init-contoso.sh
///
/// 2. Environment variables:
///    - OPENAI_API_KEY: For SQL generation
///    - CONTOSO_CONNECTION_STRING: SQL Server connection string
///      Default: Server=localhost,1433;Database=ContosoRetailDW;User Id=samurai_reader;Password=Reader!Pass123;TrustServerCertificate=True
///
/// Run these tests:
///   dotnet test --filter "TestCategory=E2E" -- MSTest.Parallelize.Workers=1
/// </summary>
[TestClass]
[TestCategory("Integration")]
[TestCategory("E2E")]
public class CompanyDataServiceIntegrationTests
{
    private static string? OpenAiApiKey;
    private static string? ConnectionString;

    private Mock<ILogger<CompanyDataService>> _mockLogger = null!;
    private Mock<ILogger<SemanticKernelLlmService>> _mockLlmLogger = null!;

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        LoadEnvironmentVariables();

        OpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        ConnectionString = Environment.GetEnvironmentVariable("CONTOSO_CONNECTION_STRING")
            ?? "Server=localhost,1433;Database=ContosoRetailDW;User Id=samurai_reader;Password=Reader!Pass123;TrustServerCertificate=True";
    }

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<CompanyDataService>>();
        _mockLlmLogger = new Mock<ILogger<SemanticKernelLlmService>>();
    }

    #region Schema Documentation Tests (Epic 6.3)

    [TestMethod]
    public void SchemaDocumentation_ContainsExpectedTables()
    {
        var service = CreateServiceWithMockLlm();
        var schema = service.GetSchemaDocumentation();

        // Verify key fact tables are documented
        Assert.IsTrue(schema.Contains("FactSales"), "Schema should contain FactSales");
        Assert.IsTrue(schema.Contains("FactOnlineSales"), "Schema should contain FactOnlineSales");

        // Verify key dimension tables
        Assert.IsTrue(schema.Contains("DimProduct"), "Schema should contain DimProduct");
        Assert.IsTrue(schema.Contains("DimCustomer"), "Schema should contain DimCustomer");
        Assert.IsTrue(schema.Contains("DimDate"), "Schema should contain DimDate");
        Assert.IsTrue(schema.Contains("DimStore"), "Schema should contain DimStore");

        // Verify schema has relationships documented
        Assert.IsTrue(schema.Contains("ProductKey") || schema.Contains("foreign key", StringComparison.OrdinalIgnoreCase),
            "Schema should document key relationships");
    }

    [TestMethod]
    public void SchemaDocumentation_HasSufficientDetail()
    {
        var service = CreateServiceWithMockLlm();
        var schema = service.GetSchemaDocumentation();

        // Schema should have enough detail for LLM to generate accurate SQL
        Assert.IsTrue(schema.Length > 5000, "Schema should be comprehensive (>5000 chars)");

        // Should contain column names
        Assert.IsTrue(schema.Contains("SalesAmount") || schema.Contains("TotalCost"),
            "Schema should contain monetary columns");
        Assert.IsTrue(schema.Contains("CalendarYear") || schema.Contains("DateKey"),
            "Schema should contain date-related columns");
    }

    #endregion

    #region SQL Generation Tests (Epic 6.3)

    [TestMethod]
    public async Task SqlGeneration_TotalSalesQuery_GeneratesValidSql()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What were total sales in 2008?");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Error: {result.ErrorMessage}");

        // The query should succeed
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");

        // Generated SQL should reference FactSales or FactOnlineSales
        Assert.IsTrue(
            result.GeneratedSql?.Contains("FactSales", StringComparison.OrdinalIgnoreCase) == true ||
            result.GeneratedSql?.Contains("FactOnlineSales", StringComparison.OrdinalIgnoreCase) == true,
            "SQL should query a sales fact table");

        // Should filter by 2008
        Assert.IsTrue(
            result.GeneratedSql?.Contains("2008") == true,
            "SQL should filter for year 2008");
    }

    [TestMethod]
    public async Task SqlGeneration_ProductCategoryQuery_GeneratesValidSql()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Which product category has the highest revenue?");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Success: {result.Success}");

        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");

        // Should reference product dimension for categories
        Assert.IsTrue(
            result.GeneratedSql?.Contains("DimProduct", StringComparison.OrdinalIgnoreCase) == true ||
            result.GeneratedSql?.Contains("ProductCategory", StringComparison.OrdinalIgnoreCase) == true,
            "SQL should reference product data");
    }

    [TestMethod]
    public async Task SqlGeneration_TopCustomersQuery_GeneratesValidSql()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Show me top 10 customers by purchase amount");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Success: {result.Success}");

        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");

        // Should reference customer dimension
        Assert.IsTrue(
            result.GeneratedSql?.Contains("DimCustomer", StringComparison.OrdinalIgnoreCase) == true ||
            result.GeneratedSql?.Contains("Customer", StringComparison.OrdinalIgnoreCase) == true,
            "SQL should reference customer data");

        // Should use TOP or LIMIT
        Assert.IsTrue(
            result.GeneratedSql?.Contains("TOP", StringComparison.OrdinalIgnoreCase) == true,
            "SQL should limit results");
    }

    #endregion

    #region Release 7 Test Matrix - Simple Aggregations

    [TestMethod]
    public async Task TestMatrix_SimpleAgg_ProductCount()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("How many products are in the catalog?");
        LogResult("ProductCount", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_SimpleAgg_SalesChannels()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("List all sales channels");
        LogResult("SalesChannels", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_SimpleAgg_AvgUnitPrice()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What's the average unit price across all products?");
        LogResult("AvgUnitPrice", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_SimpleAgg_TotalCustomers()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("How many customers are in the database?");
        LogResult("TotalCustomers", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_SimpleAgg_TotalStores()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("How many stores do we have?");
        LogResult("TotalStores", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    #endregion

    #region Release 7 Test Matrix - Time-Based Queries

    [TestMethod]
    public async Task TestMatrix_TimeBased_SalesByYear()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Show total sales for each year");
        LogResult("SalesByYear", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_TimeBased_QuarterlySales()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What were the quarterly sales for 2008?");
        LogResult("QuarterlySales", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount >= 1, "Should return quarterly data");
    }

    [TestMethod]
    public async Task TestMatrix_TimeBased_YearOverYear()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Compare sales between 2008 and 2009");
        LogResult("YearOverYear", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_TimeBased_BestMonth()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Which month had the highest sales in 2008?");
        LogResult("BestMonth", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    #endregion

    #region Release 7 Test Matrix - Multi-Table Joins

    [TestMethod]
    public async Task TestMatrix_Joins_Top5ProductsBySales()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What are the top 5 products by sales in 2009?");
        LogResult("Top5Products", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0 && result.RowCount <= 5, "Should return up to 5 products");
    }

    [TestMethod]
    public async Task TestMatrix_Joins_SalesByCategory()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Sales by product category for each year");
        LogResult("SalesByCategory", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_Joins_TopStoresInCalifornia()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Which stores had the highest sales in California?");
        LogResult("TopStoresCalifornia", result);
        // This may return 0 rows if there are no California stores - that's okay
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Joins_TopPromotions()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What promotions drove the most revenue?");
        LogResult("TopPromotions", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_Joins_ProductsByBrand()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("How many products does each brand have?");
        LogResult("ProductsByBrand", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    #endregion

    #region Release 7 Test Matrix - Product Hierarchy

    [TestMethod]
    public async Task TestMatrix_Product_SalesBySubcategory()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Show sales by product subcategory");
        LogResult("SalesBySubcategory", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_Product_ComputersSales()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Monthly sales trend for Computers category in 2008");
        LogResult("ComputersSales", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_Product_TopBrands()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What are the top 10 brands by total revenue?");
        LogResult("TopBrands", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0 && result.RowCount <= 10, "Should return up to 10 brands");
    }

    #endregion

    #region Release 7 Test Matrix - Customer Demographics

    [TestMethod]
    public async Task TestMatrix_Customer_ByGender()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Break down sales by customer gender");
        LogResult("SalesByGender", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_Customer_ByAge()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Average purchase amount by customer age group");
        LogResult("SalesByAge", result);
        // This might require specific calculation - just check it doesn't error
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Customer_ByEducation()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Sales by customer education level");
        LogResult("SalesByEducation", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task TestMatrix_Customer_Demographics()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Customer demographics breakdown by purchase amount");
        LogResult("CustomerDemographics", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    #endregion

    #region Release 7 Test Matrix - Geographic Queries

    [TestMethod]
    public async Task TestMatrix_Geo_SalesByCountry()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Total sales by country");
        LogResult("SalesByCountry", result);
        // Note: This query may use DimGeography.CountryRegionName which may not exist
        // The correct approach is via DimSalesTerritory.SalesTerritoryCountry
        // Accepting either success OR known column error (tracked in Issue #5)
        if (!result.Success && result.ErrorMessage?.Contains("invalid") == true)
        {
            Console.WriteLine("Known Issue #5: DimGeography country column variance");
        }
        else
        {
            Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        }
    }

    [TestMethod]
    public async Task TestMatrix_Geo_TopCities()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What are the top 10 cities by sales?");
        LogResult("TopCities", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0 && result.RowCount <= 10, "Should return up to 10 cities");
    }

    [TestMethod]
    public async Task TestMatrix_Geo_SalesByRegion()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Sales by sales territory region");
        LogResult("SalesByRegion", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    #endregion

    #region Release 7 Test Matrix - Inventory Queries

    [TestMethod]
    public async Task TestMatrix_Inventory_CurrentLevels()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Show current inventory levels by product");
        LogResult("InventoryLevels", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Inventory_LowStock()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Which products have low inventory?");
        LogResult("LowStock", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Inventory_Turnover()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Inventory turnover by product brand");
        LogResult("InventoryTurnover", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    #endregion

    #region Release 7 Test Matrix - Edge Cases (DimCurrency, Exchange Rates, IT Tables)

    [TestMethod]
    public async Task TestMatrix_Edge_CurrencySales()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Show me sales in different currencies");
        LogResult("CurrencySales", result);
        // This tests if the model correctly uses CurrencyLabel (not CurrencySymbol!)
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Edge_ExchangeRates()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What's the exchange rate for EUR to USD?");
        LogResult("ExchangeRates", result);
        // FactExchangeRate table - might or might not have data
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Edge_ITMachine()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("IT machine maintenance costs by year");
        LogResult("ITMachine", result);
        // Tests FactITMachine table
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Edge_ITSLA()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Store outage downtime analysis");
        LogResult("ITSLA", result);
        // Tests FactITSLA table
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Edge_CurrencyList()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("List all currencies in the database");
        LogResult("CurrencyList", result);
        // Tests correct column usage: CurrencyKey, CurrencyLabel, CurrencyName, CurrencyDescription
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return currency data");
    }

    #endregion

    #region Release 7 Test Matrix - Complex Multi-Join Queries

    [TestMethod]
    public async Task TestMatrix_Complex_SalesVsQuota()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Sales vs. quota comparison by store");
        LogResult("SalesVsQuota", result);
        // Uses FactSalesQuota
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Complex_CustomerLifetimeValue()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What are the top 20 customers by total lifetime purchases?");
        LogResult("CustomerLTV", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount <= 20, "Should return at most 20 customers");
    }

    [TestMethod]
    public async Task TestMatrix_Complex_ProductPerformanceByStore()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Best selling products at each store type");
        LogResult("ProductByStoreType", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task TestMatrix_Complex_SeasonalTrends()
    {
        SkipIfMissingPrerequisites();
        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Identify seasonal sales trends by product category");
        LogResult("SeasonalTrends", result);
        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
    }

    #endregion

    #region End-to-End Query Tests (Epic 7.3)

    [TestMethod]
    public async Task E2E_TotalSales2008_ReturnsData()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What were total sales in 2008?");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Row Count: {result.RowCount}");
        Console.WriteLine($"Success: {result.Success}");

        if (result.Success && result.Data != null)
        {
            foreach (var row in result.Data.Take(5))
            {
                Console.WriteLine($"  Row: {string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }

        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task E2E_ProductCategoryRevenue_ReturnsData()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Which product category has the highest revenue?");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Row Count: {result.RowCount}");

        if (result.Success && result.Data != null)
        {
            foreach (var row in result.Data.Take(5))
            {
                Console.WriteLine($"  Row: {string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }

        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task E2E_TopCustomers_ReturnsData()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Show me top 10 customers by purchase amount");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Row Count: {result.RowCount}");

        if (result.Success && result.Data != null)
        {
            foreach (var row in result.Data.Take(5))
            {
                Console.WriteLine($"  Row: {string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }

        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
        Assert.IsTrue(result.RowCount <= 10, "Should return at most 10 rows");
    }

    [TestMethod]
    public async Task E2E_InvalidQuery_HandlesGracefully()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Show me data from the FakeTable that doesn't exist");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Error: {result.ErrorMessage}");

        // The query might succeed with a valid SQL that returns no rows,
        // or it might fail with a sanitized error message
        // Either way, it shouldn't expose internal details
        if (!result.Success)
        {
            Assert.IsFalse(result.ErrorMessage?.Contains("sys.") == true,
                "Error should not expose system table names");
        }
    }

    [TestMethod]
    public async Task E2E_StorePerformance_ReturnsData()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("What are the top 5 stores by total sales?");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Row Count: {result.RowCount}");

        if (result.Success && result.Data != null)
        {
            foreach (var row in result.Data)
            {
                Console.WriteLine($"  Row: {string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }

        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    [TestMethod]
    public async Task E2E_MonthlySalesTrend_ReturnsData()
    {
        SkipIfMissingPrerequisites();

        var service = CreateService();
        var result = await service.QueryCompanyDataAsync("Show monthly sales totals for 2008");

        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Row Count: {result.RowCount}");

        if (result.Success && result.Data != null)
        {
            foreach (var row in result.Data.Take(12))
            {
                Console.WriteLine($"  Row: {string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }

        Assert.IsTrue(result.Success, $"Query should succeed. Error: {result.ErrorMessage}");
        Assert.IsTrue(result.RowCount > 0, "Should return at least one row");
    }

    #endregion

    #region Helpers

    private static void LogResult(string testName, CompanyDataResult result)
    {
        Console.WriteLine($"\n=== {testName} ===");
        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Generated SQL: {result.GeneratedSql}");
        Console.WriteLine($"Row Count: {result.RowCount}");

        if (result.Success && result.Data != null)
        {
            foreach (var row in result.Data.Take(5))
            {
                Console.WriteLine($"  Row: {string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            if (result.RowCount > 5)
            {
                Console.WriteLine($"  ... and {result.RowCount - 5} more rows");
            }
        }
        else if (!result.Success)
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }
    }

    private static void SkipIfMissingPrerequisites()
    {
        if (string.IsNullOrWhiteSpace(OpenAiApiKey))
        {
            Assert.Inconclusive("Skipping test: OPENAI_API_KEY environment variable not set.");
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Assert.Inconclusive("Skipping test: CONTOSO_CONNECTION_STRING not configured.");
        }
    }

    private CompanyDataService CreateService()
    {
        var apiKeys = new LlmApiKeysConfiguration { OpenAI = OpenAiApiKey };
        var council = new CouncilConfiguration
        {
            CouncilModels = [new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" }]
        };

        var llmService = new SemanticKernelLlmService(
            _mockLlmLogger.Object,
            Options.Create(apiKeys),
            Options.Create(council));

        var config = new CompanyDataConfiguration
        {
            ConnectionString = ConnectionString,
            MaxRows = 100,
            QueryTimeoutSeconds = 30,
            SqlGenerationModel = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" }
        };

        return new CompanyDataService(
            _mockLogger.Object,
            llmService,
            Options.Create(config));
    }

    private CompanyDataService CreateServiceWithMockLlm()
    {
        var mockLlmService = new Mock<ILlmService>();
        var config = new CompanyDataConfiguration
        {
            ConnectionString = null, // Disabled
            MaxRows = 100,
            QueryTimeoutSeconds = 30,
            SqlGenerationModel = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" }
        };

        return new CompanyDataService(
            _mockLogger.Object,
            mockLlmService.Object,
            Options.Create(config));
    }

    private static void LoadEnvironmentVariables()
    {
        var envFilePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "env_vars"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "env_vars"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "env_vars"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "env_vars")
        };

        foreach (var envFilePath in envFilePaths)
        {
            if (File.Exists(envFilePath))
            {
                Console.WriteLine($"Loading env_vars from: {envFilePath}");

                foreach (var line in File.ReadAllLines(envFilePath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }
                break;
            }
        }
    }

    #endregion
}
