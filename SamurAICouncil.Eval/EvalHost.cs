using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Eval;

/// <summary>
/// Constructs the Core services the eval needs, standalone (no ASP.NET host). Mirrors the
/// relevant wiring from SamurAICouncil.Api/Program.cs. Uses a minimal openai-only Council
/// config so SemanticKernelLlmService's ctor key-validation only requires the gateway key.
/// </summary>
public sealed class EvalHost
{
    public required ILlmService Llm { get; init; }
    public required CompanyDataService CompanyData { get; init; }
    public required StudioClassifier Studio { get; init; }
    public required IChartDataTransformer Transformer { get; init; }
    public required CompanyDataConfiguration CompanyDataConfig { get; init; }
    public required ModelConfiguration JudgeModel { get; init; }
    public required string DefaultGenerationModelId { get; init; }

    private const string GatewayEndpoint = "https://llm.fortyau.com/v1";

    public static EvalHost Create(string? judgeModelId)
    {
        LoadDotEnv();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var apiKeys = config.GetSection(LlmApiKeysConfiguration.SectionName).Get<LlmApiKeysConfiguration>() ?? new();
        // The repo .env uses OPENAI_API_KEY (mapped to LlmApiKeys__OpenAI only inside Docker),
        // so fall back to it here; default the endpoint to the FortyAU gateway.
        if (string.IsNullOrWhiteSpace(apiKeys.OpenAI))
            apiKeys.OpenAI = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKeys.OpenAIEndpoint))
            apiKeys.OpenAIEndpoint = GatewayEndpoint;

        var companyData = config.GetSection(CompanyDataConfiguration.SectionName).Get<CompanyDataConfiguration>() ?? new();
        if (string.IsNullOrWhiteSpace(companyData.ConnectionString))
        {
            // Dev default (read-only creds, same as compose.yaml). Live suite skips gracefully if the DB is down.
            companyData.ConnectionString =
                "Server=localhost,1433;Database=ContosoRetailDW;User Id=samurai_reader;Password=Reader!Pass123;TrustServerCertificate=True";
        }
        // CompanyData.SqlGenerationModel (the model Classic uses for chart-recommendation) keeps
        // its configured/default value (provider "openai") and routes through the gateway.

        // Minimal openai-only council — only providers listed here get key-validated.
        var council = new CouncilConfiguration
        {
            CouncilModels = [new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o" }],
            ChairmanModel = new ModelConfiguration { Provider = "openai", ModelId = "claude-opus-4-1" },
            TitleGenerationModel = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" },
        };

        var llm = new SemanticKernelLlmService(
            NullLogger<SemanticKernelLlmService>.Instance, Options.Create(apiKeys), Options.Create(council));
        var cds = new CompanyDataService(
            NullLogger<CompanyDataService>.Instance, llm, Options.Create(companyData));

        return new EvalHost
        {
            Llm = llm,
            CompanyData = cds,
            Studio = new StudioClassifier(),
            Transformer = new ChartDataTransformer(NullLogger<ChartDataTransformer>.Instance),
            CompanyDataConfig = companyData,
            JudgeModel = new ModelConfiguration { Provider = "openai", ModelId = judgeModelId ?? "claude-opus-4-1" },
            DefaultGenerationModelId = "claude-opus-4-1",
        };
    }

    /// <summary>Best-effort: load KEY=VALUE lines from a repo-root .env into the process env.</summary>
    private static void LoadDotEnv()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var envFile = Path.Combine(dir.FullName, ".env");
            if (File.Exists(envFile))
            {
                foreach (var raw in File.ReadAllLines(envFile))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line[..eq].Trim();
                    var val = line[(eq + 1)..].Trim().Trim('"');
                    if (Environment.GetEnvironmentVariable(key) is null)
                        Environment.SetEnvironmentVariable(key, val);
                }
                return;
            }
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))) return;
            dir = dir.Parent;
        }
    }
}
