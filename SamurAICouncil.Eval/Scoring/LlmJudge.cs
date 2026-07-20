using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Eval.Models;

namespace SamurAICouncil.Eval.Scoring;

/// <summary>
/// LLM-as-judge: presents both recommendations BLIND (randomised A/B per case) to a strong
/// model and asks which better fits the query + data. De-references the verdict back to
/// Classic/Studio so position bias can't favour either mode.
/// </summary>
public sealed class LlmJudge(ILlmService llm, ModelConfiguration judgeModel)
{
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    public async Task<(JudgeVerdict? Verdict, double LatencyMs, long EstTokens)> JudgeAsync(
        EvalCase c,
        IReadOnlyList<Dictionary<string, object?>> rows,
        ChartRecommendation classic,
        ChartRecommendation studio,
        CancellationToken ct = default)
    {
        // Blind A/B: deterministic per case id (stable across runs, independent of position).
        var aIsClassic = (StableHash(c.Id) & 1) == 0;
        var recA = aIsClassic ? classic : studio;
        var recB = aIsClassic ? studio : classic;

        var prompt = BuildPrompt(c.Query, rows, recA, recB);
        var messages = new[]
        {
            ChatMessage.System(
                "You are a data-visualization expert judging which of two chart specifications better answers a user's question " +
                "about a result set. Consider: is the chart FORM appropriate for the data's shape and the question's intent, and is " +
                "the result CLEAR and honest? Respond with STRICT JSON only, no prose, no code fences: " +
                "{\"winner\":\"A\"|\"B\"|\"tie\",\"a\":{\"appropriateness\":1-5,\"clarity\":1-5},\"b\":{\"appropriateness\":1-5,\"clarity\":1-5},\"rationale\":\"one sentence\"}"),
            ChatMessage.User(prompt),
        };

        var sw = Stopwatch.StartNew();
        var response = await llm.QueryModelAsync(judgeModel, messages, ct);
        sw.Stop();

        var estTokens = (prompt.Length + (response?.Length ?? 0)) / 4;
        var verdict = Parse(response, aIsClassic);
        return (verdict, sw.Elapsed.TotalMilliseconds, estTokens);
    }

    private static string BuildPrompt(string query, IReadOnlyList<Dictionary<string, object?>> rows, ChartRecommendation a, ChartRecommendation b)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User question: {query}").AppendLine();
        sb.AppendLine("Result set (first rows):");
        if (rows.Count == 0)
        {
            sb.AppendLine("(empty result)");
        }
        else
        {
            var cols = rows[0].Keys.ToList();
            sb.AppendLine(string.Join(" | ", cols));
            foreach (var r in rows.Take(10))
            {
                sb.AppendLine(string.Join(" | ", cols.Select(col => Fmt(r.GetValueOrDefault(col)))));
            }
            if (rows.Count > 10) sb.AppendLine($"... ({rows.Count} rows total)");
        }
        sb.AppendLine();
        sb.AppendLine("Chart A:").AppendLine(JsonSerializer.Serialize(a, Compact));
        sb.AppendLine("Chart B:").AppendLine(JsonSerializer.Serialize(b, Compact));
        return sb.ToString();
    }

    private static string Fmt(object? v) => v switch
    {
        null => "",
        DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "",
    };

    private static JudgeVerdict? Parse(string? response, bool aIsClassic)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        var json = Strip(response);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var winnerAb = root.GetProperty("winner").GetString()?.Trim().ToLowerInvariant() ?? "tie";
            var (aApp, aClr) = ReadScores(root, "a");
            var (bApp, bClr) = ReadScores(root, "b");
            var rationale = root.TryGetProperty("rationale", out var r) ? r.GetString() ?? "" : "";

            // De-reference A/B back to Classic/Studio.
            var winner = winnerAb switch
            {
                "a" => aIsClassic ? "Classic" : "Studio",
                "b" => aIsClassic ? "Studio" : "Classic",
                _ => "Tie",
            };
            var (classicApp, classicClr, studioApp, studioClr) = aIsClassic
                ? (aApp, aClr, bApp, bClr)
                : (bApp, bClr, aApp, aClr);

            return new JudgeVerdict(winner, classicApp, classicClr, studioApp, studioClr, rationale);
        }
        catch
        {
            return null;
        }
    }

    private static (int App, int Clarity) ReadScores(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var obj)) return (0, 0);
        var app = obj.TryGetProperty("appropriateness", out var a) && a.TryGetInt32(out var ai) ? ai : 0;
        var clr = obj.TryGetProperty("clarity", out var c) && c.TryGetInt32(out var ci) ? ci : 0;
        return (app, clr);
    }

    private static string Strip(string s)
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
        return start >= 0 && end > start ? s[start..(end + 1)] : s.Trim();
    }

    private static uint StableHash(string s)
    {
        // FNV-1a — stable across runs/processes (unlike string.GetHashCode).
        uint h = 2166136261;
        foreach (var ch in s) { h ^= ch; h *= 16777619; }
        return h;
    }
}
