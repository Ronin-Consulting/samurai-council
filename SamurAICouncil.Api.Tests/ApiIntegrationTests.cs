using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Api.Tests;

/// <summary>Fake council so the API can be tested without an LLM key or database.</summary>
internal sealed class FakeCouncil : ICouncilService
{
    public Task<List<Stage1Response>> Stage1CollectResponsesAsync(string userQuery, CancellationToken ct = default)
        => Task.FromResult(new List<Stage1Response>
        {
            new() { Model = "test/a", Response = "Answer A" },
            new() { Model = "test/b", Response = "Answer B" },
        });

    public Task<(List<Stage2Ranking> Rankings, Dictionary<string, string> LabelToModel)> Stage2CollectRankingsAsync(
        string userQuery, List<Stage1Response> stage1, CancellationToken ct = default)
        => Task.FromResult((
            new List<Stage2Ranking> { new() { Model = "test/a", Ranking = "1. Response A", ParsedRanking = ["Response A", "Response B"] } },
            new Dictionary<string, string> { ["Response A"] = "test/a", ["Response B"] = "test/b" }));

    public Task<Stage3Response> Stage3SynthesizeFinalAsync(
        string userQuery, List<Stage1Response> stage1, List<Stage2Ranking> stage2, CancellationToken ct = default)
        => Task.FromResult(new Stage3Response { Model = "chair", Response = "Final synthesized answer." });

    public Task<CouncilResult> RunFullCouncilAsync(string userQuery, CancellationToken ct = default)
        => Task.FromResult(new CouncilResult());

    public Task<string> GenerateConversationTitleAsync(string userQuery, CancellationToken ct = default)
        => Task.FromResult("Test Title");
}

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // no appsettings.Development.json => persistence disabled, no real LLM
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICouncilService>();
            services.AddSingleton<ICouncilService, FakeCouncil>();
        });
    }
}

[TestClass]
public class ApiIntegrationTests
{
    private static ApiFactory _factory = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _factory = new ApiFactory();

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    [TestMethod]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        Assert.AreEqual(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        StringAssert.Contains(body, "ok");
    }

    [TestMethod]
    public async Task ListConversations_Empty_WhenPersistenceDisabled()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/conversations");
        Assert.AreEqual(HttpStatusCode.OK, res.StatusCode);
        var body = (await res.Content.ReadAsStringAsync()).Trim();
        Assert.AreEqual("[]", body);
    }

    [TestMethod]
    public async Task CreateConversation_ReturnsConversation()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/conversations", new { });
        Assert.IsTrue(res.IsSuccessStatusCode, $"status {res.StatusCode}");
        var body = await res.Content.ReadAsStringAsync();
        StringAssert.Contains(body, "\"id\"");
        StringAssert.Contains(body, "\"title\"");
    }

    [TestMethod]
    public async Task SendMessage_StreamsStagesInOrder()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            new { content = "hello" });

        Assert.IsTrue(res.IsSuccessStatusCode, $"status {res.StatusCode}");
        var stream = await res.Content.ReadAsStringAsync();

        StringAssert.Contains(stream, "event: stage1");
        StringAssert.Contains(stream, "event: stage2");
        StringAssert.Contains(stream, "event: stage3");
        StringAssert.Contains(stream, "event: done");

        // Stage ordering
        var i1 = stream.IndexOf("event: stage1", StringComparison.Ordinal);
        var i3 = stream.IndexOf("event: stage3", StringComparison.Ordinal);
        var iDone = stream.IndexOf("event: done", StringComparison.Ordinal);
        Assert.IsTrue(i1 < i3 && i3 < iDone, "events must arrive stage1 -> stage3 -> done");

        // Final answer payload present
        StringAssert.Contains(stream, "Final synthesized answer.");
    }
}
