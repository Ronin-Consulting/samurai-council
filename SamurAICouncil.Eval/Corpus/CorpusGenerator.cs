using System.Text.Json;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Eval.Models;

namespace SamurAICouncil.Eval.Corpus;

/// <summary>
/// LLM-assisted fixture generation. Proposes new eval cases (query + synthetic result shape +
/// expected form) for the coverage matrix and writes them to disk FOR HUMAN REVIEW — generated
/// cases are not trusted until reviewed and moved into the main fixtures folder.
/// </summary>
public sealed class CorpusGenerator(ILlmService llm, ModelConfiguration model)
{
    public async Task<string> GenerateAsync(int count, string outDir, string stamp, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outDir);

        var messages = new[]
        {
            ChatMessage.System(
                "You generate evaluation fixtures for a data-visualization form-chooser. Output STRICT JSON only " +
                "(no prose, no code fences): {\"cases\":[{...}]}. Each case: " +
                "{\"id\":string,\"query\":string,\"edgeCategory\":string,\"expectedForm\":one of " +
                "[None,Bar,HorizontalBar,GroupedBar,StackedBar,Line,Area,Pie,Donut,Scatter,Stat,Table]," +
                "\"acceptableForms\":[...],\"notes\":string," +
                "\"columns\":[{\"name\":string,\"type\":\"number\"|\"date\"|\"text\"}]," +
                "\"rows\":[[cell,...]]}. Rows must align to columns; dates as ISO strings; keep result sets small (<=8 rows)."),
            ChatMessage.User(
                $"Generate {count} varied cases covering these shapes and edge cases: single KPI (1 row, 1-4 numeric), " +
                "category+measure (short and long labels), temporal trend, multi-series grouped, two-dimension pivot " +
                "(magnitude and composition/share), scatter (two measures), wide/detail tables, empty result, all-zeros, " +
                "negatives, nulls, high cardinality, id-like numeric columns (e.g. year), ambiguous intent, and text-only. " +
                "Use realistic retail data (ContosoRetailDW-style: channels, products, stores, months, revenue, quantity)."),
        };

        var response = await llm.QueryModelAsync(model, messages, ct);
        var json = StripFences(response ?? "");

        // Validate it parses into the fixture schema before writing.
        var parsed = JsonSerializer.Deserialize<CaseFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (parsed?.Cases is null || parsed.Cases.Count == 0)
        {
            throw new InvalidOperationException("Model returned no parseable cases. Raw response:\n" + response);
        }

        var outFile = Path.Combine(outDir, $"generated-{stamp}.json");
        File.WriteAllText(outFile, JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true }));
        return outFile;
    }

    private static string StripFences(string s)
    {
        s = s.Trim();
        if (s.StartsWith("```"))
        {
            var nl = s.IndexOf('\n');
            if (nl >= 0) s = s[(nl + 1)..];
            if (s.EndsWith("```")) s = s[..^3];
        }
        var start = s.IndexOf('{');
        var end = s.LastIndexOf('}');
        return start >= 0 && end > start ? s[start..(end + 1)] : s;
    }
}
