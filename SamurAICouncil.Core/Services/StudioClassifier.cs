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
    private const int MaxPieSlices = 6;
    private const int MaxLinePoints = 24;
    private const int MaxScatterPoints = 200;
    private const int MaxTableRows = 100;
    private const int MaxTableColumns = 12;

    [GeneratedRegex(@"year|quarter|month|week|day|date|yr|mon|period|fy", RegexOptions.IgnoreCase)]
    private static partial Regex TemporalNameRegex();

    [GeneratedRegex(@"share|composition|of total|percentage|proportion|breakdown|split|mix", RegexOptions.IgnoreCase)]
    private static partial Regex CompositionRegex();

    public ChartRecommendation Classify(string query, IReadOnlyList<Dictionary<string, object?>> data)
    {
        if (data.Count == 0)
        {
            return new ChartRecommendation { Type = ChartType.None };
        }

        var columns = data[0].Keys.ToArray();
        var temporal = columns.Where(c => IsTemporal(c, data)).ToList();
        var numeric = columns.Where(c => !IsTemporal(c, data) && IsNumeric(c, data)).ToList();
        var dims = columns.Where(c => !numeric.Contains(c)).ToList(); // temporal + text
        var rowCount = data.Count;

        // 1) single row, only numbers → KPI tiles
        if (rowCount == 1 && numeric.Count is >= 1 and <= 4 && dims.Count == 0)
        {
            return BuildStat(data[0], numeric);
        }

        // 5) two measures, no dimension → scatter
        if (numeric.Count == 2 && dims.Count == 0)
        {
            return BuildScatter(query, data, numeric[0], numeric[1]);
        }

        // 6) no measure, or too many dimensions → table
        if (numeric.Count == 0 || dims.Count >= 3)
        {
            return BuildTable(data);
        }

        // 2) one dimension + one measure → line (temporal) or bar
        if (dims.Count == 1 && numeric.Count == 1)
        {
            var dim = dims[0];
            if (temporal.Contains(dim) && rowCount <= MaxLinePoints)
            {
                return BuildCategory(ChartType.Line, query, data, dim, [numeric[0]]);
            }
            if (rowCount <= MaxBarCategories)
            {
                var rec = BuildCategory(ChartType.Bar, query, data, dim, [numeric[0]]);
                return HorizontalIfLong(rec);
            }
            return BuildTable(data); // too many categories for a readable bar
        }

        // 3) one dimension + several measures → multi-line (temporal) or grouped bar
        if (dims.Count == 1 && numeric.Count >= 2)
        {
            var dim = dims[0];
            var type = temporal.Contains(dim) ? ChartType.Line : ChartType.GroupedBar;
            if (rowCount <= (type == ChartType.Line ? MaxLinePoints : MaxBarCategories))
            {
                return BuildCategory(type, query, data, dim, numeric.Take(6).ToList());
            }
            return BuildTable(data);
        }

        // 4) two dimensions + one measure → pivot the second dim into series
        if (dims.Count == 2 && numeric.Count == 1)
        {
            return BuildPivot(query, data, dims, numeric[0]);
        }

        // default
        return BuildTable(data);
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

    private static ChartRecommendation BuildCategory(
        ChartType type, string query, IReadOnlyList<Dictionary<string, object?>> data, string dim, List<string> measures)
    {
        var cap = type == ChartType.Line ? MaxLinePoints : MaxBarCategories;
        var rows = data.Take(cap).ToList();
        var labels = rows.Select(r => ToLabel(r[dim])).ToArray();
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
            XAxisLabel = Prettify(dim),
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
