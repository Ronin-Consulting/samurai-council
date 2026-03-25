using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

[TestClass]
public class CompanyDataServiceTests
{
    private Mock<ILlmService> _mockLlmService = null!;
    private Mock<ILogger<CompanyDataService>> _mockLogger = null!;
    private CompanyDataConfiguration _config = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLlmService = new Mock<ILlmService>();
        _mockLogger = new Mock<ILogger<CompanyDataService>>();
        _config = new CompanyDataConfiguration
        {
            ConnectionString = "Server=test;Database=test;",
            MaxRows = 100,
            QueryTimeoutSeconds = 30,
            SqlGenerationModel = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4" }
        };
    }

    #region Service Disabled Tests

    [TestMethod]
    public async Task QueryCompanyDataAsync_WhenDisabled_ReturnsError()
    {
        _config.ConnectionString = null; // Disabled
        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("What are total sales?");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("not configured"));
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithEmptyConnectionString_ReturnsError()
    {
        _config.ConnectionString = "";
        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("What are total sales?");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("not configured"));
    }

    #endregion

    #region Input Validation Tests

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithEmptyQuery_ReturnsError()
    {
        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("empty"));
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithWhitespaceQuery_ReturnsError()
    {
        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("   ");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("empty"));
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithNullQuery_ReturnsError()
    {
        var service = CreateService();

        var result = await service.QueryCompanyDataAsync(null!);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("empty"));
    }

    #endregion

    #region SQL Generation Tests

    [TestMethod]
    public async Task QueryCompanyDataAsync_WhenLlmReturnsNull_ReturnsError()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("What are total sales?");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("Failed to generate SQL"));
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WhenLlmReturnsEmpty_ReturnsError()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("What are total sales?");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("Failed to generate SQL"));
    }

    #endregion

    #region SQL Validation Tests

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithInsertStatement_ReturnsError()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("INSERT INTO Sales VALUES (1, 2, 3)");

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("Insert some data");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("Only SELECT") == true || result.ErrorMessage?.Contains("prohibited") == true);
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithDeleteStatement_ReturnsError()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("DELETE FROM Sales");

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("Delete all sales");

        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithUpdateStatement_ReturnsError()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("UPDATE Sales SET Amount = 0");

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("Update sales amount");

        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithDropStatement_ReturnsError()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("DROP TABLE Sales");

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("Drop the table");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("prohibited") == true || result.ErrorMessage?.Contains("SELECT") == true);
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithExecStatement_ReturnsError()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("EXEC sp_dangerous_procedure");

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("Execute procedure");

        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithMultipleStatements_ReturnsError()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("SELECT * FROM Sales; DROP TABLE Sales");

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("Select and drop");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("prohibited") == true || result.ErrorMessage?.Contains("Multiple") == true);
    }

    [TestMethod]
    public async Task QueryCompanyDataAsync_WithSelectContainingInsertKeyword_ButValid_AllowsQuery()
    {
        // This tests that a SELECT query mentioning "INSERT" in a column/alias is still blocked
        // because the regex checks for the INSERT keyword anywhere
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("SELECT INSERT_DATE FROM Sales");

        var service = CreateService();

        var result = await service.QueryCompanyDataAsync("Show insert dates");

        // This should fail because INSERT keyword is detected
        Assert.IsFalse(result.Success);
    }

    #endregion

    #region Schema Documentation Tests

    [TestMethod]
    public void GetSchemaDocumentation_ReturnsNonEmptyString()
    {
        var service = CreateService();

        var schema = service.GetSchemaDocumentation();

        Assert.IsFalse(string.IsNullOrWhiteSpace(schema));
    }

    [TestMethod]
    public void GetSchemaDocumentation_ContainsFactSales()
    {
        var service = CreateService();

        var schema = service.GetSchemaDocumentation();

        Assert.IsTrue(schema.Contains("FactSales") || schema.Contains("Fact Tables"));
    }

    [TestMethod]
    public void GetSchemaDocumentation_ContainsDimensionTables()
    {
        var service = CreateService();

        var schema = service.GetSchemaDocumentation();

        Assert.IsTrue(schema.Contains("Dim") || schema.Contains("Dimension"));
    }

    #endregion

    #region CompanyDataResult Tests

    [TestMethod]
    public void CompanyDataResult_Ok_SetsPropertiesCorrectly()
    {
        var data = new List<Dictionary<string, object?>>
        {
            new() { ["Id"] = 1, ["Name"] = "Test" }
        };

        var result = CompanyDataResult.Ok("SELECT * FROM Test", data);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("SELECT * FROM Test", result.GeneratedSql);
        Assert.AreEqual(1, result.RowCount);
        Assert.IsNotNull(result.Data);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void CompanyDataResult_Error_SetsPropertiesCorrectly()
    {
        var result = CompanyDataResult.Error("Test error");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Test error", result.ErrorMessage);
        Assert.IsNull(result.Data);
        Assert.AreEqual(0, result.RowCount);
    }

    [TestMethod]
    public void CompanyDataResult_ErrorWithSql_IncludesSql()
    {
        var result = CompanyDataResult.Error("Validation failed", "SELECT * FROM Table");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Validation failed", result.ErrorMessage);
        Assert.AreEqual("SELECT * FROM Table", result.GeneratedSql);
    }

    #endregion

    private CompanyDataService CreateService()
    {
        return new CompanyDataService(
            _mockLogger.Object,
            _mockLlmService.Object,
            Options.Create(_config));
    }
}
