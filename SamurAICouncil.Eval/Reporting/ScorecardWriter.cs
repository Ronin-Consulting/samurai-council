using System.Globalization;
using System.Text;
using System.Text.Json;
using SamurAICouncil.Eval.Models;

namespace SamurAICouncil.Eval.Reporting;

/// <summary>Writes results.json (raw) and scorecard.md (human summary) for a run.</summary>
public static class ScorecardWriter
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static void Write(Scorecard card, string outDir)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "results.json"), JsonSerializer.Serialize(card, Json));
        File.WriteAllText(Path.Combine(outDir, "scorecard.md"), BuildMarkdown(card));
        AppendRunHistory(card, outDir);
    }

    /// <summary>
    /// Appends one row per run to <c>reports/RUNS.md</c> — a longitudinal log of every run's headline
    /// scores, each linked to its full scorecard. Lets you see the effect of a change across runs.
    /// </summary>
    private static void AppendRunHistory(Scorecard card, string outDir)
    {
        var reportsRoot = Directory.GetParent(Path.GetFullPath(outDir))?.FullName ?? outDir;
        Directory.CreateDirectory(reportsRoot);
        var runsFile = Path.Combine(reportsRoot, "RUNS.md");
        var scored = card.Cases.Where(c => !c.Skipped).ToList();

        string link;
        try { link = Path.GetRelativePath(reportsRoot, Path.Combine(outDir, "scorecard.md")).Replace('\\', '/'); }
        catch { link = "scorecard.md"; }

        var judgeCell = "off";
        if (card.JudgeEnabled)
        {
            var judged = scored.Where(c => c.Judge is not null).ToList();
            var wc = judged.Count(c => c.Judge!.Winner == "Classic");
            var ws = judged.Count(c => c.Judge!.Winner == "Studio");
            judgeCell = $"C{wc}–S{ws}–T{judged.Count - wc - ws}";
        }

        if (!File.Exists(runsFile))
        {
            var head = new StringBuilder();
            head.AppendLine("# Evaluation Run History").AppendLine();
            head.AppendLine("Studio vs Classic — one row per run (newest at the bottom). Paired metrics are **Classic / Studio**; click a run to open its full scorecard.").AppendLine();
            head.AppendLine("| Run | Suite | Cases | Runs | Judge on | Form (C/S) | Fidelity (C/S) | Halluc (C/S) | Valid (C/S) | Latency (C/S) | LLM calls (C) | Judge (C–S–T) |");
            head.AppendLine("|---|---|--:|--:|:--:|---|---|---|---|---|--:|---|");
            File.WriteAllText(runsFile, head.ToString());
        }

        var row = "| " + string.Join(" | ",
            $"[{card.GeneratedAt}]({link})",
            card.Suite,
            card.Cases.Count.ToString(),
            card.Runs.ToString(),
            card.JudgeEnabled ? "yes" : "no",
            $"{Pct(scored, x => x.Classic.FormMatch)} / {Pct(scored, x => x.Studio.FormMatch)}",
            $"{Avg(scored, x => x.Classic.Fidelity?.NumberMatchRate ?? 1)} / {Avg(scored, x => x.Studio.Fidelity?.NumberMatchRate ?? 1)}",
            $"{Sum(scored, x => x.Classic.Fidelity?.HallucinatedCount ?? 0)} / {Sum(scored, x => x.Studio.Fidelity?.HallucinatedCount ?? 0)}",
            $"{Pct(scored, x => x.Classic.Valid)} / {Pct(scored, x => x.Studio.Valid)}",
            $"{Ms(scored, x => x.Classic.AvgLatencyMs)} / {Ms(scored, x => x.Studio.AvgLatencyMs)}",
            scored.Sum(x => x.Classic.LlmCalls).ToString(),
            judgeCell) + " |";
        File.AppendAllText(runsFile, row + Environment.NewLine);
    }

    private static string BuildMarkdown(Scorecard card)
    {
        var scored = card.Cases.Where(c => !c.Skipped).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("# Studio vs Classic — Evaluation Scorecard").AppendLine();
        sb.AppendLine($"- Generated: `{card.GeneratedAt}`");
        sb.AppendLine($"- Suite: **{card.Suite}** · Cases: **{card.Cases.Count}** ({scored.Count} scored, {card.Cases.Count - scored.Count} skipped)");
        sb.AppendLine($"- Classic runs per case: **{card.Runs}** · LLM-judge: **{(card.JudgeEnabled ? card.JudgeModel : "off")}**");
        sb.AppendLine();

        // ---- Overall ----
        sb.AppendLine("## Overall").AppendLine();
        sb.AppendLine("| Metric | Classic | Studio |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| Form appropriateness (matched expected/acceptable) | {Pct(scored, x => x.Classic.FormMatch)} | {Pct(scored, x => x.Studio.FormMatch)} |");
        sb.AppendLine($"| Data fidelity (verbatim value-match rate) | {Avg(scored, x => x.Classic.Fidelity?.NumberMatchRate ?? 1)} | {Avg(scored, x => x.Studio.Fidelity?.NumberMatchRate ?? 1)} |");
        sb.AppendLine($"| Hallucinated values (unmatched, non-derived) | {Sum(scored, x => x.Classic.Fidelity?.HallucinatedCount ?? 0)} | {Sum(scored, x => x.Studio.Fidelity?.HallucinatedCount ?? 0)} |");
        sb.AppendLine($"| Renders validly (IsValidChart) | {Pct(scored, x => x.Classic.Valid)} | {Pct(scored, x => x.Studio.Valid)} |");
        sb.AppendLine($"| Determinism (form stability across runs) | {Avg(scored, x => x.Classic.FormStability)} | 100% (by construction) |");
        sb.AppendLine($"| Avg latency / case | {Ms(scored, x => x.Classic.AvgLatencyMs)} | {Ms(scored, x => x.Studio.AvgLatencyMs)} |");
        sb.AppendLine($"| LLM calls (total) | {scored.Sum(x => x.Classic.LlmCalls)} | 0 |");
        sb.AppendLine($"| Est. tokens (rough) | {scored.Sum(x => x.Classic.EstTokens):N0} | 0 |");
        sb.AppendLine();

        // ---- Judge ----
        if (card.JudgeEnabled)
        {
            var judged = scored.Where(c => c.Judge is not null).ToList();
            var winC = judged.Count(c => c.Judge!.Winner == "Classic");
            var winS = judged.Count(c => c.Judge!.Winner == "Studio");
            var tie = judged.Count - winC - winS;
            sb.AppendLine("## LLM-as-judge (blind A/B)").AppendLine();
            sb.AppendLine($"- Verdicts: **Classic {winC}** · **Studio {winS}** · Tie {tie} (of {judged.Count})");
            sb.AppendLine($"- Mean appropriateness — Classic {Mean(judged, c => c.Judge!.ClassicAppropriateness)}, Studio {Mean(judged, c => c.Judge!.StudioAppropriateness)}");
            sb.AppendLine($"- Mean clarity — Classic {Mean(judged, c => c.Judge!.ClassicClarity)}, Studio {Mean(judged, c => c.Judge!.StudioClarity)}");
            sb.AppendLine();
        }

        // ---- Per-case / edge matrix ----
        sb.AppendLine("## Per-case detail").AppendLine();
        sb.AppendLine("| Case | Edge | Rows | Expected | Classic | fid | valid | Studio | fid | valid | Judge |");
        sb.AppendLine("|---|---|--:|---|---|--:|:--:|---|--:|:--:|---|");
        foreach (var c in card.Cases)
        {
            if (c.Skipped)
            {
                sb.AppendLine($"| {c.Id} | {c.EdgeCategory} | – | {c.ExpectedForm} | _skipped_ | | | | | | {c.SkipReason} |");
                continue;
            }
            sb.AppendLine(string.Join(" | ",
                "| " + c.Id,
                c.EdgeCategory,
                c.RowCount.ToString(),
                c.ExpectedForm,
                Form(c.Classic),
                Rate(c.Classic.Fidelity?.NumberMatchRate),
                Check(c.Classic.Valid),
                Form(c.Studio),
                Rate(c.Studio.Fidelity?.NumberMatchRate),
                Check(c.Studio.Valid),
                (c.Judge?.Winner ?? "–") + " |"));
        }
        sb.AppendLine();

        sb.AppendLine("## Notes").AppendLine();
        sb.AppendLine("- **Data fidelity** is the verbatim value-match rate against the raw rows. Classic legitimately derives values (percentages, rounding), which lowers this without being wrong — the sharper signal is the **hallucinated values** count (unmatched *and* not plausibly derived).");
        sb.AppendLine("- **Studio determinism is 100% by construction** (pure code); Classic's stability is measured across the N runs.");
        sb.AppendLine("- **Token counts are rough estimates** (chars/4) — the LLM service exposes no real usage; latency is measured directly.");
        return sb.ToString();
    }

    private static string Form(ModeResult m) => $"{m.Form} {(m.FormMatch ? "✓" : "✗")}";
    private static string Check(bool b) => b ? "✓" : "✗";
    private static string Rate(double? r) => r is null ? "–" : (r.Value * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static string Pct(List<CaseResult> cs, Func<CaseResult, bool> sel)
        => cs.Count == 0 ? "–" : (cs.Count(sel) * 100.0 / cs.Count).ToString("0", CultureInfo.InvariantCulture) + "%";
    private static string Avg(List<CaseResult> cs, Func<CaseResult, double> sel)
        => cs.Count == 0 ? "–" : (cs.Average(sel) * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    private static int Sum(List<CaseResult> cs, Func<CaseResult, int> sel) => cs.Sum(sel);
    private static string Ms(List<CaseResult> cs, Func<CaseResult, double> sel)
        => cs.Count == 0 ? "–" : cs.Average(sel).ToString("0", CultureInfo.InvariantCulture) + " ms";
    private static string Mean(List<CaseResult> cs, Func<CaseResult, int> sel)
        => cs.Count == 0 ? "–" : cs.Average(sel).ToString("0.0", CultureInfo.InvariantCulture);
}
