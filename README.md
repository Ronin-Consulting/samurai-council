<div align="center">
  <h1>SamurAI Council</h1>
  <p><strong>Multi-Model AI Deliberation Platform</strong></p>
  <p>
    <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10" />
    <img src="https://img.shields.io/badge/Angular-22-DD0031?logo=angular&logoColor=white" alt="Angular 22" />
    <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL" />
    <img src="https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white" alt="Docker" />
  </p>
</div>

SamurAI Council is an Angular SPA + .NET 10 Web API that orchestrates a 3-stage deliberation process across multiple AI models (OpenAI, Anthropic, Google) to produce higher-quality, peer-validated responses. Models independently respond to queries, anonymously rank each other's work, and a chairman model synthesizes the best elements into a final answer.

Originally a Blazor Server application (since migrated to Angular + Web API), and before that converted from the [LlmCouncil](#reference-implementation) Python/React reference implementation.

---

## Table of Contents

- [How It Works](#how-it-works)
- [Architecture Overview](#architecture-overview)
- [Project Structure](#project-structure)
- [Technology Stack](#technology-stack)
- [Getting Started](#getting-started)
- [Docker Deployment](#docker-deployment)
- [Configuration Reference](#configuration-reference)
- [Core Services](#core-services)
- [Angular Client](#angular-client)
- [LLM Integration](#llm-integration)
- [Tool Calling & Text-to-SQL](#tool-calling--text-to-sql)
- [Chart Visualization](#chart-visualization)
- [Export Features](#export-features)
- [Data Layer](#data-layer)
- [Resilience & Fault Tolerance](#resilience--fault-tolerance)
- [Testing](#testing)
- [Reference Implementation](#reference-implementation)

---

## How It Works

SamurAI Council implements a **3-Stage Deliberation Architecture** inspired by academic peer review:

```
User Query
    |
    v
+-------------------------------------+
|  Stage 1: Collect Responses          |
|  - Query all council models in       |
|    parallel                          |
|  - Each model provides independent   |
|    response                          |
|  - Tools (e.g., SQL queries) are     |
|    available if relevant             |
+-------------------+-----------------+
                    |
                    v
+-------------------------------------+
|  Stage 2: Anonymous Peer Review      |
|  - Anonymize responses (A, B, C)     |
|  - Each model ranks all responses    |
|  - Rankings parsed and aggregated    |
|  - Consensus scoring identifies      |
|    strongest answers                 |
+-------------------+-----------------+
                    |
                    v
+-------------------------------------+
|  Stage 3: Chairman Synthesis         |
|  - Chairman model reviews all        |
|    responses + peer evaluations      |
|  - Synthesizes final answer from     |
|    strongest elements                |
|  - Chart data propagated from        |
|    top-ranked Stage 1 response       |
+-------------------+-----------------+
                    |
                    v
             Final Response
       (with optional chart visualization)
```

Stage results stream to the browser over **Server-Sent Events** (`loading → stage1 → stage2 → stage3 → title → done`).

### Stage 1: Independent Response Generation

Multiple AI models from different providers independently analyze the same query. Each brings unique strengths from different training data and architectures. If the query involves company data (detected via keyword matching), models are given access to the `query_company_data` tool for Text-to-SQL queries against the ContosoRetailDW database. All models are queried in parallel using `Task.WhenAll`.

### Stage 2: Anonymous Peer Review

Responses are anonymized using letter labels (Response A, B, C) via `AnonymizationHelper`. Each model receives all anonymized responses and ranks them by quality. `RankingParser` extracts structured rankings from the free-text evaluations. `AggregateRankingCalculator` computes consensus scores across all reviewers, producing a sorted list of `AggregateRanking` objects with average rank positions.

### Stage 3: Chairman Synthesis

A designated chairman model (configurable, defaults to a more capable model) receives all original responses, peer evaluations, and aggregate rankings. It synthesizes the best final answer as a concise executive brief. If any Stage 1 response generated chart data via tool use, the charts from the top-ranked response are propagated to the Stage 3 result for display.

---

## Architecture Overview

```
+---------------------------------------------------------+
|              Angular 22 SPA  (clients/angular)          |
|  Standalone components + signals, Tailwind, ngx-echarts |
|                                                         |
|  components/            services/                       |
|  - chat-page            - api.service                   |
|  - sidebar              - conversation.store (signals)  |
|  - assistant-message    - council-stream.service (SSE)  |
|  - visual-display       - theme.service                 |
|  - chart-display        - view-mode.service             |
|  - stat-tile,data-table                                 |
|  - studio/* (V2 cards)  - gallery-page                  |
+---------------------------------------------------------+
                    |  HTTP (CRUD) + SSE (council stream)
                    v
+---------------------------------------------------------+
|                   SamurAICouncil.Api                    |
|  ASP.NET Core Web API (.NET 10)                         |
|  - ConversationsController (CRUD + SSE + export)        |
|  - Serves the built Angular app from wwwroot (SPA)      |
+---------------------------------------------------------+
          |                          |
          v                          v
+-----------------------+  +-------------------------+
| SamurAICouncil.Core   |  | SamurAICouncil.Data     |
| Domain + Business     |  | Data Access Layer       |
| Logic                 |  |                         |
| - CouncilService      |  | - ConversationRepo      |
| - CouncilConversation |  | - MessageRepository     |
|   Service             |  | - ResilientConversation |
| - SemanticKernelLlm   |  |   Repository            |
|   Service             |  | - ResilientMessage      |
| - ResilientLlmService |  |   Repository            |
| - CompanyDataService  |  | - DatabaseResilience    |
| - CompanyDataTool     |  |   Policies (Polly)      |
| - StudioClassifier    |  | - FluentMigrator        |
| - ChartDataTransformer|  |   Migrations            |
| - RankingParser       |  +-------------------------+
| - AggregateRanking    |           |
|   Calculator          |           v
| - AnonymizationHelper |  +-------------------+
| - Export/ (PDF,Excel) |  | PostgreSQL 16     |
+-----------------------+  | - conversations   |
          |                | - messages (JSONB)|
          v                +-------------------+
+-------------------+
| LLM Providers     |     +-------------------+
| - OpenAI (SK)     |     | SQL Server 2022   |
| - Anthropic (SDK) |     | - ContosoRetailDW |
| - Google (SK)     |     +-------------------+
+-------------------+
(Routed through the FortyAU OpenAI-compatible gateway)
```

---

## Project Structure

```
SamurAICouncil/
├── SamurAICouncil.sln                    # Solution file (6 projects)
├── compose.yaml                          # Docker Compose (web + PostgreSQL + SQL Server)
├── CLAUDE.md                             # AI assistant instructions
├── README.md                             # This file
│
├── scripts/
│   ├── dev-up.sh                         # One-command Docker launcher (build + start + wait)
│   ├── dev-up.ps1                        # Windows/PowerShell wrapper (delegates to WSL)
│   ├── init-contoso.sh                   # Initialize ContosoRetailDW database
│   └── sql/
│       └── init-contoso-db.sql           # Database restore and user setup
│
├── data/                                 # Data files (gitignored)
│   └── contoso/                          # ContosoRetailDW.bak goes here
│
├── clients/angular/                      # Angular 22 SPA (UI layer)
│   ├── angular.json, package.json, proxy.conf.json, tailwind (postcss)
│   └── src/
│       ├── index.html, main.ts, styles.css   # bootstrap + Tailwind/design system
│       └── app/
│           ├── app.ts / app.html             # Shell (topbar, sidebar host, outlet)
│           ├── app.config.ts / app.routes.ts # Providers + routes
│           ├── models.ts, util.ts            # DTOs + dataviz palette / ECharts builders
│           ├── components/
│           │   ├── chat-page.ts              # Main chat interface
│           │   ├── sidebar.ts                # Conversation list & navigation
│           │   ├── assistant-message.ts      # Final / Responses / Rankings tabs
│           │   ├── visual-display.ts         # Classic (V1) dispatcher
│           │   ├── chart-display.ts          # ngx-echarts renderer
│           │   ├── stat-tile.ts, data-table.ts
│           │   ├── gallery-page.ts           # /gallery (both modes, sample data)
│           │   └── studio/                   # Studio (V2) pre-made cards
│           │       ├── studio-display.ts     # dispatcher
│           │       ├── studio-card.ts        # shared chrome
│           │       └── studio-chart.ts, studio-kpi.ts, studio-table.ts
│           └── services/
│               ├── api.service.ts            # HTTP client for the API
│               ├── conversation.store.ts     # ConversationStore (signals)
│               ├── council-stream.service.ts # SSE reader
│               ├── theme.service.ts          # Dark/light theme
│               └── view-mode.service.ts      # Classic ↔ Studio toggle
│
├── SamurAICouncil.Api/                   # ASP.NET Core Web API (hosts SPA + SSE)
│   ├── Controllers/
│   │   └── ConversationsController.cs    # CRUD + SSE council stream + export
│   ├── Program.cs                        # DI, static-file/SPA fallback, migrations
│   ├── appsettings.json                  # Application configuration
│   └── Dockerfile                        # Multi-stage (Angular build → publish → wwwroot)
│
├── SamurAICouncil.Core/                  # Domain models, interfaces, business logic
│   ├── Configuration/
│   │   ├── CouncilConfiguration.cs        # Council models + chairman config
│   │   ├── LlmApiKeysConfiguration.cs     # API keys + custom OpenAI endpoint
│   │   ├── LlmProviderConfiguration.cs    # Provider-level config
│   │   └── CompanyDataConfiguration.cs    # Text-to-SQL config
│   ├── Interfaces/
│   │   ├── ICouncilService.cs             # 3-stage orchestration contract
│   │   ├── ILlmService.cs                 # LLM query contract + ChatMessage
│   │   ├── ILlmTool.cs                    # Tool interface + ToolUsage + LlmQueryResult
│   │   ├── ICompanyDataService.cs         # Text-to-SQL service contract
│   │   ├── IChartDataTransformer.cs       # Chart parsing contract
│   │   ├── IConversationRepository.cs     # Conversation persistence contract
│   │   └── IMessageRepository.cs          # Message persistence contract
│   ├── Models/
│   │   ├── Conversation.cs                # Conversation + ConversationMetadata
│   │   ├── ConversationSummary.cs         # List-view summary
│   │   ├── Message.cs                     # Message (polymorphic: User/Assistant)
│   │   ├── CouncilResult.cs               # Full result + metadata + rankings
│   │   ├── StageResponses.cs              # Stage1Response, Stage2Ranking, Stage3Response
│   │   └── ChartModels.cs                 # ChartRecommendation, ChartSeriesData, ChartType (12 forms)
│   ├── Resources/
│   │   └── ContosoRetailDW_Schema.md      # Embedded DB schema for SQL generation
│   └── Services/
│       ├── CouncilService.cs              # 3-stage deliberation orchestrator
│       ├── CouncilConversationService.cs  # Conversation turn: SSE stream + persistence + chart propagation
│       ├── SemanticKernelLlmService.cs    # Multi-provider LLM service (SK + Anthropic SDK)
│       ├── ResilientLlmService.cs         # Polly-wrapped LLM decorator
│       ├── CompanyDataService.cs          # Text-to-SQL query engine
│       ├── CompanyDataTool.cs             # ILlmTool implementation for SQL queries
│       ├── CompanyDataPlugin.cs           # Semantic Kernel plugin wrapper
│       ├── StudioClassifier.cs            # Deterministic (V2) chart-form choice
│       ├── ChartDataTransformer.cs        # LLM chart recommendation parser
│       ├── RankingParser.cs               # Extracts rankings from model text
│       ├── AggregateRankingCalculator.cs  # Computes consensus rankings
│       ├── AnonymizationHelper.cs         # Response anonymization for peer review
│       └── Export/                        # PDF (QuestPDF) + Excel (ClosedXML) export
│
├── SamurAICouncil.Data/                   # Data access layer
│   ├── Repositories/
│   │   ├── ConversationRepository.cs      # Dapper-based conversation CRUD
│   │   ├── MessageRepository.cs           # Dapper-based message CRUD (JSONB)
│   │   ├── ResilientConversationRepository.cs  # Polly-wrapped conversation repo
│   │   └── ResilientMessageRepository.cs       # Polly-wrapped message repo
│   ├── Resilience/
│   │   └── DatabaseResiliencePolicies.cs  # Polly retry + circuit breaker policies
│   ├── Migrations/
│   │   ├── Migration_001_CreateConversationsTable.cs
│   │   ├── Migration_002_CreateMessagesTable.cs
│   │   └── MigrationRunner.cs             # FluentMigrator runner
│   └── ServiceCollectionExtensions.cs     # DI registration helpers
│
├── SamurAICouncil.Core.Tests/             # Unit tests (Core business logic)
├── SamurAICouncil.Data.Tests/             # Integration tests with TestContainers
├── SamurAICouncil.Api.Tests/              # API integration + StudioClassifier tests
│
└── LlmCouncil/                            # Python/React reference implementation
    ├── backend/                           # FastAPI backend
    └── frontend/                          # React frontend
```

---

## Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| **Runtime** | .NET | 10.0 |
| **UI Framework** | Angular (standalone components, signals) | 22 |
| **Styling** | Tailwind CSS | 4 |
| **Charts** | ngx-echarts / Apache ECharts | 22 / 6 |
| **Markdown (client)** | ngx-markdown + Prism, @tailwindcss/typography | 22 |
| **API** | ASP.NET Core Web API (HTTP + SSE) | .NET 10 |
| **LLM Orchestration** | Microsoft Semantic Kernel | 1.67.1 |
| **LLM - Anthropic** | Official Anthropic C# SDK | (SK + native SDK) |
| **Database (App)** | PostgreSQL | 16 (Alpine) |
| **Database (Analytics)** | SQL Server | 2022 |
| **ORM** | Dapper | 2.x |
| **Migrations** | FluentMigrator | 7.x |
| **Resilience** | Polly | 8.x |
| **Markdown (server)** | Markdig | 0.x |
| **PDF Export** | QuestPDF | 2025.x |
| **Excel Export** | ClosedXML | 0.x |
| **Testing** | MSTest, Moq, TestContainers (.NET); Vitest (Angular) | Latest |
| **Containerization** | Docker, Docker Compose | Multi-stage build |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22](https://nodejs.org/) + Angular CLI 22 (for the client)
- [Docker](https://docs.docker.com/get-docker/) (for database and deployment)
- An LLM gateway token / API key (`OPENAI_API_KEY`); Anthropic/Google keys optional

> On this host the Node/Angular and .NET tooling run inside **WSL2** (native Windows binaries crash with `0xC000001D`). Docker Engine also runs in WSL2.

### Quick Start (Docker)

```bash
# 1. Clone and enter the repository
git clone <repository-url>
cd SamurAICouncil

# 2. Configure secrets
cp .env.example .env      # then set OPENAI_API_KEY (the gateway token)

# 3. Build and start everything (one command)
./scripts/dev-up.sh       # or, from Windows PowerShell: .\scripts\dev-up.ps1

# 4. Open http://localhost:5080
```

`dev-up.sh` supports `up` (default), `logs`, `down`, and `rebuild`.

### Local Development

```bash
# 1. Start database services
docker compose up -d db sqlserver

# 2. Run the API (backend)
dotnet run --project SamurAICouncil.Api

# 3. Run the Angular client (frontend) — proxies /api to the API on :5131
cd clients/angular && ng serve      # http://localhost:4200
```

For fast UI iteration, `ng serve` (hot reload) beats a full Docker image rebuild.

### ContosoRetailDW Setup (Text-to-SQL Feature)

The optional SQL Server database enables natural-language queries against Microsoft's ContosoRetailDW demo dataset (retail data from 2007-2009):

```bash
# 1. Download ContosoRetailDW.bak from Microsoft
#    https://www.microsoft.com/en-us/download/details.aspx?id=18279

# 2. Place the .bak file in the data directory
cp ContosoRetailDW.bak ./data/contoso/

# 3. Start all services
docker compose up -d

# 4. Initialize the database (first time only)
./scripts/init-contoso.sh
```

**Connection Details:**
| Property | Value |
|----------|-------|
| Server | `localhost,1433` (or `sqlserver` from containers) |
| Database | `ContosoRetailDW` |
| Read-only User | `samurai_reader` / `Reader!Pass123` |

---

## Docker Deployment

### Services

The `compose.yaml` defines three services:

| Service | Image | Port | Purpose |
|---------|-------|------|---------|
| `web` | `samuraicouncil-web` (built from Dockerfile) | 5080:8080 | Web API serving the Angular SPA |
| `db` | `postgres:16-alpine` | 5432:5432 | Application data (conversations, messages) |
| `sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | 1433:1433 | ContosoRetailDW analytics database |

### Multi-Stage Dockerfile

`SamurAICouncil.Api/Dockerfile` builds the frontend and backend into one image:

```
Stage 1 (ngbuild) - node:22-alpine: npm ci + `ng build --configuration production`
Stage 2 (build)   - sdk:10.0: restore + `dotnet publish` the API
Stage 3 (final)   - aspnet:10.0 runtime + Kerberos lib + curl;
                    copy published API, then copy the Angular build into ./wwwroot
```

The API serves `wwwroot` as static files with SPA fallback, so the whole app runs single-origin on port 8080 (mapped to host 5080).

### Health Checks

- **Web**: `curl -f http://localhost:8080/` (30s interval)
- **PostgreSQL**: `pg_isready -U postgres` (5s interval)
- **SQL Server**: `sqlcmd SELECT 1` (10s interval, 30s start period)

### Required Environment Variables

```bash
export OPENAI_API_KEY="sk-..."          # Required (gateway token; all roles route through it)
export ANTHROPIC_API_KEY="sk-ant-..."   # Optional
export GOOGLE_API_KEY="..."             # Optional
export MSSQL_SA_PASSWORD="..."          # Optional (default: YourStrong!Passw0rd)
```

### Commands

```bash
docker compose up --build -d    # Build and start
docker compose logs -f web      # View logs
docker compose down             # Stop all services
docker compose down -v          # Stop and remove volumes
```

---

## Configuration Reference

### appsettings.json (code defaults)

```jsonc
{
  "LlmApiKeys": {
    "OpenAI": "",           // Or set via LlmApiKeys__OpenAI env var (gateway token)
    "OpenAIEndpoint": "",   // Custom OpenAI-compatible base URL (the gateway)
    "Anthropic": "",        // Or set via LlmApiKeys__Anthropic env var
    "Google": ""            // Or set via LlmApiKeys__Google env var
  },
  "Council": {
    "CouncilModels": [      // Models that participate in Stage 1 + Stage 2
      { "Provider": "openai", "ModelId": "gpt-4", "DisplayName": "GPT-4" },
      { "Provider": "anthropic", "ModelId": "claude-3-sonnet-20240229", "DisplayName": "Claude 3 Sonnet" },
      { "Provider": "google", "ModelId": "gemini-1.5-pro", "DisplayName": "Gemini 1.5 Pro" }
    ],
    "ChairmanModel": {      // Synthesizes the final answer in Stage 3
      "Provider": "anthropic", "ModelId": "claude-3-opus-20240229", "DisplayName": "Claude 3 Opus"
    },
    "TitleGenerationModel": { // Lightweight model for auto-titling conversations
      "Provider": "openai", "ModelId": "gpt-3.5-turbo", "DisplayName": "GPT-3.5"
    }
  },
  "CompanyData": {          // Text-to-SQL configuration
    "ConnectionString": "", // Empty = disabled; set to SQL Server connection string
    "MaxRows": 100,         // Maximum rows returned per query
    "QueryTimeoutSeconds": 30,
    "SqlGenerationModel": { // Model used to generate SQL from natural language
      "Provider": "anthropic", "ModelId": "claude-sonnet-4-5-20250929"
    }
  }
}
```

> These are the checked-in code defaults. In Docker, `compose.yaml` overrides them and routes **every role through the FortyAU OpenAI-compatible gateway** (`LlmApiKeys__OpenAIEndpoint=https://llm.fortyau.com/v1`): all roles use the `openai` provider pointed at that endpoint, with `OPENAI_API_KEY` as the gateway token.

### Docker Compose Models (via the gateway)

| Role | Provider | Model |
|------|----------|-------|
| Council Member 1 | openai (gateway) | `gpt-4o` |
| Council Member 2 | openai (gateway) | `gpt-4.1` |
| Council Member 3 | openai (gateway) | `gemini-2.5-flash` |
| Chairman | openai (gateway) | `claude-opus-4-1` |
| Title Generation | openai (gateway) | `gpt-4o-mini` |
| SQL Generation | openai (gateway) | `gpt-4o` |

### Alternate profile: open-source models via OpenRouter

Every role above uses the `openai` provider pointed at `LlmApiKeys.OpenAIEndpoint`, and that
override works for *any* OpenAI-compatible server — not just the FortyAU gateway. `compose.yaml`'s
LLM env lines all have `${VAR:-default}` fallbacks, so an alternate, opt-in `.env.openrouter`
profile can point every role at [OpenRouter](https://openrouter.ai) with open-source models
instead, with **no code changes** and no effect on the default profile:

```bash
cp .env.openrouter.example .env.openrouter   # then set OPENAI_API_KEY to your OpenRouter key
docker compose --env-file .env.openrouter up -d --build
# or: ./scripts/dev-up.sh up openrouter   /   .\scripts\dev-up.ps1 up openrouter
```

| Role | Model (via OpenRouter) |
|------|-------------------------|
| Council Member 1 | `z-ai/glm-5.2` |
| Council Member 2 | `minimax/minimax-m2.7` |
| Council Member 3 | `qwen/qwen3.7-max` |
| Chairman | `deepseek/deepseek-v4-pro` |
| Title Generation | `deepseek/deepseek-v4-flash` |
| SQL Generation | `qwen/qwen3-coder-next` |

Model choices favor agentic tool-calling reliability (council member 2) and schema-grounded
precision (SQL generation); see CLAUDE.md for the retry/timeout mechanism.

Revert to the default FortyAU profile any time with a plain `docker compose up -d --build`
(no `--env-file`) — the two profiles are fully independent and instantly reversible.

### API Key Validation

API keys are validated at startup against the configured providers. If a council model's provider is missing its API key, the application fails fast with a descriptive error message. This prevents runtime failures during deliberation.

---

## Core Services

### CouncilService

**File:** `SamurAICouncil.Core/Services/CouncilService.cs`

The central orchestrator that implements `ICouncilService`. Manages the full 3-stage deliberation pipeline:

| Method | Purpose |
|--------|---------|
| `RunFullCouncilAsync()` | Runs all 3 stages sequentially, returns `CouncilResult` |
| `Stage1CollectResponsesAsync()` | Queries all council models in parallel (with optional tools) |
| `Stage2CollectRankingsAsync()` | Anonymizes responses, collects peer rankings from all models |
| `Stage3SynthesizeFinalAsync()` | Chairman synthesizes final answer (concise executive brief) |
| `GenerateConversationTitleAsync()` | Auto-generates 3-5 word conversation title |

Key behaviors:
- **Tool detection**: Keyword-based matching (sales, revenue, product, etc.) determines if `CompanyDataTool` should be offered to models
- **Chart propagation**: Charts from the top-ranked Stage 1 response are propagated to Stage 3 for display
- **Fallback**: If the chairman returns empty, falls back to the first Stage 1 response

`CouncilConversationService` wraps this for the API: it runs a conversation turn, streams SSE events, persists messages, and propagates both the LLM `chart` and the deterministic `studio_chart` to Stage 3.

### SemanticKernelLlmService

**File:** `SamurAICouncil.Core/Services/SemanticKernelLlmService.cs`

Multi-provider LLM integration implementing `ILlmService`:

| Provider | Implementation |
|----------|---------------|
| **OpenAI** | Semantic Kernel `IChatCompletionService` with `FunctionChoiceBehavior.Auto()` for tool calling (supports a custom OpenAI-compatible endpoint) |
| **Google** | Semantic Kernel Google Connector with `GeminiPromptExecutionSettings` |
| **Anthropic** | Official Anthropic C# SDK with native tool use (`ToolUnion` format, up to 5 tool iterations) |

- Kernels are created per-call and disposed after use
- The Anthropic client is lazily initialized and cached for reuse
- Per-call stopwatch timing for performance logging

### CompanyDataService

**File:** `SamurAICouncil.Core/Services/CompanyDataService.cs`

Text-to-SQL engine that:
1. Receives natural language queries about company data
2. Uses an LLM to generate SQL against the ContosoRetailDW schema (embedded as a resource)
3. Executes the SQL query with parameterized timeout and row limits
4. Optionally generates chart recommendations based on query results (Stat/Table payloads are filled from the real result set)

### Supporting Services

| Service | File | Purpose |
|---------|------|---------|
| `ResilientLlmService` | `Core/Services/` | Polly-wrapped decorator for LLM calls |
| `CompanyDataTool` | `Core/Services/` | `ILlmTool` implementation - bridges natural language to SQL |
| `CompanyDataPlugin` | `Core/Services/` | Semantic Kernel plugin wrapper for `CompanyDataTool` |
| `StudioClassifier` | `Core/Services/` | Deterministic (V2) chart-form choice from result shape |
| `ChartDataTransformer` | `Core/Services/` | Parses and validates LLM chart JSON into `ChartRecommendation` |
| `RankingParser` | `Core/Services/` | Regex-based extraction of rankings from model responses |
| `AggregateRankingCalculator` | `Core/Services/` | Computes average rank positions across all peer reviews |
| `AnonymizationHelper` | `Core/Services/` | Assigns letter labels (A, B, C) to responses for blind peer review |

---

## Angular Client

The frontend (`clients/angular`) is an Angular 22 SPA of standalone components with signal-based state and no NgModules. It talks to the API via a same-origin `/api` prefix (proxied to `:5131` in development, single-origin in production).

### Routes

| Path | Component | Purpose |
|------|-----------|---------|
| `` (root) | `ChatPage` | Main chat interface |
| `chat/:id` | `ChatPage` | A specific conversation |
| `gallery` | `GalleryPage` | Every chart form in both modes with sample data (design/QA) |

### Components

| Component | Purpose |
|-----------|---------|
| `app` (shell) | Topbar (brand mark, Classic/Studio + theme toggles), sidebar host, router outlet |
| `chat-page` | Welcome screen, message list, input, council orchestration |
| `sidebar` | Conversation list, new-chat, navigation |
| `assistant-message` | Final / Responses / Rankings tabs; 3-step loading stepper; renders the visual per mode |
| `visual-display` | Classic (V1) dispatcher → `chart-display` / `stat-tile` / `data-table` |
| `chart-display` | Renders a `ChartRecommendation` via ngx-echarts |
| `stat-tile`, `data-table` | KPI tiles and the table / accessible fallback |
| `studio/studio-display` | Studio (V2) dispatcher → the pre-made card for the chosen form |
| `studio/{studio-card, studio-chart, studio-kpi, studio-table}` | Curated pre-made components |
| `gallery-page` | Renders all forms in both modes |

### Services

| Service | Purpose |
|---------|---------|
| `api.service` | HTTP client for the conversations API |
| `conversation.store` (`ConversationStore`) | Signal store for the conversation list + current messages |
| `council-stream.service` | Fetch-based SSE reader for the council stream |
| `theme.service` | Dark/light theme (class-based, persisted to localStorage) |
| `view-mode.service` | Classic ↔ Studio visualization mode (persisted) |

### Message Flow & Streaming

The chat interface provides real-time progress through SSE. When a message is sent, `ConversationStore` appends the user message and a loading assistant message, then `council-stream.service` opens the stream:

```
User sends message in chat-page
    |
    v
ConversationStore: append user + placeholder assistant (loading.stage1 = true)
    |
    v
council-stream.service POSTs to /api/conversations/{id}/messages, reads SSE frames:
    event: loading  -> update the 3-step stepper (Responses -> Review -> Synthesis)
    event: stage1   -> model responses populated; Responses tab enabled
    event: stage2   -> rankings + aggregate rankings populated; Rankings tab enabled
    event: stage3   -> chairman's final answer + charts; loading cleared
    event: title    -> conversation auto-titled (first message)
    event: done     -> processing complete
    (event: error   -> error surfaced in the answer panel)
```

State is held in signals; components re-render reactively. The Final Answer renders as a concise executive brief (bold lede + interpretation) with the chart rendered beside it.

---

## LLM Integration

### Multi-Provider Architecture

SamurAI Council uses a dual-SDK approach:

**OpenAI & Google** - via Microsoft Semantic Kernel:
- Kernel created per-call with appropriate connector
- `FunctionChoiceBehavior.Auto()` enables automatic tool invocation
- Tools registered as kernel plugins via `KernelFunctionFactory.CreateFromMethod()`
- Semantic Kernel handles multi-turn tool conversations automatically
- The OpenAI connector accepts a custom base URL (the FortyAU gateway)

**Anthropic** - via official Anthropic C# SDK:
- Direct SDK integration (not via Semantic Kernel) for full tool use control
- Tools converted to `ToolUnion` format with `InputSchema`
- Multi-turn loop (up to 5 iterations) handles sequential tool calls
- Tool results injected as `ToolResultBlockParam` content blocks
- `AnthropicClient` is lazily initialized and reused across calls

### ILlmTool Interface

```csharp
public interface ILlmTool
{
    string Name { get; }                    // e.g., "query_company_data"
    string Description { get; }             // Natural language description for LLMs
    JsonElement InputSchema { get; }        // JSON Schema for parameters
    Task<string> ExecuteAsync(JsonElement input, CancellationToken ct);
}
```

### LlmQueryResult

Every tool-enabled query returns an `LlmQueryResult` containing:
- `Content` - The model's final text response
- `ToolUsages` - List of `ToolUsage` records (tool name, input, output, optional chart + studio chart)
- `UsedTools` - Boolean convenience property

---

## Tool Calling & Text-to-SQL

### How It Works

1. **Detection**: `CouncilService` checks if the user query contains company-data keywords (sales, revenue, product, customer, etc.)
2. **Tool Provision**: If detected, `CompanyDataTool` is provided to models as an available tool
3. **SQL Generation**: When a model invokes the tool, `CompanyDataService` uses an LLM to generate SQL from the natural language query against the embedded `ContosoRetailDW_Schema.md`
4. **Execution**: Generated SQL is executed against SQL Server with configurable timeout and row limits
5. **Chart Generation**: If the query returns tabular data, both an LLM `ChartRecommendation` (Classic) and a deterministic `StudioClassifier` recommendation (Studio) are produced
6. **Response**: Results (data + optional charts) are returned to the calling model as tool output

### Tool Flow Diagram

```
Model receives tool: query_company_data
    |
    v
Model decides to call tool with: { "query": "top 5 products by revenue" }
    |
    v
CompanyDataTool.ExecuteAsync()
    |
    v
CompanyDataService.QueryCompanyDataAsync()
    |-- LLM generates SQL from natural language + embedded schema
    |-- SQL executed against ContosoRetailDW (read-only)
    |-- Results returned as CompanyDataResult
    |
    v
Chart recommendations
    |-- CompanyDataService.GenerateChartRecommendationAsync()  (LLM, Classic)
    |-- StudioClassifier.Classify()                            (deterministic, Studio)
    |
    v
Tool returns JSON: { success, sql, rowCount, data, chart, studioChart }
    |
    v
Model incorporates data into its response
```

### ContosoRetailDW Schema Overview

The embedded schema (`ContosoRetailDW_Schema.md`) describes a star-schema retail data warehouse covering 2007-2009:

**Fact Tables (8):**

| Table | Description | Key Metrics |
|-------|-------------|-------------|
| `FactSales` | Daily aggregated store sales (~3.4M rows) | SalesAmount, TotalCost, SalesQuantity |
| `FactOnlineSales` | Individual online transactions | SalesAmount, SalesQuantity |
| `FactInventory` | Weekly inventory snapshots | OnHandQuantity, OnOrderQuantity, DaysInStock |
| `FactExchangeRate` | Daily currency exchange rates | AverageRate, EndOfDayRate |
| `FactSalesQuota` | Sales plan (actual/budget/forecast) | SalesAmountQuota |
| `FactStrategyPlan` | Corporate P&L strategy | Amount |
| `FactITMachine` | Machine procurement/maintenance | CostAmount |
| `FactITSLA` | IT outage tracking | DownTime |

**Key Dimension Tables:** DimDate, DimProduct, DimStore, DimCustomer, DimGeography, DimChannel, DimPromotion, DimCurrency, DimEmployee, DimSalesTerritory

**SQL Validation:** The `CompanyDataService` validates all generated SQL before execution, rejecting INSERT, UPDATE, DELETE, DROP, CREATE, ALTER, TRUNCATE, EXEC, EXECUTE, GRANT, REVOKE, and DENY statements. Only SELECT/WITH queries are permitted.

---

## Chart Visualization

SamurAI Council visualizes data-query results in **two switchable modes**, toggled in the topbar. See `docs/implementation-review.md` §5 for the full comparison.

- **Classic (generative):** the LLM chooses the chart form and payload; a generic renderer (`util.chartToEChartsOption`) draws it via **ngx-echarts** (Apache ECharts).
- **Studio (deterministic):** `StudioClassifier` inspects the SQL result's shape and selects a **pre-made component**, seeded with the real rows — no LLM in the presentation path.

### Supported Chart Forms (`ChartType`)

`None, Bar, HorizontalBar, GroupedBar, StackedBar, Line, Area, Pie, Donut, Scatter, Stat, Table`

| Form | Use Case | Example Query |
|------|----------|---------------|
| **Bar / HorizontalBar** | Category comparisons | "Show top 5 products by revenue in 2008" |
| **Grouped / Stacked Bar** | Multi-series / composition | "Sales by channel and year" |
| **Line / Area** | Trends over time | "Show monthly sales trends" |
| **Pie / Donut** | Proportions of a whole | "What percentage of sales come from each channel?" |
| **Scatter** | Correlation between two measures | "Unit price vs quantity" |
| **Stat** | Headline number(s) | "Total revenue in 2008" |
| **Table** | Detail listings / wide results | "List all stores" |

### Chart Data Model

```csharp
public record ChartRecommendation
{
    ChartType Type { get; init; }           // 12 forms (see above)
    string Title { get; init; }             // Chart title
    string[] Labels { get; init; }          // X-axis labels / pie segments
    ChartSeriesData[] Series { get; init; } // Data series (values, or scatter points)
    string? XAxisLabel { get; init; }
    string? YAxisLabel { get; init; }
    StatData[]? Stats { get; init; }        // KPI tiles
    TableData? Table { get; init; }         // Table rows (filled from real data)
}
```

### Rendering

Charts are rendered client-side with **ngx-echarts**. The Classic path uses one generic renderer; the Studio path uses curated pre-made components. The charts from the best-ranked Stage 1 response are automatically propagated to Stage 3 (the final answer panel).

---

## Export Features

Council responses can be exported in two formats (services live in `SamurAICouncil.Core/Services/Export/`):

### PDF Export (QuestPDF)

- Full deliberation report including all 3 stages
- Markdown content rendered to PDF via `MarkdownToPdfRenderer`
- Includes query, model responses, rankings, and final synthesis
- QuestPDF Community License (companies < $1M annual revenue)

### Excel Export (ClosedXML)

- Available when tabular data exists (SQL query results or markdown tables)
- Markdown tables parsed and converted to Excel worksheets via `MarkdownToExcelRenderer`
- `IExportService.HasTabularData()` checks if export is applicable

### Download

Exports are served by `GET /api/conversations/{id}/export?format=pdf|xlsx` and downloaded by the browser.

---

## Data Layer

### Database Schema

**PostgreSQL** stores application data in two tables:

#### conversations

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | UUID | Primary Key |
| `created_at` | TIMESTAMP | NOT NULL, default UTC now |
| `title` | VARCHAR(255) | NOT NULL, default "New Conversation" |

Index: `ix_conversations_created_at` (descending) for sorted listing.

#### messages

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | UUID | Primary Key |
| `conversation_id` | UUID | FK -> conversations.id (CASCADE DELETE) |
| `role` | VARCHAR(20) | NOT NULL ("user" or "assistant") |
| `content` | TEXT | Nullable (user message text) |
| `stage1` | JSONB | Nullable (Stage 1 responses array) |
| `stage2` | JSONB | Nullable (Stage 2 rankings array) |
| `stage3` | JSONB | Nullable (Stage 3 synthesis) |
| `metadata` | JSONB | Nullable (council metadata) |
| `created_at` | TIMESTAMP | NOT NULL, default UTC now |

Indexes:
- `ix_messages_conversation_id` - lookup by conversation
- `ix_messages_conversation_created` - ordered retrieval within conversation

### Repository Pattern

Each repository has a base implementation and a resilient decorator:

| Repository | Purpose |
|------------|---------|
| `ConversationRepository` | Dapper-based CRUD for conversations |
| `ResilientConversationRepository` | Polly-wrapped decorator with retry + circuit breaker |
| `MessageRepository` | Dapper-based message CRUD with JSONB serialization |
| `ResilientMessageRepository` | Polly-wrapped decorator |

### Domain Models

Messages use JSON polymorphic serialization:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "role")]
[JsonDerivedType(typeof(UserMessage), "user")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
public abstract class Message { }
```

`AssistantMessage` contains nullable `Stage1`, `Stage2`, `Stage3`, and `Metadata` properties that are progressively populated during deliberation and stored as JSONB.

### Migrations

FluentMigrator migrations run automatically on application startup:

```csharp
// In SamurAICouncil.Api/Program.cs
MigrationRunner.RunMigrations(scope.ServiceProvider);
```

---

## Resilience & Fault Tolerance

### Database Resilience (Polly)

**File:** `SamurAICouncil.Data/Resilience/DatabaseResiliencePolicies.cs`

| Policy | Configuration | Purpose |
|--------|--------------|---------|
| **Retry** | 3 retries, exponential backoff (200ms, 400ms, 800ms) | Handle transient PostgreSQL errors |
| **Circuit Breaker** | Opens after 5 failures, 30s break duration | Prevent cascade failures |
| **Combined** | Circuit breaker wraps retry | Full resilience stack |

Handles specific PostgreSQL error codes:
- Connection errors (08xxx)
- Shutdown events (57Pxx)
- Serialization/deadlock (40xxx)
- Resource exhaustion (53xxx)

### LLM Resilience

`ResilientLlmService` wraps `ILlmService` with a composed Polly policy stack:

| Policy | Configuration | Purpose |
|--------|--------------|---------|
| **Timeout** | per-call | Prevent hanging LLM requests |
| **Circuit Breaker** | Opens after repeated failures | Prevent cascade failures to LLM providers |
| **Retry** | exponential backoff | Handle transient HTTP failures |

Handles `HttpRequestException`, non-cancellation `TaskCanceledException`, and `TimeoutRejectedException`. Returns null/empty on final failure rather than propagating exceptions.

### Application-Level Error Handling

- The API surfaces failures as an `error` SSE event / error `Stage3`
- The Angular client renders a user-friendly error message in the answer panel
- Graceful fallback when chairman model returns empty (uses first Stage 1 response)

---

## Testing

### Test Suite Overview

| Project | Focus | Frameworks |
|---------|-------|------------|
| `SamurAICouncil.Core.Tests` | Business logic, services, ranking, anonymization, chart validation | MSTest, Moq |
| `SamurAICouncil.Data.Tests` | Database integration, repository CRUD | MSTest, TestContainers |
| `SamurAICouncil.Api.Tests` | API integration (SSE ordering) + `StudioClassifier` shape tests | MSTest, WebApplicationFactory |
| `clients/angular` | Chart mapping / dispatch unit tests (`util.spec.ts`) | Vitest |

### Running Tests

```bash
# Run all .NET tests
dotnet test

# Run specific test project
dotnet test SamurAICouncil.Core.Tests
dotnet test SamurAICouncil.Data.Tests      # Requires Docker (TestContainers)
dotnet test SamurAICouncil.Api.Tests

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~CouncilServiceTests"

# Angular unit tests
cd clients/angular && ng test
```

### Testing Patterns

- **Unit tests** (Core.Tests): Mock `ILlmService` via Moq, test deliberation logic, ranking calculations, anonymization, and chart validation
- **Integration tests** (Core.Tests): Opt-in LLM integration tests using real API keys (loaded from `env_vars` file or environment), tagged with `[TestCategory("Integration")]`, skip gracefully via `Assert.Inconclusive` when keys are absent
- **Integration tests** (Data.Tests): TestContainers spins up real `postgres:16-alpine`, tests repository CRUD + JSONB round-trip, cascade deletes; runs sequentially (`[assembly: DoNotParallelize]`)
- **API tests** (Api.Tests): `WebApplicationFactory` exercises the controller end-to-end (including SSE event ordering); `StudioClassifierTests` assert the deterministic form choice for each result shape
- **Frontend tests** (Angular): Vitest specs for the chart-mapping / dispatch logic in `util.ts`

---

## Reference Implementation

The original Python/React implementation is in `LlmCouncil/`:

```bash
# Backend (FastAPI)
cd LlmCouncil && uv run python -m backend.main
# Runs on http://localhost:8001

# Frontend (React)
cd LlmCouncil/frontend && npm run dev
# Runs on http://localhost:5173
```

The C# implementation faithfully reproduces the 3-stage deliberation pattern while adding enterprise features: database persistence, resilience policies, PDF/Excel export, chart visualization, and tool calling.

---

## Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| **Angular SPA + Web API** (migrated from Blazor Server) | Single-origin static hosting (API serves the SPA from `wwwroot`), SSE streaming of stage results, API keys stay server-side |
| **Server-Sent Events** for the council stream | One-way, text-based streaming fits the staged pipeline; simpler than WebSockets |
| **Semantic Kernel** for LLM integration | Native .NET support for OpenAI + Google, consistent API, built-in function calling; supports a custom OpenAI-compatible gateway |
| **Anthropic SDK** (direct, not via SK) | Full control over Anthropic's native tool use protocol with multi-turn support |
| **PostgreSQL + Dapper** | JSONB columns for flexible stage data storage; Dapper provides full SQL control |
| **FluentMigrator** | Code-first schema migrations that run on startup |
| **Polly** for resilience | Industry-standard retry + circuit breaker for both LLM APIs and database |
| **Tailwind + ngx-echarts** for UI | Utility-first styling with a themed design system; ECharts for rich, engine-neutral charts |
| **Two visualization modes** (Classic + Studio) | Compare a generative (LLM-chosen) vs deterministic (rule-based) approach to charting |

---

*SamurAI Council -- Where AI Models Deliberate, So You Can Decide with Confidence*
