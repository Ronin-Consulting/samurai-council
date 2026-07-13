# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SamurAI Council implements a 3-stage LLM deliberation pattern where multiple AI models collaborate to provide higher-quality responses. It is an **Angular SPA** frontend (`clients/angular`) talking over HTTP + Server-Sent Events to a **.NET Web API** (`SamurAICouncil.Api`) that hosts the domain logic in `SamurAICouncil.Core` / `SamurAICouncil.Data`. In production the API serves the built Angular app as static files (single origin). Originally a Blazor Server app (since migrated) and before that the LlmCouncil Python/React reference implementation.

## Project Structure

```
SamurAICouncil/
├── SamurAICouncil.sln
├── compose.yaml                  # Docker Compose for PostgreSQL + SQL Server + Web
├── WORK_PLAN_RELEASE_X.md        # Release work plans (0, 1, 2, etc.)
│
├── scripts/                      # Utility scripts
│   ├── init-contoso.sh           # Initialize ContosoRetailDW database
│   └── sql/                      # SQL scripts
│       └── init-contoso-db.sql   # Database restore and user setup
│
├── data/                         # Data files (gitignored)
│   └── contoso/                  # ContosoRetailDW.bak goes here
│
├── SamurAICouncil.Web/           # Blazor Server app (UI layer)
│   ├── Components/
│   │   ├── Layout/               # MainLayout, Sidebar, ReconnectModal
│   │   ├── Pages/                # Chat, Error, NotFound
│   │   └── Shared/               # Stage panels, MarkdownRenderer, etc.
│   ├── Services/                 # ConversationService, ConversationState, PdfExportService
│   ├── wwwroot/js/               # fileDownload.js, markdown-enhance.js
│   └── Dockerfile
│
├── SamurAICouncil.Core/          # Domain models, interfaces, business logic
│   ├── Configuration/            # CouncilConfiguration, LlmApiKeys, CompanyData
│   ├── Interfaces/               # ILlmService, ICouncilService, repositories
│   ├── Models/                   # Domain entities
│   ├── Resources/                # Embedded ContosoRetailDW schema
│   └── Services/                 # CouncilService, SemanticKernelLlmService
│
├── SamurAICouncil.Data/          # Data access layer
│   ├── Repositories/             # Dapper-based repositories
│   ├── Resilience/               # Polly retry + circuit breaker policies
│   └── Migrations/               # FluentMigrator migrations
│
├── SamurAICouncil.Core.Tests/    # Unit tests for Core (232 tests)
├── SamurAICouncil.Data.Tests/    # Integration tests with TestContainers (18 tests)
├── SamurAICouncil.Web.Tests/     # bUnit component + integration tests (110 tests)
│
└── LlmCouncil/                   # Python/React reference implementation
```

## Technology Stack

| Layer | Technology |
|-------|------------|
| UI | Blazor Server (.NET 10, Interactive Server mode) |
| LLM Integration | Microsoft Semantic Kernel (OpenAI, Anthropic connectors) |
| Database | PostgreSQL 16 (app data), SQL Server 2022 (ContosoRetailDW) |
| ORM | Dapper with JSON column support |
| Migrations | FluentMigrator |
| Resilience | Polly (retry + circuit breaker for LLM and DB) |
| Testing | MSTest, bUnit, Moq, TestContainers |
| Markdown | Markdig |
| PDF Export | QuestPDF |
| Excel Export | ClosedXML |

## 3-Stage Deliberation Architecture

```
User Query
    │
    ▼
┌─────────────────────────────────────┐
│  Stage 1: Collect Responses         │
│  - Query all council models in      │
│    parallel                         │
│  - Each model provides independent  │
│    response                         │
└───────────────┬─────────────────────┘
                │
                ▼
┌─────────────────────────────────────┐
│  Stage 2: Peer Review               │
│  - Anonymize responses (A, B, C)    │
│  - Each model ranks all responses   │
│  - Calculate aggregate rankings     │
└───────────────┬─────────────────────┘
                │
                ▼
┌─────────────────────────────────────┐
│  Stage 3: Synthesis                 │
│  - Chairman model reviews all       │
│  - Synthesizes final answer         │
│  - Considers rankings in synthesis  │
└───────────────┬─────────────────────┘
                │
                ▼
          Final Response
```

## Key Components

### Core Services

| Service | Purpose |
|---------|---------|
| `CouncilService` | Orchestrates 3-stage deliberation |
| `SemanticKernelLlmService` | Multi-provider LLM integration (OpenAI, Anthropic, Google) |
| `ResilientLlmService` | Polly-wrapped decorator for LLM calls |
| `CompanyDataService` | Text-to-SQL queries against ContosoRetailDW |
| `CompanyDataTool` | ILlmTool implementation for SQL tool calling |
| `CompanyDataPlugin` | Semantic Kernel plugin wrapper for CompanyDataTool |
| `ChartDataTransformer` | Parses and validates LLM chart recommendations |
| `RankingParser` | Extracts rankings from model responses |
| `AggregateRankingCalculator` | Computes consensus rankings |
| `AnonymizationHelper` | Manages response anonymization |

