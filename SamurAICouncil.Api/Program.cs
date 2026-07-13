using QuestPDF.Infrastructure;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Services;
using SamurAICouncil.Data;
using SamurAICouncil.Data.Migrations;

// QuestPDF Community license (companies with < $1M annual revenue)
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Controllers — honor [JsonPropertyName]/polymorphic attributes on the Core models
// (do NOT force camelCase; the models are snake_case by design).
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddOpenApi();

const string SpaCors = "spa";
builder.Services.AddCors(o => o.AddPolicy(SpaCors, p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Configuration (same sections as the Blazor host)
builder.Services.Configure<LlmApiKeysConfiguration>(
    builder.Configuration.GetSection(LlmApiKeysConfiguration.SectionName));
builder.Services.Configure<CouncilConfiguration>(
    builder.Configuration.GetSection(CouncilConfiguration.SectionName));
builder.Services.Configure<CompanyDataConfiguration>(
    builder.Configuration.GetSection(CompanyDataConfiguration.SectionName));

// Core services (mirror SamurAICouncil.Web/Program.cs)
builder.Services.AddSingleton<ILlmService, SemanticKernelLlmService>();
builder.Services.AddSingleton<ICouncilService, CouncilService>();
builder.Services.AddSingleton<ICompanyDataService, CompanyDataService>();
builder.Services.AddSingleton<IChartDataTransformer, ChartDataTransformer>();
builder.Services.AddSingleton<CompanyDataTool>();
builder.Services.AddSingleton<CompanyDataPlugin>();

// Application orchestrator (scoped — depends on the scoped repositories)
builder.Services.AddScoped<CouncilConversationService>();

// Export (PDF/Excel) — moved from the Blazor host into Core
builder.Services.AddScoped<IExportService, PdfExportService>();

// Data layer (only when a connection string is configured)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDataServices(connectionString);
    builder.Services.AddMigrations(connectionString);
}

var app = builder.Build();

// Run migrations on startup (warn-only if the DB is unavailable)
if (!string.IsNullOrEmpty(connectionString))
{
    using var scope = app.Services.CreateScope();
    try
    {
        MigrationRunner.RunMigrations(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database migration failed. Database may not be available.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(SpaCors);

// Serve the Angular SPA (copied into wwwroot at container build time).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// SPA fallback: any non-API, non-file route returns index.html for client-side routing.
app.MapFallbackToFile("index.html");

app.Run();
