using System.Text.Json;
using Moq;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

[TestClass]
public class CompanyDataToolTests
{
    // CompanyDataTool is registered as a Singleton (Program.cs) and its ExecuteAsync is invoked
    // concurrently by design: CouncilService.Stage1CollectResponsesAsync runs every council model's
    // tool-calling turn in parallel via Task.WhenAll, and the same singleton instance also serves
    // every conversation/browser tab on the server. This test drives many concurrent ExecuteAsync
    // calls, each for a distinct query, and asserts each call's response contains ITS OWN chart —
    // not one leaked in from a concurrently-running call.
    [TestMethod]
    public async Task ExecuteAsync_ConcurrentCalls_EachReturnsItsOwnChart()
    {
        const int concurrency = 100;
        var mockDataService = new Mock<ICompanyDataService>();

        mockDataService
            .Setup(s => s.QueryCompanyDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (query, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 5), ct);
                return CompanyDataResult.Ok("SELECT 1", new List<Dictionary<string, object?>>
                {
                    new() { ["value"] = 1 }
                });
            });

        mockDataService
            .Setup(s => s.GenerateChartRecommendationAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Dictionary<string, object?>>>(), It.IsAny<CancellationToken>()))
            .Returns<string, IReadOnlyList<Dictionary<string, object?>>, CancellationToken>(async (query, _, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 5), ct);
                // Title carries the originating query, so a leaked chart from another call is detectable.
                return new ChartRecommendation { Title = query };
            });

        var tool = new CompanyDataTool(mockDataService.Object, new StudioClassifier());

        var tasks = Enumerable.Range(0, concurrency).Select(async i =>
        {
            var query = $"query-{i}";
            var input = JsonSerializer.SerializeToElement(new { query });
            var resultJson = await tool.ExecuteAsync(input);
            var chartTitle = JsonDocument.Parse(resultJson).RootElement.GetProperty("chart").GetProperty("title").GetString();
            return (query, chartTitle);
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (query, chartTitle) in results)
        {
            Assert.AreEqual(query, chartTitle,
                $"Expected the chart generated for '{query}' but got a chart titled '{chartTitle}' instead — " +
                "chart data leaked in from a concurrently-running ExecuteAsync call.");
        }
    }
}
