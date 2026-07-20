using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// V2 "Studio" mode: deterministically classifies a SQL result's SHAPE (columns/types/row count)
/// into a visualization form and binds the REAL data — no LLM in the presentation path.
/// Produces the same engine-neutral <see cref="ChartRecommendation"/> the Classic path uses,
/// so the pre-made Studio components and the Classic renderer share one data contract.
/// </summary>
public partial class StudioClassifier
{
    private const int MaxBarCategories = 15;
    // Horizontal bars grow vertically instead of squeezing labels on the x-axis, so they
    // tolerate roughly double the categories a vertical bar can before a table reads better.
    private const int MaxHorizontalBarCategories = 30;
    private const int MaxPieSlices = 6;
    // Comfortably covers a 5-year monthly trend (60 points); still bails to Table for denser
    // series (e.g. daily data over a year) where a line chart stops being readable.
    private const int MaxLinePoints = 60;
    private const int MaxScatterPoints = 200;
    private const int MaxTableRows = 100;
    private const int MaxTableColumns = 12;

    [GeneratedRegex(@"year|quarter|month|week|day|date|yr|mon|period|fy", RegexOptions.IgnoreCase)]
    private static partial Regex TemporalNameRegex();

    [GeneratedRegex(@"share|composition|of total|percentage|proportion|breakdown|split|mix", RegexOptions.IgnoreCase)]
    private static partial Regex CompositionRegex();

    [GeneratedRegex(@"\btotal\b", RegexOptions.IgnoreCase)]
    private static partial Regex TotalIntentRegex();

