using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Eval.Models;

/// <summary>
/// Data-fidelity signal for one recommendation vs the source rows. We report the
/// verbatim match rate (numbers/labels found in the raw rows) as a signal — Classic
/// legitimately derives values (percentages, rounding), so a low rate is not automatically
/// "wrong"; <see cref="HallucinatedCount"/> only counts unmatched numbers that are NOT
/// plausibly derived.
/// </summary>
public sealed record FidelityResult(
    int PlottedNumbers,
    int MatchedNumbers,
    int PlottedLabels,
    int MatchedLabels,
    IReadOnlyList<double> UnmatchedNumbers,
    bool LikelyDerived,
    bool HasPlottedData)
{
    public double NumberMatchRate => PlottedNumbers == 0 ? 1.0 : (double)MatchedNumbers / PlottedNumbers;
    public double LabelMatchRate => PlottedLabels == 0 ? 1.0 : (double)MatchedLabels / PlottedLabels;
    public int HallucinatedCount => LikelyDerived ? 0 : UnmatchedNumbers.Count;
}

/// <summary>Blind LLM-judge verdict, de-referenced back to Classic/Studio.</summary>
public sealed record JudgeVerdict(
    string Winner, // "Classic" | "Studio" | "Tie"
    int ClassicAppropriateness,
    int ClassicClarity,
    int StudioAppropriateness,
    int StudioClarity,
    string Rationale);

/// <summary>Per-mode result for a single case.</summary>
public sealed class ModeResult
{
    public string Mode { get; set; } = "";
    public string Form { get; set; } = nameof(ChartType.None);
    public bool Valid { get; set; }
    public bool FormMatch { get; set; }
    public string? FormNote { get; set; }
    public FidelityResult? Fidelity { get; set; }

    /// <summary>Forms produced across each run (Classic runs N times; Studio once).</summary>
    public List<string> RunForms { get; set; } = [];
    public double FormStability { get; set; } = 1.0;
    public double ValueStability { get; set; } = 1.0;

    public List<double> LatenciesMs { get; set; } = [];
    public int LlmCalls { get; set; }
    public long EstTokens { get; set; }
    public string? Error { get; set; }

    /// <summary>Primary recommendation (first run) — kept for the report + judge.</summary>
    public ChartRecommendation? Recommendation { get; set; }

    public double AvgLatencyMs => LatenciesMs.Count == 0 ? 0 : LatenciesMs.Average();
}

/// <summary>All scoring for a single case.</summary>
public sealed class CaseResult
{
    public string Id { get; set; } = "";
    public string Query { get; set; } = "";
    public string Suite { get; set; } = "";
    public string EdgeCategory { get; set; } = "";
    public string ExpectedForm { get; set; } = "";
    public List<string> AcceptableForms { get; set; } = [];
    public int RowCount { get; set; }

    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }

    public ModeResult Classic { get; set; } = new() { Mode = "Classic" };
    public ModeResult Studio { get; set; } = new() { Mode = "Studio" };
    public JudgeVerdict? Judge { get; set; }
}

/// <summary>Top-level run result written to results.json.</summary>
public sealed class Scorecard
{
    public string GeneratedAt { get; set; } = "";
    public string Suite { get; set; } = "";
    public bool JudgeEnabled { get; set; }
    public int Runs { get; set; }
    public string JudgeModel { get; set; } = "";
    public List<CaseResult> Cases { get; set; } = [];
}
