using SamurAICouncil.Core.Models;
using SamurAICouncil.Eval.Models;
using SamurAICouncil.Eval.Scoring;

namespace SamurAICouncil.Eval.Tests;

[TestClass]
public class FormMatcherTests
{
    private static EvalCase Case(string expected, params string[] acceptable)
        => new() { ExpectedForm = expected, AcceptableForms = [.. acceptable] };

    private static ChartRecommendation Rec(ChartType t) => new() { Type = t };

    [TestMethod]
    public void Exact_form_matches()
    {
        var (ok, note) = FormMatcher.Match(Rec(ChartType.Bar), Case("Bar"));
        Assert.IsTrue(ok);
        Assert.AreEqual("exact", note);
    }

    [TestMethod]
    public void Acceptable_form_matches()
    {
        var (ok, _) = FormMatcher.Match(Rec(ChartType.HorizontalBar), Case("Bar", "HorizontalBar"));
        Assert.IsTrue(ok);
    }

    [TestMethod]
    public void Wrong_form_is_rejected()
    {
        var (ok, _) = FormMatcher.Match(Rec(ChartType.Pie), Case("Bar", "HorizontalBar"));
        Assert.IsFalse(ok);
    }
}
