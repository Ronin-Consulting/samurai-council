using Microsoft.Extensions.Logging;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services.Export.Rendering;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Orchestrates export of a council response to PDF, Excel, or Word. Builds one shared
/// <see cref="Export.Model.ReportDocument"/> from the message/query/timestamp and hands it to the
/// format-specific renderer, so all three formats render identical content structure.
/// </summary>
public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;
    private readonly Export.ReportDocumentBuilder _builder;

    public ExportService(ILogger<ExportService> logger, Export.ReportDocumentBuilder builder)
    {
        _logger = logger;
        _builder = builder;
    }

    public Task<byte[]> ExportToPdfAsync(AssistantMessage message, string query, DateTime timestamp)
    {
        _logger.LogInformation("Generating PDF export for query: {Query}", Truncate(query));
        var document = _builder.Build(message, query, timestamp);
        var bytes = PdfReportRenderer.Render(document);
        _logger.LogInformation("PDF generated successfully, size: {Size} bytes", bytes.Length);
        return Task.FromResult(bytes);
    }

    public Task<byte[]> ExportToExcelAsync(AssistantMessage message, string query, DateTime timestamp)
    {
        _logger.LogInformation("Generating Excel export for query: {Query}", Truncate(query));
        var document = _builder.Build(message, query, timestamp);
        var bytes = ExcelReportRenderer.Render(document);
        _logger.LogInformation("Excel generated successfully, size: {Size} bytes", bytes.Length);
        return Task.FromResult(bytes);
    }

    public Task<byte[]> ExportToWordAsync(AssistantMessage message, string query, DateTime timestamp)
    {
        _logger.LogInformation("Generating Word export for query: {Query}", Truncate(query));
        var document = _builder.Build(message, query, timestamp);
        var bytes = WordReportRenderer.Render(document);
        _logger.LogInformation("Word document generated successfully, size: {Size} bytes", bytes.Length);
        return Task.FromResult(bytes);
    }

    public bool HasTabularData(AssistantMessage message)
    {
        if (message.Stage3?.UsedTools == true)
            return true;

        if (message.Stage1?.Any(s => s.UsedTools) == true)
            return true;

        if (message.Stage3?.Response != null && ContainsMarkdownTable(message.Stage3.Response))
            return true;

        return false;
    }

    public string GenerateFilename(string extension, DateTime timestamp)
    {
        var formattedTime = timestamp.ToString("yyyy-MM-dd_HHmmss");
        return $"SamurAI_Council_{formattedTime}.{extension}";
    }

    private static bool ContainsMarkdownTable(string text) => text.Contains("|") && text.Contains("---");

    private static string Truncate(string text) => text.Length > 50 ? text[..50] + "..." : text;
}
