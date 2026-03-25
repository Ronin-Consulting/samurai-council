using MudBlazor.Services;
using QuestPDF.Infrastructure;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Services;
using SamurAICouncil.Data;
using SamurAICouncil.Data.Migrations;
using SamurAICouncil.Web.Components;
using SamurAICouncil.Web.Services;

// Configure QuestPDF license (Community license for companies with <$1M annual revenue)
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

// Configure LLM API keys from appsettings.json or environment variables
builder.Services.Configure<LlmApiKeysConfiguration>(
    builder.Configuration.GetSection(LlmApiKeysConfiguration.SectionName));

// Configure Council settings from appsettings.json
builder.Services.Configure<CouncilConfiguration>(
    builder.Configuration.GetSection(CouncilConfiguration.SectionName));

// Configure Company Data settings (Text-to-SQL)
builder.Services.Configure<CompanyDataConfiguration>(
    builder.Configuration.GetSection(CompanyDataConfiguration.SectionName));

// Register Core services
builder.Services.AddSingleton<ILlmService, SemanticKernelLlmService>();
builder.Services.AddSingleton<ICouncilService, CouncilService>();
builder.Services.AddSingleton<ICompanyDataService, CompanyDataService>();
builder.Services.AddSingleton<IChartDataTransformer, ChartDataTransformer>();
builder.Services.AddSingleton<CompanyDataTool>();
builder.Services.AddSingleton<CompanyDataPlugin>();

// Register Data services (repositories with Polly resilience)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDataServices(connectionString);
    builder.Services.AddMigrations(connectionString);
}

// Add UI state services (scoped per circuit in Blazor Server)
builder.Services.AddScoped<ConversationState>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<ThemeService>();

// Add export services
builder.Services.AddScoped<IExportService, PdfExportService>();

var app = builder.Build();

// Run database migrations on startup (if connection string configured)
if (!string.IsNullOrEmpty(connectionString))
{
    using var scope = app.Services.CreateScope();
    try
    {
        MigrationRunner.RunMigrations(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database migration failed. Database may not be available.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();