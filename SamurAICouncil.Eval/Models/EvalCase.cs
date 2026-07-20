using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SamurAICouncil.Eval.Models;

/// <summary>One column of a fixture result set, with an explicit CLR type hint.</summary>
public sealed class ColumnSpec
{
    public string Name { get; set; } = "";

    /// <summary>"number" | "date" | "text". Omitted → inferred from the JSON value.</summary>
    public string? Type { get; set; }
}

/// <summary>
/// A single evaluation case. Fixtures carry an inline result set (columns + rows);
/// live cases carry only the query and are resolved against the real database at run time.
/// </summary>
public sealed class EvalCase
{
    public string Id { get; set; } = "";
    public string Query { get; set; } = "";

    /// <summary>"fixture" | "live" — set by the loader.</summary>
    public string Suite { get; set; } = "fixture";

    public string EdgeCategory { get; set; } = "";

    /// <summary>The single best-fit form (a <see cref="Core.Models.ChartType"/> name).</summary>
    public string ExpectedForm { get; set; } = "";

    /// <summary>Forms that are also acceptable (defensible) for this case.</summary>
    public List<string> AcceptableForms { get; set; } = [];

    public string? Notes { get; set; }

    // ---- fixture-only: inline result set ----
    public List<ColumnSpec>? Columns { get; set; }
    public List<List<JsonElement>>? Rows { get; set; }

    /// <summary>
    /// Materializes the fixture <see cref="Rows"/> into the row shape the production
    /// pipeline uses (<c>IReadOnlyList&lt;Dictionary&lt;string, object?&gt;&gt;</c>), applying
    /// the per-column type hint so StudioClassifier's numeric/temporal detection matches prod.
    /// </summary>
    public IReadOnlyList<Dictionary<string, object?>> Materialize()
    {
        if (Columns is null || Rows is null)
        {
            return [];
        }

        var result = new List<Dictionary<string, object?>>(Rows.Count);
        foreach (var row in Rows)
        {
            var dict = new Dictionary<string, object?>(Columns.Count);
            for (var c = 0; c < Columns.Count; c++)
            {
                var col = Columns[c];
                var cell = c < row.Count ? row[c] : default;
                dict[col.Name] = Convert(cell, col.Type);
            }
            result.Add(dict);
        }
        return result;
    }

    private static object? Convert(JsonElement el, string? type)
    {
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        switch (type?.ToLowerInvariant())
        {
            case "number":
                return el.ValueKind == JsonValueKind.Number
                    ? el.GetDouble()
                    : double.TryParse(el.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
            case "date":
                return DateTime.TryParse(el.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                    ? dt
                    : el.GetString();
            case "text":
                return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            default:
                // Infer: number → double, string → string, bool → bool.
                return el.ValueKind switch
                {
                    JsonValueKind.Number => el.GetDouble(),
                    JsonValueKind.String => el.GetString(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => el.ToString(),
                };
        }
    }
}

/// <summary>Root of a fixture / live-query JSON file.</summary>
public sealed class CaseFile
{
    [JsonPropertyName("cases")]
    public List<EvalCase> Cases { get; set; } = [];
}