### Blazor Components

| Component | Purpose |
|-----------|---------|
| `Chat.razor` | Main chat interface with message history |
| `Sidebar.razor` | Conversation list and navigation |
| `AssistantMessagePanel.razor` | Orchestrates stage display |
| `Stage1Panel.razor` | Tab view of model responses |
| `Stage2Panel.razor` | Rankings with de-anonymization |
| `Stage3Panel.razor` | Chairman's synthesized answer with chart visualization |
| `ToolUsagePanel.razor` | Displays tool usage (SQL queries, results) |
| `ChartDisplay.razor` | Renders MudBlazor charts (Bar, Line, Pie, Donut) |
| `LoadingSpinner.razor` | Animated loading indicator |
| `Logo.razor` | Application logo component |
| `TabNavigation.razor` | Reusable tab navigation control |

### Chart Visualization

The application includes LLM-driven chart generation for data query results. When the LLM determines that query results would benefit from visual representation, it recommends an appropriate chart type.

**Chart Types:**
- **Bar**: Category comparisons (sales by region, products by count)
- **Line**: Trends over time (monthly sales, daily visitors)
- **Pie/Donut**: Proportions of a whole (market share, budget allocation)
- **None**: When data doesn't suit visualization (single values, text data)

**Chart Flow:**
1. `CompanyDataTool` executes SQL query against ContosoRetailDW
2. LLM analyzes results and recommends chart type via `ChartRecommendation`
3. Chart propagates from best-ranked Stage 1 response to Stage 3 (Final Answer)
4. `ChartDisplay` component renders using MudBlazor's `MudChart`

**Key Models:**
- `ChartRecommendation`: Contains type, title, labels, series, and axis labels
- `ChartSeriesData`: Individual data series with name and values
- `ChartType`: Enum (None, Bar, Line, Pie, Donut)

**Example Queries that Produce Charts:**
- "Show top 5 products by revenue in 2008" → Bar chart
- "Show sales by country" → Bar chart
- "Show monthly sales trends" → Line chart
- "What percentage of sales come from each channel?" → Pie chart

### Data Layer

| Entity | Storage |
|--------|---------|
| `Conversation` | PostgreSQL (id, title, created_at) |
| `Message` | PostgreSQL with JSON columns for stage data |
| Stage data | JSON columns (stage1, stage2, stage3, metadata) |

## Build and Run Commands

### Development

```bash
# Build entire solution
dotnet build

# Run all tests (360 total)
dotnet test

# Run the API (backend)
dotnet run --project SamurAICouncil.Api

# Run the Angular client (frontend) — proxies /api to the API on :5131
cd clients/angular && ng serve   # http://localhost:4200

# Note: run the Node/Angular and .NET tooling inside WSL2 on this host
# (native Windows binaries crash with 0xC000001D — see the run-setup memory).
```

### Docker Deployment

```bash
# Set API keys in environment
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."

# Build and start
docker compose up --build

# View logs
docker compose logs -f web

# Stop
docker compose down
```

**The application runs on http://localhost:5080**

### Visual Verification with Playwright

When using Playwright MCP to verify UI changes:
- The container must be redeployed for changes to be visible
- Always use port **5080** (not 5000)
- Ask the user to redeploy before attempting visual verification

### ContosoRetailDW Setup (Text-to-SQL Feature)

The application includes a SQL Server database with Microsoft's ContosoRetailDW demo data for Text-to-SQL queries:

```bash
# 1. Download ContosoRetailDW.bak from Microsoft
# https://www.microsoft.com/en-us/download/details.aspx?id=18279

# 2. Place the .bak file in the data directory
cp ContosoRetailDW.bak ./data/contoso/

# 3. Start the services
docker compose up -d

# 4. Initialize the database (first time only)
./scripts/init-contoso.sh
```

**Connection Details:**
- Server: `localhost,1433` (or `sqlserver` from within containers)
- Database: `ContosoRetailDW`
- Read-only User: `samurai_reader` / `Reader!Pass123`

### Database Migrations

Migrations run automatically on startup. To run manually:

```bash
# Start PostgreSQL
docker compose up -d db

# Run app (migrations execute on startup)
dotnet run --project SamurAICouncil.Web
```

## Configuration

### appsettings.json

See `SamurAICouncil.Web/appsettings.json` for the full configuration. Key sections:

- **`LlmApiKeys`**: OpenAI, Anthropic, Google API keys
- **`Council.CouncilModels`**: Array of `{Provider, ModelId, DisplayName}` for council members (default: GPT-4, Claude 3 Sonnet, Gemini 1.5 Pro)
- **`Council.ChairmanModel`**: Model that synthesizes the final answer
- **`Council.TitleGenerationModel`**: Lightweight model for conversation title generation
- **`CompanyData`**: Text-to-SQL config (ConnectionString, MaxRows, QueryTimeoutSeconds, SqlGenerationModel)

### Environment Variables (Docker)

See `compose.yaml` for the full Docker configuration. Required host environment variables:

