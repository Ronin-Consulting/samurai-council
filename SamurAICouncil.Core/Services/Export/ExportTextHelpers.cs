using SamurAICouncil.Core.Services.Export.Model;

namespace SamurAICouncil.Core.Services.Export;

/// <summary>Small text helpers shared by <see cref="ReportDocumentBuilder"/> and all three renderers.</summary>
public static class ExportTextHelpers
{
    public static string GetShortModelName(string model)
    {
        if (string.IsNullOrEmpty(model))
            return "Unknown";

        var parts = model.Split('/');
        var modelPart = parts.Length > 1 ? parts[^1] : model;

        return modelPart switch
        {
            var m when m.StartsWith("gpt-4o") => "GPT-4o",
            var m when m.StartsWith("gpt-4") => "GPT-4",
            var m when m.StartsWith("gpt-3.5") => "GPT-3.5",
            var m when m.StartsWith("claude-3-opus") => "Claude Opus",
            var m when m.StartsWith("claude-3-sonnet") => "Claude Sonnet",
            var m when m.StartsWith("claude-3.5-sonnet") => "Claude 3.5",
            var m when m.StartsWith("claude-sonnet-4") => "Claude Sonnet 4",
            var m when m.StartsWith("claude-3-haiku") => "Claude Haiku",
            var m when m.StartsWith("gemini-2") => "Gemini 2",
            var m when m.StartsWith("gemini-1.5-pro") => "Gemini 1.5 Pro",
            var m when m.StartsWith("gemini-1.5-flash") => "Gemini 1.5 Flash",
            var m when m.StartsWith("gemini-pro") => "Gemini Pro",
            var m when m.StartsWith("gemini-1.5") => "Gemini 1.5",
            _ => modelPart.Length > 25 ? modelPart[..25] + "..." : modelPart
        };
    }

    public static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }

    /// <summary>
    /// Truncates a parsed block list to approximately <paramref name="maxChars"/> of text, so each
    /// renderer can apply its own (currently different) limit to the SAME parsed structure instead
    /// of truncating raw markdown before parsing (which would defeat the point of a shared parse).
    /// Drops blocks once the budget is exhausted and appends an ellipsis run to the last kept block.
    /// </summary>
    public static IReadOnlyList<ReportBlock> TruncateBlocks(IReadOnlyList<ReportBlock> blocks, int maxChars)
    {
        var result = new List<ReportBlock>();
        var remaining = maxChars;

        foreach (var block in blocks)
        {
            if (remaining <= 0)
                break;

            var (truncated, consumed, hitLimit) = TruncateBlock(block, remaining);
            if (truncated != null)
                result.Add(truncated);
            remaining -= consumed;

            if (hitLimit)
                break;
        }

        return result;
    }

    private static (ReportBlock? Block, int Consumed, bool HitLimit) TruncateBlock(ReportBlock block, int maxChars)
    {
        switch (block)
        {
            case ParagraphBlock p:
                var (runs, consumed, hit) = TruncateRuns(p.Runs, maxChars);
                return (runs.Count > 0 ? new ParagraphBlock(runs) : null, consumed, hit);

            case HeadingBlock h:
                var (hRuns, hConsumed, hHit) = TruncateRuns(h.Runs, maxChars);
                return (hRuns.Count > 0 ? new HeadingBlock(h.Level, hRuns) : null, hConsumed, hHit);

            default:
                // Lists/tables/cards/images are kept whole or dropped - splitting them mid-structure
                // reads worse than a slightly-over-budget block. Estimate their size from a rough text length.
                var length = EstimateLength(block);
                return length <= maxChars ? (block, length, false) : (null, 0, true);
        }
    }

    private static (List<InlineRun> Runs, int Consumed, bool HitLimit) TruncateRuns(IReadOnlyList<InlineRun> runs, int maxChars)
    {
        var result = new List<InlineRun>();
        var remaining = maxChars;

        foreach (var run in runs)
        {
            if (remaining <= 0)
                return (result, maxChars - remaining, true);

            if (run.Text.Length <= remaining)
            {
                result.Add(run);
                remaining -= run.Text.Length;
            }
            else
            {
                result.Add(run with { Text = run.Text[..remaining] + "..." });
                remaining = 0;
                return (result, maxChars, true);
            }
        }

        return (result, maxChars - remaining, false);
    }

    private static int EstimateLength(ReportBlock block) => block switch
    {
        BulletListBlock b => b.Items.Sum(i => i.Sum(r => r.Text.Length)),
        NumberedListBlock n => n.Items.Sum(i => i.Sum(r => r.Text.Length)),
        TableBlock t => t.Headers.Sum(h => h.Length) + t.Rows.Sum(r => r.Sum(c => c.Length)),
        MonospaceBlock m => m.Text.Length,
        CardBlock c => c.Content.Sum(EstimateLength),
        _ => 0,
    };
}
