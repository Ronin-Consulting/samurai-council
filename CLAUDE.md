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
│   ├── dev-up.sh                 # One-command Docker launcher (build + start + wait)
│   ├── dev-up.ps1                # Windows/PowerShell wrapper (delegates to WSL)
│   ├── init-contoso.sh           # Initialize ContosoRetailDW database
│   └── sql/                      # SQL scripts
│       └── init-contoso-db.sql   # Database restore and user setup
│
├── data/                         # Data files (gitignored)
│   └── contoso/                  # ContosoRetailDW.bak goes here
│
├── clients/angular/              # Angular 22 SPA (UI layer)
│   └── src/app/
│       ├── app.ts / app.html     # Shell: topbar, sidebar host, router outlet
│       ├── app.routes.ts         # Routes: '', 'chat/:id', 'gallery'
│       ├── components/           # chat-page, sidebar, assistant-message,
│       │   │                     #   visual-display, chart-display, stat-tile,
│       │   │                     #   data-table, gallery-page
│       │   └── studio/           # Studio (V2) pre-made cards
│       ├── services/             # api.service, conversation.store (signals),
│       │                         #   council-stream.service (SSE), theme, view-mode
│       ├── util.ts               # dataviz palette + ECharts option builders
│       └── styles.css            # Tailwind + design system (prose, Source Serif)
│
├── SamurAICouncil.Api/           # ASP.NET Core Web API (hosts the SPA + SSE)
│   ├── Controllers/              # ConversationsController (CRUD + SSE + export)
│   ├── Program.cs                # DI + static-file/SPA fallback + migrations
│   ├── appsettings.json
│   └── Dockerfile                # Multi-stage: build Angular → publish API → wwwroot
│
├── SamurAICouncil.Core/          # Domain models, interfaces, business logic
│   ├── Configuration/            # CouncilConfiguration, LlmApiKeys, CompanyData
│   ├── Interfaces/               # ILlmService, ICouncilService, ILlmTool, repositories
│   ├── Models/                   # Domain entities (incl. ChartModels)
│   ├── Resources/                # Embedded ContosoRetailDW schema
│   └── Services/                 # CouncilService, CouncilConversationService,
│                                 #   SemanticKernelLlmService, CompanyData*,
│                                 #   StudioClassifier, Export/ (PDF + Excel)
│
├── SamurAICouncil.Data/          # Data access layer
│   ├── Repositories/             # Dapper-based repositories
│   ├── Resilience/               # Polly retry + circuit breaker policies
│   └── Migrations/               # FluentMigrator migrations
│
├── SamurAICouncil.Core.Tests/    # Unit tests for Core
├── SamurAICouncil.Data.Tests/    # Integration tests with TestContainers
├── SamurAICouncil.Api.Tests/     # API integration + StudioClassifier tests
│
└── LlmCouncil/                   # Python/React reference implementation
```

## Technology Stack

| Layer | Technology |
|-------|------------|
| UI | Angular 22 SPA (standalone components, signals, zoneless) |
| Styling | Tailwind CSS 4 (class dark mode), @tailwindcss/typography, Source Serif 4 |
| Charts | ngx-echarts (Apache ECharts 6) |
| Markdown | ngx-markdown + Prism (client); Markdig (server, exports) |
| API | ASP.NET Core Web API (.NET 10), HTTP + Server-Sent Events |
| LLM Integration | Microsoft Semantic Kernel (OpenAI, Anthropic, Google connectors) |
| Database | PostgreSQL 16 (app data), SQL Server 2022 (ContosoRetailDW) |
| ORM | Dapper with JSON column support |
| Migrations | FluentMigrator |
| Resilience | Polly (retry + circuit breaker for LLM and DB) |
| Testing | MSTest, Moq, TestContainers (.NET); Vitest (Angular unit) |
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

Results stream to the browser over SSE. `CouncilConversationService` orchestrates a turn and emits events in order: `loading → stage1 → loading → stage2 → loading → stage3 → title → done` (or `error`).

## Key Components

### Core Services

| Service | Purpose |
|---------|---------|
| `CouncilService` | Orchestrates the 3-stage deliberation (Stage1/2/3, title gen) |
| `CouncilConversationService` | Runs a conversation turn, streams SSE, persists, propagates charts |
| `SemanticKernelLlmService` | Multi-provider LLM integration (OpenAI, Anthropic, Google) |
| `ResilientLlmService` | Polly-wrapped decorator for LLM calls |
| `CompanyDataService` | Text-to-SQL queries against ContosoRetailDW |
| `CompanyDataTool` | ILlmTool implementation for SQL tool calling |
| `CompanyDataPlugin` | Semantic Kernel plugin wrapper for CompanyDataTool |
| `StudioClassifier` | Deterministic (V2 "Studio") chart-form choice from result shape |
| `ChartDataTransformer` | Parses and validates LLM chart recommendations |
| `RankingParser` | Extracts rankings from model responses |
| `AggregateRankingCalculator` | Computes consensus rankings |
| `AnonymizationHelper` | Manages response anonymization |
| `Export/` | PDF (QuestPDF) + Excel (ClosedXML) export services |

### Angular Client (`clients/angular/src/app`)

Standalone components with signal-based state; no NgModules. The API is reached via a same-origin `/api` prefix (proxied to `:5131` in dev).

| Piece | Purpose |
|-------|---------|
| `app.ts` / `app.html` | Shell: topbar (brand, Classic/Studio + theme toggles), sidebar host, router outlet |
| `components/chat-page.ts` | Main chat: welcome screen, message list, input, council orchestration |
| `components/sidebar.ts` | Conversation list and navigation |
| `components/assistant-message.ts` | Final / Responses / Rankings tabs; loading stepper; renders the visual |
| `components/visual-display.ts` | Classic (V1) dispatcher → chart-display / stat-tile / data-table |
| `components/chart-display.ts` | Renders a `ChartRecommendation` via ngx-echarts (`chartToEChartsOption`) |
| `components/stat-tile.ts`, `data-table.ts` | KPI tiles and the table/a11y fallback |
| `components/studio/*` | Studio (V2) pre-made catalog: studio-display (dispatcher) + studio-card, studio-chart, studio-kpi, studio-table |
| `components/gallery-page.ts` | `/gallery` — every form in both modes with sample data (design/QA) |
| `services/api.service.ts` | HTTP client for the conversations API |
| `services/conversation.store.ts` | `ConversationStore` — signal store for conversations + current messages |
| `services/council-stream.service.ts` | Fetch-based SSE reader for the council stream |
| `services/theme.service.ts` | Dark/light theme (class-based, localStorage) |
| `services/view-mode.service.ts` | Classic ↔ Studio toggle (localStorage) |

### Chart Visualization

Data-query results are visualized in **two switchable modes** (toggled in the topbar). See `docs/implementation-review.md` §5 for the full Classic-vs-Studio comparison.

- **Classic (V1, generative):** the LLM picks the chart form and payload; one generic renderer (`util.chartToEChartsOption`) draws it via ngx-echarts.
- **Studio (V2, deterministic):** `StudioClassifier` inspects the SQL result's shape (column types, row/dimension/measure counts) and selects a **pre-made component**, seeded with the real rows — no LLM in the presentation path. Both payloads are produced from one tool run and carried on Stage 3 (`chart` + `studio_chart`).

**Chart forms (`ChartType`):** `None, Bar, HorizontalBar, GroupedBar, StackedBar, Line, Area, Pie, Donut, Scatter, Stat, Table`.

**Chart Flow:**
1. `CompanyDataTool` executes the SQL query against ContosoRetailDW.
2. The tool attaches both an LLM `chart` and a deterministic `studio_chart` to its `ToolUsage`.
3. The top-ranked Stage-1 response's charts propagate to Stage 3 (`Stage3.Chart` / `Stage3.StudioChart`).
4. The Angular client renders the active mode: `<app-visual-display>` (Classic) or `<app-studio-display>` (Studio).

**Key Models (`SamurAICouncil.Core/Models/ChartModels.cs`):**
- `ChartRecommendation`: engine-neutral form — `type`, `title`, `labels`, `series`, axis labels, plus `stats` (KPI), `table` (rows), and scatter `points`.
- `ChartSeriesData`: a series (name + values, or points for scatter).
- `ChartType`: the 12-member enum above.

**Example Queries that Produce Charts:**
- "Show top 5 products by revenue in 2008" → Bar chart
- "Show sales by country" → Bar chart
- "Show monthly sales trends" → Line chart
- "What percentage of sales come from each channel?" → Pie chart (Classic)

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

# Run all .NET tests
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
# One command (recommended): build the image from the current source and start the stack
./scripts/dev-up.sh              # or, from Windows PowerShell: .\scripts\dev-up.ps1
#   up (default) | logs | down | rebuild

# Equivalent raw Docker (requires OPENAI_API_KEY in .env)
docker compose up --build
docker compose logs -f web
docker compose down
```

**The application runs on http://localhost:5080**

The `web` image is multi-stage (`SamurAICouncil.Api/Dockerfile`): stage 1 builds the Angular SPA (Node), stage 2 publishes the .NET API, stage 3 copies the Angular build into the API's `wwwroot` and serves it (SPA fallback). Rebuilding the image (`--build`) is what surfaces frontend + backend changes.

### Visual Verification with Playwright

When verifying UI changes:
- The container must be rebuilt for changes to be visible (`./scripts/dev-up.sh` re-runs the build).
- Always use port **5080** (not 5000).
- For fast UI iteration, prefer `ng serve` (:4200) over a full image rebuild.

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
dotnet run --project SamurAICouncil.Api
```

## Configuration

### appsettings.json

See `SamurAICouncil.Api/appsettings.json` for the full configuration. Key sections:

- **`LlmApiKeys`**: OpenAI, Anthropic, Google API keys, plus `OpenAIEndpoint` for a custom OpenAI-compatible base URL (the FortyAU gateway).
- **`Council.CouncilModels`**: Array of `{Provider, ModelId, DisplayName}` for council members.
- **`Council.ChairmanModel`**: Model that synthesizes the final answer.
- **`Council.TitleGenerationModel`**: Lightweight model for conversation title generation.
- **`CompanyData`**: Text-to-SQL config (ConnectionString, MaxRows, QueryTimeoutSeconds, SqlGenerationModel).

### Environment Variables (Docker)

See `compose.yaml` for the full Docker configuration. Required host environment variables:

```bash
export OPENAI_API_KEY="sk-..."       # gateway token (all roles route through it)
export ANTHROPIC_API_KEY="sk-ant-..." # optional
export GOOGLE_API_KEY="..."          # optional
export MSSQL_SA_PASSWORD="..."       # optional, has default
```

**Note:** API keys are validated at startup. The council is routed through the FortyAU OpenAI-compatible gateway (`LlmApiKeys__OpenAIEndpoint=https://llm.fortyau.com/v1`): every role uses the `openai` provider pointed at that endpoint, with the `OPENAI_API_KEY` holding the gateway token. The `compose.yaml` currently configures council models `gpt-4o`, `gpt-4.1`, `gemini-2.5-flash`, chairman `claude-opus-4-1`, title-gen `gpt-4o-mini`, and SQL generation `gpt-4o`.

### Trying open-source models via OpenRouter (alternate profile)

Because every agent role is already routed through the generic `LlmApiKeys.OpenAIEndpoint`
override (`AddOpenAiChatCompletion` in `SamurAICouncil.Core/Services/SemanticKernelLlmService.cs`),
swapping in a different OpenAI-compatible provider needs **no code changes** — just a different
endpoint, key, and model ids. `compose.yaml`'s LLM-related env lines all have `${VAR:-default}`
fallbacks, so an alternate `.env.openrouter` profile coexists with the default FortyAU one:

```bash
cp .env.openrouter.example .env.openrouter   # then set OPENAI_API_KEY to your OpenRouter key
docker compose --env-file .env.openrouter up -d --build
# or: ./scripts/dev-up.sh up openrouter  /  .\scripts\dev-up.ps1 up openrouter
```

Default model mapping (open-source models via [OpenRouter](https://openrouter.ai)):

| Role | Model |
|---|---|
| Council member 1 | `z-ai/glm-5.2` |
| Council member 2 | `minimax/minimax-m2.7` |
| Council member 3 | `qwen/qwen3.7-max` |
| Chairman | `deepseek/deepseek-v4-pro` |
| Title generation | `deepseek/deepseek-v4-flash` |
| SQL generation | `qwen/qwen3-coder-next` |

Council member 2, SQL generation, and title generation are chosen for agentic tool-calling
reliability, schema-precision-critical generation, and cost/speed on a trivial task,
respectively — see model list above. A stuck/dead OpenRouter call can otherwise retry 4x at
~100s each (~400s worst case) before the pipeline's graceful degradation (missing ranking /
chairman fallback) kicks in; `.env.openrouter` sets `OPENAI_MAX_RETRIES=1` (only applied for
this profile — see `LlmApiKeysConfiguration.OpenAIMaxRetries`) to cap that at ~200s.

Revert to the default FortyAU profile any time with a plain `docker compose up -d --build`
(no `--env-file`) — the two profiles are fully independent.

## Testing

```bash
# Run all .NET tests
dotnet test

# Run specific test project
dotnet test SamurAICouncil.Core.Tests
dotnet test SamurAICouncil.Data.Tests      # Requires Docker (TestContainers)
dotnet test SamurAICouncil.Api.Tests        # API integration + StudioClassifier

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~CouncilServiceTests"

# Angular unit tests
cd clients/angular && ng test
```

### Test Projects

| Project | Focus |
|---------|-------|
| Core.Tests | Business logic — deliberation, ranking, anonymization, chart validation |
| Data.Tests | Repository integration via TestContainers (PostgreSQL) |
| Api.Tests | API integration (`WebApplicationFactory`, SSE ordering) + `StudioClassifier` shape tests |
| Angular (Vitest) | `util.spec.ts` — chart mapping / dispatch |

## Architecture Decisions

### Why Angular SPA + Web API?
- Single origin in production: the API serves the built Angular app from `wwwroot` with SPA fallback, so no CORS.
- SSE streams stage results to the browser as they complete.
- API keys stay server-side; the SPA only talks to the API.

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
- API returns a structured `error` SSE event / error `Stage3` on failure
- The Angular client renders a user-facing error message in the answer panel
- Graceful fallback when the chairman returns empty (uses the first Stage 1 response)
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

Claude Code has access to Context7 documentation for the frameworks used in this project:
- **Angular** - SPA framework (standalone components, signals)
- **Tailwind CSS** - utility-first styling
- **ngx-echarts / Apache ECharts** - chart rendering
- **Prism** - code syntax highlighting
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
