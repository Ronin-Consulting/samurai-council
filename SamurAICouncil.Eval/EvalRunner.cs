using System.Diagnostics;
using System.Text.Json;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Eval.Models;
using SamurAICouncil.Eval.Scoring;

namespace SamurAICouncil.Eval;

/// <summary>Runs both form-choosers over the corpus and scores each case.</summary>
public sealed class EvalRunner(EvalHost host)
{
    public async Task<Scorecard> RunAsync(
        List<EvalCase> cases, string suite, bool judge, int runs, string judgeModelId, string stamp, CancellationToken ct)
    {
        var card = new Scorecard
        {
            GeneratedAt = stamp,
            Suite = suite,
            JudgeEnabled = judge,
            Runs = runs,
            JudgeModel = judge ? judgeModelId : "(disabled)",
        };
        var judgeSvc = judge ? new LlmJudge(host.Llm, host.JudgeModel) : null;

        var n = 0;
        foreach (var c in cases)
        {
            n++;
            Console.Error.WriteLine($"[{n}/{cases.Count}] {c.Suite}:{c.Id}");
            card.Cases.Add(await RunCaseAsync(c, judgeSvc, Math.Max(1, runs), ct));
        }
        return card;
    }

    private async Task<CaseResult> RunCaseAsync(EvalCase c, LlmJudge? judge, int runs, CancellationToken ct)
    {
        var cr = new CaseResult
        {
            Id = c.Id, Query = c.Query, Suite = c.Suite, EdgeCategory = c.EdgeCategory,
            ExpectedForm = c.ExpectedForm, AcceptableForms = c.AcceptableForms,
        };

        // 1. Resolve rows (live → real SQL; fixture → materialize inline).
        IReadOnlyList<Dictionary<string, object?>> rows;
        if (c.Suite == "live")
        {
            if (!host.CompanyDataConfig.IsEnabled)
            {
                return Skip(cr, "CompanyData connection string not configured");
            }
            try
            {
                var res = await host.CompanyData.QueryCompanyDataAsync(c.Query, ct);
                if (!res.Success || res.Data is null)
                {
                    return Skip(cr, $"live query failed: {res.ErrorMessage ?? "no data"}");
                }
                rows = res.Data;
            }
            catch (Exception ex)
            {
                return Skip(cr, $"live query threw: {ex.Message}");
            }
        }
        else
        {
            rows = c.Materialize();
        }
        cr.RowCount = rows.Count;

        // 2. Studio — deterministic, single run.
        var swStudio = Stopwatch.StartNew();
        var studioRec = host.Studio.Classify(c.Query, rows);
        swStudio.Stop();
        FillMode(cr.Studio, studioRec, [studioRec], c, rows);
        cr.Studio.LatenciesMs = [swStudio.Elapsed.TotalMilliseconds];
        cr.Studio.LlmCalls = 0;

        // 3. Classic — N runs (LLM), timed.
        var classicRuns = new List<ChartRecommendation>();
        long classicTokens = 0;
        string? classicError = null;
        for (var i = 0; i < runs; i++)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var rec = await host.CompanyData.GenerateChartRecommendationAsync(c.Query, rows, ct)
                          ?? new ChartRecommendation { Type = ChartType.None };
                sw.Stop();
                cr.Classic.LatenciesMs.Add(sw.Elapsed.TotalMilliseconds);
                classicRuns.Add(rec);
                classicTokens += EstimateTokens(c.Query, rows, rec);
            }
            catch (Exception ex)
            {
                classicError = ex.Message;
                classicRuns.Add(new ChartRecommendation { Type = ChartType.None });
                cr.Classic.LatenciesMs.Add(0);
            }
        }
        FillMode(cr.Classic, classicRuns[0], classicRuns, c, rows);
        cr.Classic.LlmCalls = classicRuns.Count;
        cr.Classic.EstTokens = classicTokens;
        cr.Classic.Error = classicError;

        // 4. Judge (blind A/B).
        if (judge is not null)
        {
            try
            {
                var (verdict, _, _) = await judge.JudgeAsync(c, rows, classicRuns[0], studioRec, ct);
                cr.Judge = verdict;
            }
            catch (Exception ex)
            {
                cr.Judge = new JudgeVerdict("Tie", 0, 0, 0, 0, $"judge error: {ex.Message}");
            }
        }

        return cr;
    }

    private void FillMode(ModeResult m, ChartRecommendation primary, IReadOnlyList<ChartRecommendation> runs,
        EvalCase c, IReadOnlyList<Dictionary<string, object?>> rows)
    {
        m.Recommendation = primary;
        m.Form = primary.Type.ToString();
        m.Valid = host.Transformer.IsValidChart(primary);

        var (ok, note) = FormMatcher.Match(primary, c);
        m.FormMatch = ok;
        m.FormNote = note;

        m.Fidelity = FidelityScorer.Score(primary, rows, c.Query);

        m.RunForms = runs.Select(r => r.Type.ToString()).ToList();
        var (formStab, valueStab) = DeterminismChecker.Measure(runs);
        m.FormStability = formStab;
        m.ValueStability = valueStab;
    }

    private static CaseResult Skip(CaseResult cr, string reason)
    {
        cr.Skipped = true;
        cr.SkipReason = reason;
        return cr;
    }

    private static long EstimateTokens(string query, IReadOnlyList<Dictionary<string, object?>> rows, ChartRecommendation rec)
    {
        var recChars = JsonSerializer.Serialize(rec).Length;
        var rowChars = rows.Count * (rows.Count > 0 ? rows[0].Count : 1) * 8;
        return (query.Length + recChars + rowChars) / 4; // rough estimate — service exposes no real usage
    }
}