    [GeneratedRegex(@"cumulative|running total|volume|area under", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeIntentRegex();

    /// <summary>
    /// One scoreable option for a decision point in <see cref="Classify"/> — either at the top
    /// level (which shape family applies) or within a family that has genuine internal ambiguity
    /// (e.g. 1 dimension + 1 measure could be a Pie, a Bar, or a Table). <see cref="Score"/> is
    /// only meaningful as a relative rank among candidates compared together in ONE <see
    /// cref="PickBest"/> call — scores are NOT a single cross-cutting utility scale. <see
    /// cref="Build"/> is deferred so only the winner's <see cref="ChartRecommendation"/> is
    /// actually constructed.
    /// </summary>
    private sealed record Candidate(int Score, Func<ChartRecommendation> Build);

    /// <summary>Highest-<see cref="Candidate.Score"/> candidate wins. Every call site must always
    /// include an unconditional Table candidate (score 0) so this never sees an empty sequence.</summary>
    private static ChartRecommendation PickBest(IEnumerable<Candidate> candidates)
        => (candidates.MaxBy(c => c.Score)
            ?? throw new InvalidOperationException("PickBest requires at least one candidate."))
           .Build();

    public ChartRecommendation Classify(string query, IReadOnlyList<Dictionary<string, object?>> data)
    {
        if (data.Count == 0)
        {
            return new ChartRecommendation { Type = ChartType.None };
        }

        var columns = data[0].Keys.ToArray();
        var temporal = columns.Where(c => IsTemporal(c, data)).ToList();
        // Identifier/key columns (…Id, …Key, …Code) are numeric in type but are NOT measures —
        // treat them as dimensions so a listing isn't plotted as if the key were a value.
        var numeric = columns.Where(c => !IsTemporal(c, data) && IsNumeric(c, data) && !IsIdentifier(c)).ToList();
        var dims = columns.Where(c => !numeric.Contains(c)).ToList(); // temporal + text + identifiers
        var rowCount = data.Count;

        // Every decision point below is an explicit score, not "whichever check runs first" —
        // when two shapes are simultaneously plausible (e.g. a single row with exactly 2 numeric
        // columns and 0 dims satisfies both Stat and Scatter's constraints), the higher score
        // wins on purpose, not by accident of ordering. This is what lets a future rule addition
        // slot in anywhere without risking silently shadowing an existing one — the bug class
        // that originally motivated this refactor.
        return PickBest(TopLevelCandidates(query, data, temporal, numeric, dims, rowCount));
    }

    private static IEnumerable<Candidate> TopLevelCandidates(
        string query, IReadOnlyList<Dictionary<string, object?>> data,
        List<string> temporal, List<string> numeric, List<string> dims, int rowCount)
    {
        // single row, only numbers → KPI tiles. Can overlap Scatter (exactly 2 numeric, 0 dims,
        // 1 row) — Stat must win there (a 1-point scatter plot is meaningless).
        if (rowCount == 1 && numeric.Count is >= 1 and <= 4 && dims.Count == 0)
        {
            yield return new Candidate(100, () => BuildStat(data[0], numeric));
        }

        // two measures, no dimension → scatter
        if (numeric.Count == 2 && dims.Count == 0)
        {
            yield return new Candidate(90, () => BuildScatter(query, data, numeric[0], numeric[1]));
        }

        // Redundant temporal columns (e.g. CalendarYear + CalendarMonth + MonthLabel) describe
        // ONE time axis. If a temporal column uniquely orders the rows (or a composite of them
        // does) and every dimension is temporal, it's a trend over that axis — a line — not a
        // 3-dimension table or a pivot. Can overlap the too-many-dims Table candidate (dims>=3)
        // and Pivot (dims==2, both temporal) — Line must win both times to collapse the axis
        // instead of bailing.
        if (numeric.Count is >= 1 and <= 6 && dims.Count >= 1 && dims.All(temporal.Contains)
            && rowCount <= MaxLinePoints && ChooseTimeAxis(temporal, data, rowCount) is { } axis)
        {
            var measures = numeric.Take(6).ToList();
            var lineType = PickLineType(query, measures.Count);
            yield return new Candidate(80, () =>
                BuildCategory(lineType, query, data, axis.LabelSelector, axis.XAxisLabel, measures));
        }

        // two dimensions + one measure → pivot the second dim into series
        if (dims.Count == 2 && numeric.Count == 1)
        {
            yield return new Candidate(60, () => BuildPivot(query, data, dims, numeric[0]));
        }

        // one dimension + one measure → best of {Pie, Bar/HorizontalBar, Table} (see
        // OneDimOneMeasureCandidates — this family's own internal ambiguity, unrelated to the
        // top-level score, is resolved by its own nested PickBest).
        if (dims.Count == 1 && numeric.Count == 1)
        {
            var dim = dims[0];
            yield return new Candidate(50, () => PickBest(
                OneDimOneMeasureCandidates(query, data, dim, numeric[0], temporal.Contains(dim), rowCount)));
        }

        // one dimension + several measures → best of {Pie, Line, Bar/HorizontalBar, GroupedBar, Table}
        if (dims.Count == 1 && numeric.Count >= 2)
        {
            var dim = dims[0];
            yield return new Candidate(50, () => PickBest(
                OneDimManyMeasuresCandidates(query, data, dim, numeric, temporal.Contains(dim), rowCount)));
        }

        // no measure, or too many dimensions → table. Scored low (not 0) so it still beats the
        // universal-fallback Table below when it's the ONLY thing that applies, but always loses
        // to Line when a temporal collapse is also possible for a dims>=3 shape.
        if (numeric.Count == 0 || dims.Count >= 3)
        {
            yield return new Candidate(5, () => BuildTable(data));
        }

        // Universal fallback — always valid, lowest score, so something is always returned even
        // for shapes no named rule above covers (e.g. 2 dims + 2 measures).
        yield return new Candidate(0, () => BuildTable(data));
    }

    /// <summary>
    /// Candidates for a single non-temporal-or-ambiguous dimension + exactly one measure: pie or
    /// donut (proportions, if the query asks for them — see <see cref="PickPieType"/>), a
    /// duplicate-tolerant temporal line/area fallback (see <see cref="PickLineType"/>), or a bar.
    /// </summary>
    private static IEnumerable<Candidate> OneDimOneMeasureCandidates(
        string query, IReadOnlyList<Dictionary<string, object?>> data,
        string dim, string measure, bool dimIsTemporal, int rowCount)
    {
        // Duplicate-tolerant temporal line — NOT the same rule as the top-level Line candidate
        // (which requires ChooseTimeAxis to find a uniquely-ordering or composite axis). This is
        // only ever reached at all when that top-level candidate's ChooseTimeAxis already failed
        // (e.g. the single temporal column has duplicate values), so no axis check is repeated
        // here — it just plots the raw column directly.
        if (dimIsTemporal && rowCount <= MaxLinePoints)
        {
            var lineType = PickLineType(query, 1);
            yield return new Candidate(2, () => BuildCategory(lineType, query, data, dim, [measure]));
        }

        if (!dimIsTemporal && rowCount <= MaxPieSlices && CompositionRegex().IsMatch(query))
        {
            yield return new Candidate(3, () => BuildPie(query, data, dim, measure, PickPieType(query)));
        }

        var barType = PickBarType(data, dim);
        var barCap = barType == ChartType.HorizontalBar ? MaxHorizontalBarCategories : MaxBarCategories;
        if (rowCount <= barCap)
        {
            yield return new Candidate(1, () => BuildCategory(barType, query, data, dim, [measure]));
        }

        yield return new Candidate(0, () => BuildTable(data)); // too many categories for a readable bar
    }

    /// <summary>
    /// Candidates for a single dimension + several measures: multi-line (temporal) or grouped
    /// bar, plus a magnitude+percentage-pair special case (see inline comment).
    /// </summary>
    private static IEnumerable<Candidate> OneDimManyMeasuresCandidates(
        string query, IReadOnlyList<Dictionary<string, object?>> data,
        string dim, List<string> numeric, bool dimIsTemporal, int rowCount)
    {
        // Share/composition: a magnitude paired with a percentage column describes proportions of
        // a whole. If the query itself asks for a share/percentage/breakdown, honor that as a Pie
        // — the standard form for "what % of X is each Y" (see the documented example query list
        // in CLAUDE.md/README.md). Otherwise the percentage column is incidental, so just plot the
        // magnitude alone as a single-measure bar/line rather than grouping amount vs. percent
        // side by side.
        if (numeric.Count == 2 && numeric.Count(IsPercentageName) == 1)
        {
            var measure = numeric.First(n => !IsPercentageName(n));

            if (!dimIsTemporal && rowCount <= MaxPieSlices && CompositionRegex().IsMatch(query))
            {
                yield return new Candidate(3, () => BuildPie(query, data, dim, measure, PickPieType(query)));
            }
            if (dimIsTemporal && rowCount <= MaxLinePoints)
            {
                var lineType = PickLineType(query, 1);
                yield return new Candidate(2, () => BuildCategory(lineType, query, data, dim, [measure]));
            }

            var pairBarType = PickBarType(data, dim);
            var pairBarCap = pairBarType == ChartType.HorizontalBar ? MaxHorizontalBarCategories : MaxBarCategories;
            if (rowCount <= pairBarCap)
            {
                yield return new Candidate(1, () => BuildCategory(pairBarType, query, data, dim, [measure]));
            }

            yield return new Candidate(0, () => BuildTable(data));
            yield break;
        }

        var measures = numeric.Take(6).ToList();
        var type = dimIsTemporal ? PickLineType(query, measures.Count) : ChartType.GroupedBar;
        var cap = (type == ChartType.Line || type == ChartType.Area) ? MaxLinePoints : MaxBarCategories;
        if (rowCount <= cap)
        {
            yield return new Candidate(1, () => BuildCategory(type, query, data, dim, measures));
        }
        yield return new Candidate(0, () => BuildTable(data));
    }

    // ---- builders ----

    private static ChartRecommendation BuildStat(Dictionary<string, object?> row, List<string> numeric)
    {
        var stats = numeric.Select(c => new StatData
        {
            Label = Prettify(c),
            Value = ToDouble(row[c]) ?? 0,
            Unit = UnitFor(c),
        }).ToArray();
        return new ChartRecommendation { Type = ChartType.Stat, Title = string.Empty, Stats = stats };
    }

    /// <summary>
    /// Proportions of a whole for a single dimension — used only when the query itself asks for a
    /// share/percentage/breakdown (see <see cref="CompositionRegex"/>). The measure's raw values are
    /// charted (not a pre-computed percentage column, if one exists) so the slice proportions ECharts
    /// derives are always consistent with the underlying magnitudes, not a possibly-rounded duplicate.
    /// </summary>
    private static ChartRecommendation BuildPie(
        string query, IReadOnlyList<Dictionary<string, object?>> data, string dim, string measure,
        ChartType type = ChartType.Pie)
    {
        var rows = data.Take(MaxPieSlices).ToList();
        var labels = rows.Select(r => ToLabel(r[dim])).ToArray();
        var values = rows.Select(r => ToDouble(r[measure]) ?? 0).ToArray();
        return new ChartRecommendation
        {
            Type = type,
            Title = TitleFrom(query),
            Labels = labels,
            Series = [new ChartSeriesData { Name = Prettify(measure), Values = values }],
        };
    }

    private static ChartRecommendation BuildCategory(
        ChartType type, string query, IReadOnlyList<Dictionary<string, object?>> data, string dim, List<string> measures)
        => BuildCategory(type, query, data, r => ToLabel(r.GetValueOrDefault(dim)), Prettify(dim), measures);

    private static ChartRecommendation BuildCategory(
        ChartType type, string query, IReadOnlyList<Dictionary<string, object?>> data,
        Func<Dictionary<string, object?>, string> labelSelector, string xAxisLabel, List<string> measures)
    {
        var cap = type switch
        {
            ChartType.Line or ChartType.Area => MaxLinePoints,
            ChartType.HorizontalBar => MaxHorizontalBarCategories,
            _ => MaxBarCategories,
        };
        var rows = data.Take(cap).ToList();
        var labels = rows.Select(labelSelector).ToArray();
        var series = measures.Select(m => new ChartSeriesData
        {
            Name = Prettify(m),
            Values = rows.Select(r => ToDouble(r[m]) ?? 0).ToArray(),
        }).ToArray();
        return new ChartRecommendation
        {
            Type = type,
            Title = TitleFrom(query),
            Labels = labels,
            Series = series,
            XAxisLabel = xAxisLabel,
            YAxisLabel = measures.Count == 1 ? Prettify(measures[0]) : "Value",
        };
    }

    private static ChartRecommendation BuildPivot(
        string query, IReadOnlyList<Dictionary<string, object?>> data, List<string> dims, string measure)
    {
        // Choose the dimension with fewer distinct values as the series split.
        var d0Distinct = data.Select(r => ToLabel(r[dims[0]])).Distinct().Count();
        var d1Distinct = data.Select(r => ToLabel(r[dims[1]])).Distinct().Count();
        var (catDim, serDim) = d0Distinct >= d1Distinct ? (dims[0], dims[1]) : (dims[1], dims[0]);

        var categories = data.Select(r => ToLabel(r[catDim])).Distinct().Take(MaxBarCategories).ToArray();
        var seriesNames = data.Select(r => ToLabel(r[serDim])).Distinct().Take(6).ToArray();

        var series = seriesNames.Select(sn => new ChartSeriesData
        {
            Name = sn,
            Values = categories.Select(cat =>
            {
                var cell = data.FirstOrDefault(r => ToLabel(r[catDim]) == cat && ToLabel(r[serDim]) == sn);
                return cell is null ? 0 : ToDouble(cell[measure]) ?? 0;
            }).ToArray(),
        }).ToArray();

        var stacked = CompositionRegex().IsMatch(query);
        var rec = new ChartRecommendation
        {
            Type = stacked ? ChartType.StackedBar : ChartType.GroupedBar,
            Title = TitleFrom(query),
            Labels = categories,
            Series = series,
            XAxisLabel = Prettify(catDim),
            YAxisLabel = Prettify(measure),
        };
        return HorizontalIfLong(rec);
    }

    private static ChartRecommendation BuildScatter(
        string query, IReadOnlyList<Dictionary<string, object?>> data, string xCol, string yCol)
    {
        var points = data.Take(MaxScatterPoints).Select(r => new ChartPoint
        {
            X = ToDouble(r[xCol]) ?? 0,
            Y = ToDouble(r[yCol]) ?? 0,
        }).ToArray();
        return new ChartRecommendation
        {
            Type = ChartType.Scatter,
            Title = TitleFrom(query),
            XAxisLabel = Prettify(xCol),
            YAxisLabel = Prettify(yCol),
            Series = [new ChartSeriesData { Name = $"{Prettify(yCol)} vs {Prettify(xCol)}", Values = [], Points = points }],
        };
    }

    private static ChartRecommendation BuildTable(IReadOnlyList<Dictionary<string, object?>> data)
    {
        var columns = data[0].Keys.Take(MaxTableColumns).ToArray();
        var rows = data.Take(MaxTableRows)
            .Select(r => columns.Select(c => Cell(r.TryGetValue(c, out var v) ? v : null)).ToArray())
            .ToArray();
        return new ChartRecommendation
        {
            Type = ChartType.Table,
            Title = string.Empty,
            Table = new TableData { Columns = columns.Select(Prettify).ToArray(), Rows = rows },
        };
    }

    private static ChartRecommendation HorizontalIfLong(ChartRecommendation rec)
    {
        if (rec.Type != ChartType.Bar && rec.Type != ChartType.GroupedBar) return rec;
        if (rec.Labels.Length == 0) return rec;
        var avg = rec.Labels.Average(l => l.Length);
        // Only plain single-series bars flip to horizontal; grouped bars stay vertical.
        return avg > 12 && rec.Type == ChartType.Bar ? rec with { Type = ChartType.HorizontalBar } : rec;
    }

    /// <summary>Vertical bars get cramped with long labels; same threshold as <see cref="HorizontalIfLong"/>,
    /// but decided upfront so the row-count gate can use the right cap for whichever type wins.</summary>
    private static ChartType PickBarType(IReadOnlyList<Dictionary<string, object?>> data, string dim)
    {
        var avg = data.Select(r => ToLabel(r.GetValueOrDefault(dim)).Length).DefaultIfEmpty(0).Average();
        return avg > 12 ? ChartType.HorizontalBar : ChartType.Bar;
    }

    /// <summary>Donut is the same data binding as Pie, just with a center "total" label — offer
    /// it only when the query itself emphasizes a total, since that's what the center label
    /// actually surfaces. Does not add a new hard constraint: CompositionRegex must already have
    /// matched for either to be considered at all.</summary>
    private static ChartType PickPieType(string query)
        => TotalIntentRegex().IsMatch(query) ? ChartType.Donut : ChartType.Pie;

    /// <summary>Area only for a single plotted series — overlapping area fills read worse than
    /// overlapping lines for multi-series data, so Area is never offered there regardless of
    /// wording — and only when the query itself signals volume/cumulative magnitude matters,
    /// not just the trend shape.</summary>
    private static ChartType PickLineType(string query, int measureCount)
        => measureCount == 1 && VolumeIntentRegex().IsMatch(query) ? ChartType.Area : ChartType.Line;

    // ---- column typing ----

    private static bool IsTemporal(string col, IReadOnlyList<Dictionary<string, object?>> data)
    {
        if (TemporalNameRegex().IsMatch(col)) return true;
        return data.Take(10).Any(r => r.TryGetValue(col, out var v) && v is DateTime);
    }

    private static bool IsNumeric(string col, IReadOnlyList<Dictionary<string, object?>> data)
    {
        var seen = 0;
        foreach (var r in data)
        {
            if (!r.TryGetValue(col, out var v) || v is null) continue;
            if (ToDouble(v) is null) return false;
            if (++seen >= 20) break;
        }
        return seen > 0;
    }

    /// <summary>How to derive a chart's x-axis label per row, plus the axis title to display.</summary>
    private readonly record struct TimeAxis(Func<Dictionary<string, object?>, string> LabelSelector, string XAxisLabel);

    /// <summary>
    /// A temporal column that uniquely orders the rows is the real x-axis; redundant temporal
    /// columns (e.g. Year alongside Month) collapse into it. Priority, most to least readable:
    ///
    /// 1. A single column that's both unique per row AND text-labelled (e.g. an already-
    ///    formatted "Jan 2007" style column) — the simplest, most readable case.
    /// 2. A composite label built from denormalized calendar columns (e.g. Year + MonthLabel ->
    ///    "Jan 2007") when no single column is unique alone but the FULL COMBINATION is —
    ///    preferred over #3 even when a technically-unique column exists, because some calendar
    ///    dimensions encode a "month" column as a YYYYMM code (e.g. 200701): that's unique and
    ///    orders correctly, but "Jan 2007" is far more readable than "200701".
    /// 3. Any single unique column, even an unformatted numeric code — still correctly
    ///    identifies/orders every row, just less pretty. Only reached when no text column
    ///    exists to build a readable composite from.
    /// 4. If nothing is unique alone but the columns are unique together and none is
    ///    text-labelled (all-numeric grains), join them coarse-to-fine with "-".
    ///
    /// This deliberately never re-sorts rows: it trusts the SQL result's own row order (the
    /// SQL-generation prompt already asks for ASC-chronological ordering — see
    /// CompanyDataService.cs) rather than re-deriving a sort key from a text label, which would
    /// sort "April" before "January".
    ///
    /// Null if neither a single column nor the full combination uniquely identifies every row.
    /// </summary>
    private static TimeAxis? ChooseTimeAxis(List<string> temporal, IReadOnlyList<Dictionary<string, object?>> data, int rowCount)
    {
        int DistinctCount(string col) => data.Select(r => ToLabel(r.GetValueOrDefault(col))).Distinct().Count();
        bool HasStringValues(string col) => data.Any(r => r.GetValueOrDefault(col) is string);

        var unique = temporal.Where(c => DistinctCount(c) == rowCount).ToList();

        var uniqueText = unique.FirstOrDefault(HasStringValues);
        if (uniqueText is not null)
        {
            return new TimeAxis(r => ToLabel(r.GetValueOrDefault(uniqueText)), Prettify(uniqueText));
        }

        var combinedUnique = temporal.Count >= 2 && data
            .Select(r => string.Join("|", temporal.Select(c => ToLabel(r.GetValueOrDefault(c)))))
            .Distinct().Count() == rowCount;

        if (combinedUnique)
        {
            var byGrain = temporal.OrderBy(DistinctCount).ToList();
            var fine = byGrain.FirstOrDefault(HasStringValues);
            if (fine is not null)
            {
                var coarse = byGrain.First(c => c != fine);
                return new TimeAxis(r => $"{ToLabel(r.GetValueOrDefault(fine))} {ToLabel(r.GetValueOrDefault(coarse))}", "Period");
            }
        }

        if (unique.Count > 0)
        {
            return new TimeAxis(r => ToLabel(r.GetValueOrDefault(unique[0])), Prettify(unique[0]));
        }

        if (!combinedUnique) return null;
        var ordered = temporal.OrderBy(DistinctCount).ToList();
        return new TimeAxis(r => string.Join("-", ordered.Select(c => ToLabel(r.GetValueOrDefault(c)))), "Period");
    }

    /// <summary>Identifier/key columns (…Id, …Key, …Code, …Guid/Uuid) are numeric in type but not measures.</summary>
    private static bool IsIdentifier(string col)
    {
        var spaced = Regex.Replace(col.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ");
        var tokens = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && tokens[^1].ToLowerInvariant() is "id" or "key" or "code" or "guid" or "uuid";
    }

    /// <summary>A percentage/share measure column (detected by name).</summary>
    private static bool IsPercentageName(string col)
        => Regex.IsMatch(col, "percent|pct|share|proportion", RegexOptions.IgnoreCase);

    // ---- value helpers ----

    private static double? ToDouble(object? value)
    {
        switch (value)
        {
            case null: return null;
            case double d: return d;
            case float f: return f;
            case int i: return i;
            case long l: return l;
            case short s: return s;
            case decimal m: return (double)m;
            case bool: return null;
            case DateTime: return null;
            case JsonElement je when je.ValueKind == JsonValueKind.Number: return je.GetDouble();
            case JsonElement: return null;
            default:
                return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : null;
        }
    }

    private static string ToLabel(object? value) => Cell(value);

    private static string Cell(object? value)
    {
        if (value is null) return string.Empty;
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Number => je.GetRawText(),
                JsonValueKind.String => je.GetString() ?? string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => je.GetRawText(),
            };
        }
        if (value is DateTime dt) return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (value is double or float or decimal)
            return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture);
        return value.ToString() ?? string.Empty;
    }

    /// <summary>"TotalRevenue" / "total_revenue" → "Total Revenue".</summary>
    private static string Prettify(string col)
    {
        if (string.IsNullOrWhiteSpace(col)) return col;
        var spaced = Regex.Replace(col.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ");
        return spaced.Trim();
    }

    private static string? UnitFor(string col)
    {
        var c = col.ToLowerInvariant();
        if (Regex.IsMatch(c, "revenue|sales|amount|price|cost|profit|margin|value")) return "$";
        if (Regex.IsMatch(c, "percent|pct|rate|ratio")) return "%";
        return null;
    }

    private static string TitleFrom(string query)
    {
        var q = query.Trim();
        if (q.Length == 0) return string.Empty;
        var t = char.ToUpperInvariant(q[0]) + q[1..];
        return t.Length > 60 ? t[..57] + "…" : t.TrimEnd('?', '.');
    }
}
