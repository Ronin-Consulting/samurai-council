using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Service for exporting council responses to various formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports an assistant message to PDF format.
    /// </summary>
    /// <param name="message">The assistant message containing council response data.</param>
    /// <param name="query">The original user query.</param>
    /// <param name="timestamp">When the query was submitted.</param>
    /// <returns>PDF document as a byte array.</returns>
    Task<byte[]> ExportToPdfAsync(AssistantMessage message, string query, DateTime timestamp);

    /// <summary>
    /// Exports an assistant message to Excel format.
    /// Only available when tabular data exists (tool results or markdown tables).
    /// </summary>
    /// <param name="message">The assistant message containing council response data.</param>
    /// <param name="query">The original user query.</param>
    /// <param name="timestamp">When the query was submitted.</param>
    /// <returns>Excel document as a byte array.</returns>
    Task<byte[]> ExportToExcelAsync(AssistantMessage message, string query, DateTime timestamp);

    /// <summary>
    /// Exports an assistant message to Word (.docx) format.
    /// </summary>
    /// <param name="message">The assistant message containing council response data.</param>
    /// <param name="query">The original user query.</param>
    /// <param name="timestamp">When the query was submitted.</param>
    /// <returns>Word document as a byte array.</returns>
    Task<byte[]> ExportToWordAsync(AssistantMessage message, string query, DateTime timestamp);

    /// <summary>
    /// Determines if the message contains tabular data suitable for Excel export.
    /// </summary>
    /// <param name="message">The assistant message to check.</param>
    /// <returns>True if tabular data exists (tool results or markdown tables).</returns>
    bool HasTabularData(AssistantMessage message);

    /// <summary>
    /// Generates a filename for the export with timestamp.
    /// </summary>
    /// <param name="extension">File extension (e.g., "pdf", "xlsx").</param>
    /// <param name="timestamp">Timestamp to include in filename.</param>
    /// <returns>Formatted filename.</returns>
    string GenerateFilename(string extension, DateTime timestamp);
}