```bash
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."
export GOOGLE_API_KEY="..."          # Optional
export MSSQL_SA_PASSWORD="..."       # Optional, has default
```

**Note:** API keys are validated at startup. If a configured provider is missing its API key, the application will fail to start with a clear error message. The compose.yaml maps these to the app's `LlmApiKeys__*` settings and configures council models (currently gpt-5-mini, claude-haiku-4-5, gemini-2.5-flash with gemini-2.5-pro as chairman).

## Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test SamurAICouncil.Core.Tests
dotnet test SamurAICouncil.Data.Tests      # Requires Docker
dotnet test SamurAICouncil.Web.Tests       # Requires Docker for integration tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~CouncilServiceTests"
```

### Test Coverage

| Project | Tests | Coverage Target |
|---------|-------|-----------------|
| Core.Tests | 232 | 90% (business logic) |
| Data.Tests | 18 | Integration only |
| Web.Tests | 110 | 50% (critical components) |

## Architecture Decisions

### Why Semantic Kernel?
- Native .NET integration with multiple LLM providers
- Consistent API across OpenAI, Anthropic, Google
- Built-in retry and streaming support

### Why PostgreSQL + Dapper?
- JSON column support for flexible stage data storage
- Lightweight ORM with full control over queries
- Easy FluentMigrator integration

### Why Polly?
- Resilience for unreliable LLM APIs
- Circuit breaker prevents cascade failures
- Configurable retry with exponential backoff

### Error Handling
- Global `AppErrorBoundary` catches component errors
- User-friendly error messages
- Automatic recovery on navigation
- Structured logging for debugging

### LLM Tool Invocation
The `SemanticKernelLlmService` supports tool/function calling for both OpenAI and Anthropic providers:

**OpenAI (via Semantic Kernel)**:
- Uses `FunctionChoiceBehavior.Auto()` for automatic tool invocation
- Tools registered as kernel plugins via `KernelFunctionFactory.CreateFromMethod()`
- SK handles multi-turn conversation automatically

**Anthropic (native SDK)**:
- Uses official Anthropic SDK tool use features
- Tools converted to `ToolUnion` format with `InputSchema`
- Multi-turn loop (up to 5 iterations) handles sequential tool calls
- Tool results injected as `ToolResultBlockParam` content blocks

**ILlmTool Interface**:
```csharp
public interface ILlmTool
{
    string Name { get; }
    string Description { get; }
    JsonElement InputSchema { get; }  // JSON Schema for parameters
    Task<string> ExecuteAsync(JsonElement input, CancellationToken ct);
}
```

## Work Plans

Development work is organized into release-based work plans stored in the project root.

### File Naming Convention

```
WORK_PLAN_RELEASE_X.md   # Where X is the release number (0, 1, 2, etc.)
```

### Work Plan Structure

Each work plan should include:

1. **Overview** - Brief description of the release goals
2. **Key Decisions** - Table of architectural/technical decisions made
3. **Legend** - Status markers for tracking progress:
   ```
   - [ ] Not started
   - [x] Completed
   - [~] Skipped
   ```
4. **Sprint Plan** - Ordered list of sprints with sub-epic references
5. **Epics** - Detailed task breakdowns organized by feature area
6. **Configuration** - Any new environment variables or settings
7. **Success Criteria** - Checkboxes for release completion validation

### Sprint Plan Format

Sprints should reference specific sub-epic numbers for clarity:

```markdown
**Sprint 1: Infrastructure & Setup**
- 1.1, 1.2 (Epic 1 - Docker setup)
- 2.1, 2.2, 2.3 (Epic 2 - Database configuration)

**Sprint 2: Core Implementation**
- 3.1, 3.2, 3.3 (Epic 3 - Service implementation)
- Verify existing functionality before proceeding

**Sprint 3: Integration & Polish**
- 4.1, 4.2 (Epic 4 - Integration work)
- 5.1, 5.2, 5.3 (Epic 5 - Testing and documentation)
```

### Epic Format

Each epic should have numbered sub-sections with checkbox items:

```markdown
## Epic 1: Feature Name

### 1.1 Sub-feature
- [ ] Task description
- [ ] Another task

### 1.2 Another Sub-feature
- [ ] Task description
```

## Context7 Documentation

Claude Code has access to Context7 documentation for all frameworks used in this project:
- **MudBlazor** - UI component library documentation
- **bUnit** - Blazor component testing framework
- **Semantic Kernel** - Microsoft AI orchestration SDK
- **FluentMigrator** - Database migration framework
- **Polly** - Resilience and transient-fault-handling library
- **Dapper** - Micro ORM for database access

Use the Context7 MCP tools (`mcp__context7__resolve-library-id` and `mcp__context7__get-library-docs`) to fetch up-to-date documentation when implementing features or troubleshooting issues.

## Reference: LlmCouncil Python Implementation

The original Python implementation is in `LlmCouncil/`:

```bash
# Backend
cd LlmCouncil && uv run python -m backend.main

# Frontend
cd LlmCouncil/frontend && npm run dev
```

Backend: http://localhost:8001
Frontend: http://localhost:5173
