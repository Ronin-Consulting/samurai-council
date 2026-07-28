namespace SamurAICouncil.Core.Services.Export;

/// <summary>
/// Shared hex color constants consumed by all three renderers (PDF/Excel/Word), so brand colors
/// (red accent, blue tool boxes, gold rank-1 highlight) are defined once instead of drifting
/// between QuestPDF's <c>Colors.*</c>, ClosedXML's <c>XLColor.*</c>, and OpenXml shading hex values.
/// </summary>
public static class ReportPalette
{
    public const string RedAccentHex = "C00000";
    public const string BlueAccentHex = "1565C0";
    public const string BlueBoxBackgroundHex = "E3F2FD";
    public const string BlueBoxBorderHex = "90CAF9";
    public const string GoldHighlightHex = "FFF9C4";
    public const string GreyBoxBackgroundHex = "F5F5F5";
    public const string GreyBoxBorderHex = "E0E0E0";
    public const string AltRowBackgroundHex = "F5F5F5";

    /// <summary>The app's own dataviz categorical palette (clients/angular/src/app/util.ts PALETTE_LIGHT),
    /// reused here so exported chart images visually match what's shown in the live app.</summary>
    public static readonly string[] ChartSeriesHex =
    [
        "2a78d6", "1baf7a", "eda100", "008300", "4a3aa7", "e34948", "e87ba4", "eb6834",
    ];

    public const string ChartSurfaceHex = "fcfcfb";
    public const string ChartGridHex = "e1e0d9";
    public const string ChartPrimaryTextHex = "0b0b0b";
    public const string ChartMutedTextHex = "898781";
}
