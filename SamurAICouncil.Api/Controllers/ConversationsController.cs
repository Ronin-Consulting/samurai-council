using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly CouncilConversationService _service;
    private readonly IExportService _export;

    // SSE payload serialization: honor model attributes, keep enums as strings (attribute-driven).
    private static readonly JsonSerializerOptions SseJson = new() { PropertyNamingPolicy = null };

    public ConversationsController(CouncilConversationService service, IExportService export)
    {
        _service = service;
        _export = export;
    }

    // ---- CRUD ----

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListConversationsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var conversation = await _service.LoadConversationAsync(id, ct);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest? body, CancellationToken ct)
    {
        var conversation = await _service.CreateConversationAsync(body?.Title ?? "New Conversation", ct);
        return CreatedAtAction(nameof(Get), new { id = conversation.Id }, conversation);
    }

    [HttpPut("{id:guid}/title")]
    public async Task<IActionResult> UpdateTitle(Guid id, [FromBody] UpdateTitleRequest body, CancellationToken ct)
    {
        await _service.UpdateTitleAsync(id, body.Title, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteConversationAsync(id, ct);
        return NoContent();
    }

    [HttpPost("delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest body, CancellationToken ct)
    {
        var count = await _service.DeleteConversationsAsync(body.Ids ?? [], ct);
        return Ok(new { deleted = count });
    }

    // ---- Council run (SSE) ----

    [HttpPost("{id:guid}/messages")]
    public async Task SendMessage(Guid id, [FromBody] SendMessageRequest body, CancellationToken ct)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // disable proxy buffering

        async Task Emit(CouncilStreamEvent evt)
        {
            var json = JsonSerializer.Serialize(evt.Data, SseJson);
            var frame = $"event: {evt.Event}\ndata: {json}\n\n";
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(frame), ct);
            await Response.Body.FlushAsync(ct);
        }

        await _service.RunCouncilStreamAsync(id, body.Content ?? string.Empty, Emit, ct);
    }

    // ---- Export (PDF / Excel) ----

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format = "pdf", CancellationToken ct = default)
    {
        var found = await _service.GetAssistantMessageAsync(id, ct);
        if (found is null) return NotFound();
        var (message, query, timestamp) = found.Value;

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var xlsx = await _export.ExportToExcelAsync(message, query, timestamp);
            return File(xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _export.GenerateFilename("xlsx", timestamp));
        }

        var pdf = await _export.ExportToPdfAsync(message, query, timestamp);
        return File(pdf, "application/pdf", _export.GenerateFilename("pdf", timestamp));
    }
}

public record CreateConversationRequest(string? Title);
public record UpdateTitleRequest(string Title);
public record BulkDeleteRequest(List<Guid>? Ids);
public record SendMessageRequest(string Content);
