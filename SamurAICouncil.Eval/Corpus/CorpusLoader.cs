using System.Text.Json;
using SamurAICouncil.Eval.Models;
using SamurAICouncil.Eval.Support;

namespace SamurAICouncil.Eval.Corpus;

/// <summary>Loads fixture and live eval cases from the corpus directory.</summary>
public static class CorpusLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>All fixture cases from <c>corpus/fixtures/**/*.json</c> (includes generated/).</summary>
    public static List<EvalCase> LoadFixtures()
    {
        var dir = Path.Combine(Paths.CorpusDir(), "fixtures");
        var cases = new List<EvalCase>();
        if (!Directory.Exists(dir))
        {
            return cases;
        }
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(f => f))
        {
            var parsed = JsonSerializer.Deserialize<CaseFile>(File.ReadAllText(file), Options);
            if (parsed?.Cases is null) continue;
            foreach (var c in parsed.Cases) { c.Suite = "fixture"; cases.Add(c); }
        }
        return cases;
    }

    /// <summary>Live NL queries from <c>corpus/live-queries.json</c> (rows resolved at run time).</summary>
    public static List<EvalCase> LoadLive()
    {
        var file = Path.Combine(Paths.CorpusDir(), "live-queries.json");
        if (!File.Exists(file))
        {
            return [];
        }
        var parsed = JsonSerializer.Deserialize<CaseFile>(File.ReadAllText(file), Options);
        var cases = parsed?.Cases ?? [];
        foreach (var c in cases) c.Suite = "live";
        return cases;
    }
}
