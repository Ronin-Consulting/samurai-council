using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Eval;
using SamurAICouncil.Eval.Corpus;
using SamurAICouncil.Eval.Models;
using SamurAICouncil.Eval.Reporting;
using SamurAICouncil.Eval.Support;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();
var opts = ParseFlags(args.Skip(1));
var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

try
{
    switch (command)
    {
        case "run":
            return await RunAsync();
        case "generate":
            return await GenerateAsync();
        default:
            PrintUsage();
            return 1;
    }
}
catch (InvalidOperationException ex)
{
    // Most commonly: missing OpenAI/gateway key (SemanticKernelLlmService ctor validation).
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    Console.Error.WriteLine("Set OPENAI_API_KEY (the FortyAU gateway token) in the environment or repo .env.");
    return 2;
}

async Task<int> RunAsync()
{
    var suite = (opts.GetValueOrDefault("suite") ?? "fixtures").ToLowerInvariant();
    var judge = (opts.GetValueOrDefault("judge") ?? "off").ToLowerInvariant() is "on" or "true" or "1";
    var runs = int.TryParse(opts.GetValueOrDefault("runs"), out var r) ? Math.Max(1, r) : 3;
    var judgeModel = opts.GetValueOrDefault("judge-model") ?? "claude-opus-4-1";

    var host = EvalHost.Create(judgeModel);

    var cases = new List<EvalCase>();
    if (suite is "fixtures" or "all") cases.AddRange(CorpusLoader.LoadFixtures());
    if (suite is "live" or "all") cases.AddRange(CorpusLoader.LoadLive());
    if (cases.Count == 0)
    {
        Console.Error.WriteLine($"No cases found for suite '{suite}'. Looked in: {Paths.CorpusDir()}");
        return 2;
    }

    var outDir = opts.GetValueOrDefault("out") ?? Path.Combine(Paths.ReportsRoot(), stamp);
    Console.Error.WriteLine($"Running {cases.Count} case(s) [suite={suite}, runs={runs}, judge={(judge ? judgeModel : "off")}]…");

    var card = await new EvalRunner(host).RunAsync(cases, suite, judge, runs, judgeModel, stamp, CancellationToken.None);
    ScorecardWriter.Write(card, outDir);

    Console.WriteLine();
    Console.WriteLine($"✅ Scorecard written to: {Path.GetFullPath(Path.Combine(outDir, "scorecard.md"))}");
    Console.WriteLine($"   Raw results:        {Path.GetFullPath(Path.Combine(outDir, "results.json"))}");
    return 0;
}

async Task<int> GenerateAsync()
{
    var count = int.TryParse(opts.GetValueOrDefault("count"), out var c) ? Math.Max(1, c) : 20;
    var host = EvalHost.Create(null);
    var outDir = opts.GetValueOrDefault("out") ?? Path.Combine(Paths.CorpusDir(), "fixtures", "generated");
    var model = new ModelConfiguration { Provider = "openai", ModelId = host.DefaultGenerationModelId };

    Console.Error.WriteLine($"Generating {count} candidate case(s) with {model.ModelId}…");
    var file = await new CorpusGenerator(host.Llm, model).GenerateAsync(count, outDir, stamp, CancellationToken.None);

    Console.WriteLine($"✅ Wrote candidate fixtures to: {Path.GetFullPath(file)}");
    Console.WriteLine("   REVIEW these before trusting them — verify each expectedForm and result set, then keep or move into corpus/fixtures/.");
    return 0;
}

static Dictionary<string, string> ParseFlags(IEnumerable<string> args)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var list = args.ToList();
    for (var i = 0; i < list.Count; i++)
    {
        if (!list[i].StartsWith("--")) continue;
        var key = list[i][2..];
        var val = i + 1 < list.Count && !list[i + 1].StartsWith("--") ? list[++i] : "true";
        dict[key] = val;
    }
    return dict;
}

static void PrintUsage()
{
    Console.WriteLine("""
        SamurAICouncil.Eval — compare Classic (LLM) vs Studio (deterministic) chart form-choosers.

        Usage:
          run       --suite fixtures|live|all [--judge on|off] [--runs 3] [--out <dir>] [--judge-model <id>]
          generate  --count <N> [--out <dir>]

        Examples:
          dotnet run --project SamurAICouncil.Eval -- run --suite fixtures --judge off
          dotnet run --project SamurAICouncil.Eval -- run --suite all --judge on
          dotnet run --project SamurAICouncil.Eval -- generate --count 20

        Requires OPENAI_API_KEY (FortyAU gateway token) in the environment or repo .env.
        The live suite additionally needs the ContosoRetailDW SQL Server reachable.
        """);
}
