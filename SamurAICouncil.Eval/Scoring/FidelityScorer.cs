using System.Globalization;
using System.Text.RegularExpressions;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Eval.Models;

namespace SamurAICouncil.Eval.Scoring;

/// <summary>
/// Measures how faithfully a <see cref="ChartRecommendation"/>'s plotted values/labels
/// trace back to the source rows. Studio binds raw rows (≈1.0 by construction); Classic
/// transcribes via the LLM, so this exposes drift/hallucination. Derived values
/// (percentages) are recognised so Classic isn't unfairly penalised.
/// </summary>
public static partial class FidelityScorer
{
    private const double RelTolerance = 1e-3;

    public static FidelityResult Score(ChartRecommendation rec, IReadOnlyList<Dictionary<string, object?>> rows, string query)
    {
        var sourceNumbers = new List<double>();
        var sourceLabels = new HashSet<string>();
        foreach (var row in rows)
        {
            foreach (var kv in row)
            {
                sourceLabels.Add(Norm(kv.Key)); // column names are valid labels (series/stat/table headers)
                var d = ToDouble(kv.Value);
                if (d is not null)
                {
                    sourceNumbers.Add(d.Value);
                }
                sourceLabels.Add(Norm(CellLabel(kv.Value)));
            }
        }

        var plottedNumbers = new List<double>();
        var plottedLabels = new List<string>();

        foreach (var l in rec.Labels) plottedLabels.Add(l);
        foreach (var s in rec.Series)
        {
            plottedLabels.Add(s.Name);
            if (s.Values is { Length: > 0 }) plottedNumbers.AddRange(s.Values);
            if (s.Points is { Length: > 0 })
            {
                foreach (var p in s.Points) { plottedNumbers.Add(p.X); plottedNumbers.Add(p.Y); if (p.Label is not null) plottedLabels.Add(p.Label); }
            }
        }
        if (rec.Stats is { Length: > 0 })
        {
            foreach (var st in rec.Stats) { plottedNumbers.Add(st.Value); plottedLabels.Add(st.Label); }
        }
        if (rec.Table is not null)
        {
            foreach (var col in rec.Table.Columns) plottedLabels.Add(col);
            foreach (var r in rec.Table.Rows)
                foreach (var cell in r)
                {
                    var n = ParseNumber(cell);
                    if (n is not null) plottedNumbers.Add(n.Value); else plottedLabels.Add(cell);
                }
        }

        var matchedNumbers = 0;
        var unmatched = new List<double>();
        foreach (var p in plottedNumbers)
        {
            if (sourceNumbers.Any(s => Close(p, s))) matchedNumbers++;
            else unmatched.Add(p);
        }

        var matchedLabels = plottedLabels.Count(l => sourceLabels.Contains(Norm(l)));

        var likelyDerived = LooksDerived(query, plottedNumbers);

        return new FidelityResult(
            PlottedNumbers: plottedNumbers.Count,
            MatchedNumbers: matchedNumbers,
            PlottedLabels: plottedLabels.Count,
            MatchedLabels: matchedLabels,
            UnmatchedNumbers: unmatched,
            LikelyDerived: likelyDerived,
            HasPlottedData: plottedNumbers.Count > 0 || plottedLabels.Count > 0);
    }

    private static bool Close(double a, double b) => Math.Abs(a - b) <= RelTolerance * Math.Max(1.0, Math.Abs(b));

    private static bool LooksDerived(string query, List<double> plotted)
    {
        if (PercentQuery().IsMatch(query)) return true;
        if (plotted.Count == 0) return false;
        // A percentage-style chart: every value in [0,100] and they sum to ~100.
        if (plotted.All(v => v >= -0.001 && v <= 100.001))
        {
            var sum = plotted.Sum();
            if (sum is >= 95 and <= 105) return true;
        }
        return false;
    }

    internal static double? ToDouble(object? value) => value switch
    {
        null => null,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        short s => s,
        decimal m => (double)m,
        bool => null,
        DateTime => null,
        _ => double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : null,
    };

    private static double? ParseNumber(string? s)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : null;

    private static string CellLabel(object? value) => value switch
    {
        null => "",
        DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        double or float or decimal or int or long or short => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static string Norm(string s) => WhitespaceUnderscore().Replace(s ?? "", "").ToLowerInvariant();

    [GeneratedRegex(@"percent|percentage|share|proportion|composition|of total|%")]
    private static partial Regex PercentQuery();

    [GeneratedRegex(@"[\s_]+")]
    private static partial Regex WhitespaceUnderscore();
}
