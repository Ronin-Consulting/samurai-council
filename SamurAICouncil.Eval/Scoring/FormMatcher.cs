using SamurAICouncil.Core.Models;
using SamurAICouncil.Eval.Models;

namespace SamurAICouncil.Eval.Scoring;

/// <summary>Compares a recommendation's chosen form to the case's expected/acceptable set.</summary>
public static class FormMatcher
{
    public static (bool Ok, string Note) Match(ChartRecommendation rec, EvalCase c)
    {
        var form = rec.Type.ToString();

        if (Eq(form, c.ExpectedForm))
        {
            return (true, "exact");
        }
        if (c.AcceptableForms.Any(a => Eq(a, form)))
        {
            return (true, $"acceptable ({form})");
        }
        return (false, $"got {form}, expected {c.ExpectedForm}");
    }

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
