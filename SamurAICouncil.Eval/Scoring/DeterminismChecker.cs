using System.Globalization;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Eval.Scoring;

/// <summary>
/// Measures run-to-run stability across N recommendations for the same input:
/// the fraction of runs that agree with the modal form, and with the modal value signature.
/// Studio is deterministic by construction (a single run scores 1.0).
/// </summary>
public static class DeterminismChecker
{
    public static (double FormStability, double ValueStability) Measure(IReadOnlyList<ChartRecommendation> runs)
    {
        if (runs.Count <= 1)
        {
            return (1.0, 1.0);
        }

        var formStability = ModalFraction(runs.Select(r => r.Type.ToString()));
        var valueStability = ModalFraction(runs.Select(ValueSignature));
        return (formStability, valueStability);
    }

    private static double ModalFraction(IEnumerable<string> keys)
    {
        var list = keys.ToList();
        var modal = list.GroupBy(k => k).Max(g => g.Count());
        return (double)modal / list.Count;
    }

    /// <summary>Order-independent canonical signature of the plotted numbers (rounded).</summary>
    private static string ValueSignature(ChartRecommendation rec)
    {
        var nums = new List<double>();
        foreach (var s in rec.Series)
        {
            if (s.Values is { Length: > 0 }) nums.AddRange(s.Values);
            if (s.Points is { Length: > 0 }) foreach (var p in s.Points) { nums.Add(p.X); nums.Add(p.Y); }
        }
        if (rec.Stats is { Length: > 0 }) nums.AddRange(rec.Stats.Select(st => st.Value));

        var rounded = nums.Select(n => Math.Round(n, 4).ToString("0.####", CultureInfo.InvariantCulture)).OrderBy(x => x);
        return rec.Type + "|" + string.Join(",", rounded);
    }
}
