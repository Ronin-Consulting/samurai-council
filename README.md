<div align="center">
  <img src="SamurAICouncil.Web/wwwroot/smaurai_council.png" alt="SamurAI Council" width="600" />
  <h1>SamurAI Council</h1>
  <p><strong>Multi-Model AI Deliberation Platform</strong></p>
  <p>
    <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10" />
    <img src="https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor" alt="Blazor Server" />
    <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL" />
    <img src="https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white" alt="Docker" />
    <img src="https://img.shields.io/badge/Tests-360-green" alt="360 Tests" />
  </p>
</div>

SamurAI Council is a .NET 10 Blazor Server application that orchestrates a 3-stage deliberation process across multiple AI models (OpenAI, Anthropic, Google) to produce higher-quality, peer-validated responses. Models independently respond to queries, anonymously rank each other's work, and a chairman model synthesizes the best elements into a final answer.

Originally converted from the [LlmCouncil](#reference-implementation) Python/React reference implementation.

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
- [Blazor Components](#blazor-components)
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

### Stage 1: Independent Response Generation

Multiple AI models from different providers independently analyze the same query. Each brings unique strengths from different training data and architectures. If the query involves company data (detected via keyword matching), models are given access to the `query_company_data` tool for Text-to-SQL queries against the ContosoRetailDW database. All models are queried in parallel using `Task.WhenAll`.

### Stage 2: Anonymous Peer Review

Responses are anonymized using letter labels (Response A, B, C) via `AnonymizationHelper`. Each model receives all anonymized responses and ranks them by quality. `RankingParser` extracts structured rankings from the free-text evaluations. `AggregateRankingCalculator` computes consensus scores across all reviewers, producing a sorted list of `AggregateRanking` objects with average rank positions.

### Stage 3: Chairman Synthesis

A designated chairman model (configurable, defaults to a more capable model) receives all original responses, peer evaluations, and aggregate rankings. It synthesizes the best final answer. If any Stage 1 response generated chart data via tool use, the chart from the top-ranked response is propagated to the Stage 3 result for display.

---

## Architecture Overview

```
+---------------------------------------------------------+
|                   SamurAICouncil.Web                    |
|  Blazor Server (.NET 10, Interactive Server Mode)       |
|                                                         |
|  Components/Pages/       Components/Shared/             |
|  - Chat.razor            - AssistantMessagePanel        |
|  - Error.razor           - Stage1Panel, Stage2Panel,    |
|  - NotFound.razor          Stage3Panel                  |
|                          - ChartDisplay, ToolUsagePanel |
|  Components/Layout/      - MarkdownRenderer, Logo       |
|  - MainLayout            - TabNavigation, LoadingSpinner|
|  - Sidebar               - AppErrorBoundary             |
|  - ReconnectModal                                       |
|                                                         |
|  Services/                                              |
|  - ConversationService   - ConversationState            |
|  - PdfExportService      - ThemeService                 |
|  - MarkdownToPdfRenderer - MarkdownToExcelRenderer      |
+---------------------------------------------------------+
          |                          |
          v                          v
+-----------------------+  +-------------------------+
| SamurAICouncil.Core   |  | SamurAICouncil.Data     |
| Domain + Business     |  | Data Access Layer       |
| Logic                 |  |                         |
| - CouncilService      |  | - ConversationRepo      |
| - SemanticKernelLlm   |  | - MessageRepository     |
|   Service             |  | - ResilientConversation |
| - ResilientLlmService |  |   Repository            |
| - CompanyDataService  |  | - ResilientMessage      |
| - CompanyDataTool     |  |   Repository            |
| - CompanyDataPlugin   |  | - DatabaseResilience    |
| - ChartDataTransformer|  |   Policies (Polly)      |
| - RankingParser       |  | - FluentMigrator        |
| - AggregateRanking    |  |   Migrations            |
|   Calculator          |  +-------------------------+
| - AnonymizationHelper |           |
+-----------------------+           v
          |               +-------------------+
          v               | PostgreSQL 16     |
+-------------------+     | - conversations   |
| LLM Providers     |     | - messages (JSONB)|
| - OpenAI (SK)     |     +-------------------+
| - Anthropic (SDK) |
| - Google (SK)     |     +-------------------+
+-------------------+     | SQL Server 2022   |
                          | - ContosoRetailDW |
                          +-------------------+
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
│   ├── init-contoso.sh                   # Initialize ContosoRetailDW database
│   └── sql/
│       └── init-contoso-db.sql           # Database restore and user setup
│
├── data/                                 # Data files (gitignored)
│   └── contoso/                          # ContosoRetailDW.bak goes here
│
├── SamurAICouncil.Web/                   # Blazor Server app (UI layer)
│   ├── Components/
│   │   ├── App.razor                     # Root application component
│   │   ├── Routes.razor                  # Routing configuration
│   │   ├── _Imports.razor                # Global using directives
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor          # Application shell with sidebar
│   │   │   ├── MainLayout.razor.css      # Scoped layout styles
│   │   │   ├── Sidebar.razor             # Conversation list & navigation
│   │   │   ├── Sidebar.razor.css         # Scoped sidebar styles
│   │   │   ├── ReconnectModal.razor      # SignalR reconnection UI
│   │   │   └── ReconnectModal.razor.css  # Scoped reconnect styles
│   │   ├── Pages/
│   │   │   ├── Chat.razor                # Main chat interface (/, /chat, /chat/{id})
│   │   │   ├── Error.razor               # Error page
│   │   │   └── NotFound.razor            # 404 page
│   │   └── Shared/
│   │       ├── AssistantMessagePanel.razor # Orchestrates stage panel display
│   │       ├── Stage1Panel.razor          # Tabbed view of model responses
│   │       ├── Stage2Panel.razor          # Rankings with de-anonymization
│   │       ├── Stage3Panel.razor          # Chairman's synthesized answer
│   │       ├── ChartDisplay.razor         # MudBlazor chart rendering
│   │       ├── ToolUsagePanel.razor       # SQL query & result display
│   │       ├── MarkdownRenderer.razor     # Markdig-based markdown rendering
│   │       ├── TabNavigation.razor        # Reusable tab control
│   │       ├── LoadingSpinner.razor       # Animated loading indicator
│   │       ├── Logo.razor                 # Application logo component
│   │       └── AppErrorBoundary.razor     # Global error boundary
│   ├── Services/
│   │   ├── ConversationService.cs         # CRUD for conversations (DB persistence)
│   │   ├── ConversationState.cs           # Scoped UI state management
│   │   ├── ThemeService.cs                # Dark/light theme management
│   │   ├── IExportService.cs              # Export service interface
│   │   ├── PdfExportService.cs            # PDF + Excel export implementation
│   │   ├── MarkdownToPdfRenderer.cs       # Markdown-to-QuestPDF conversion
│   │   └── MarkdownToExcelRenderer.cs     # Markdown table-to-Excel conversion
│   ├── wwwroot/
│   │   ├── app.css                        # Global styles
│   │   ├── js/
│   │   │   ├── fileDownload.js            # JS interop for file downloads
│   │   │   └── markdown-enhance.js        # Client-side markdown enhancements
│   │   ├── favicon.svg                    # Browser favicon
│   │   ├── helmet.svg                     # Helmet icon asset
│   │   ├── logo.png                       # Full logo
│   │   ├── logo-white-red-768x249.png     # Logo variant
│   │   └── smaurai_council.png            # Welcome screen image
│   ├── Program.cs                         # Application entry point & DI configuration
│   ├── Dockerfile                         # Multi-stage Docker build
│   └── appsettings.json                   # Application configuration
│
├── SamurAICouncil.Core/                   # Domain models, interfaces, business logic
│   ├── Configuration/
│   │   ├── CouncilConfiguration.cs        # Council models + chairman config
│   │   ├── LlmApiKeysConfiguration.cs     # API key management + validation
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
│   │   ├── Message.cs                     # Message (polymorphic: User/Assistant)
│   │   ├── CouncilResult.cs               # Full result + metadata + rankings
│   │   ├── StageResponses.cs              # Stage1Response, Stage2Ranking, Stage3Response
│   │   └── ChartModels.cs                 # ChartRecommendation, ChartSeriesData, ChartType
│   ├── Resources/
│   │   └── ContosoRetailDW_Schema.md      # Embedded DB schema for SQL generation
│   └── Services/
│       ├── CouncilService.cs              # 3-stage deliberation orchestrator
│       ├── SemanticKernelLlmService.cs    # Multi-provider LLM service (SK + Anthropic SDK)
│       ├── ResilientLlmService.cs         # Polly-wrapped LLM decorator
│       ├── CompanyDataService.cs          # Text-to-SQL query engine
│       ├── CompanyDataTool.cs             # ILlmTool implementation for SQL queries
│       ├── CompanyDataPlugin.cs           # Semantic Kernel plugin wrapper
│       ├── ChartDataTransformer.cs        # LLM chart recommendation parser
│       ├── RankingParser.cs               # Extracts rankings from model text
│       ├── AggregateRankingCalculator.cs  # Computes consensus rankings
│       └── AnonymizationHelper.cs         # Response anonymization for peer review
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
├── SamurAICouncil.Core.Tests/             # Unit tests (232 tests)
├── SamurAICouncil.Data.Tests/             # Integration tests with TestContainers (18 tests)
├── SamurAICouncil.Web.Tests/              # bUnit + integration tests (110 tests)
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
| **UI Framework** | Blazor Server (Interactive Server Mode) | .NET 10 |
| **UI Components** | MudBlazor | 8.15.0 |
| **LLM Orchestration** | Microsoft Semantic Kernel | 1.67.1 |
| **LLM - OpenAI** | Semantic Kernel OpenAI Connector | 1.67.1 |
| **LLM - Google** | Semantic Kernel Google Connector | 1.67.1-alpha |
| **LLM - Anthropic** | Official Anthropic C# SDK | 10.4.0 |
| **Database (App)** | PostgreSQL | 16 (Alpine) |
| **Database (Analytics)** | SQL Server | 2022 |
| **ORM** | Dapper | 2.1.66 |
| **Migrations** | FluentMigrator | 7.1.0 |
| **Resilience** | Polly | 8.6.5 |
| **Markdown** | Markdig | 0.44.0 |
| **PDF Export** | QuestPDF | 2025.7.4 |
| **Excel Export** | ClosedXML | 0.105.0 |
| **SQL Client** | Microsoft.Data.SqlClient | 6.1.3 |
| **Testing** | MSTest, bUnit, Moq, TestContainers | Latest |
| **Containerization** | Docker, Docker Compose | Multi-stage build |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) (for database and deployment)
- API keys for at least two LLM providers:
  - **OpenAI** API key (`OPENAI_API_KEY`)
  - **Anthropic** API key (`ANTHROPIC_API_KEY`)
  - **Google AI** API key (`GOOGLE_API_KEY`) - optional

### Quick Start (Docker)

```bash
# 1. Clone the repository
git clone <repository-url>
cd SamurAICouncil

# 2. Set API keys
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."
export GOOGLE_API_KEY="..."  # Optional

# 3. Build and start all services
docker compose up --build -d

# 4. Open in browser
open http://localhost:5080
```

### Local Development

```bash
# 1. Start database services
docker compose up -d db sqlserver

# 2. Build the solution
dotnet build

# 3. Run the web project (with hot reload)
dotnet watch --project SamurAICouncil.Web

# 4. Open http://localhost:5080
```

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
| `web` | `samuraicouncil-web` (built from Dockerfile) | 5080:8080 | Blazor Server application |
| `db` | `postgres:16-alpine` | 5432:5432 | Application data (conversations, messages) |
| `sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | 1433:1433 | ContosoRetailDW analytics database |

### Multi-Stage Dockerfile

```
Stage 1 (base)    - aspnet:10.0 runtime + Kerberos lib + curl for health checks
Stage 2 (build)   - sdk:10.0, restore dependencies, build solution
Stage 3 (publish) - dotnet publish with UseAppHost=false
Stage 4 (final)   - Copy published output to runtime image
```

### Health Checks

- **Web**: `curl -f http://localhost:8080/health` (30s interval)
- **PostgreSQL**: `pg_isready -U postgres` (5s interval)
- **SQL Server**: `sqlcmd SELECT 1` (10s interval, 30s start period)

### Required Environment Variables

```bash
export OPENAI_API_KEY="sk-..."          # Required
export ANTHROPIC_API_KEY="sk-ant-..."   # Required
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

### appsettings.json

```jsonc
{
  "LlmApiKeys": {
    "OpenAI": "",           // Or set via LlmApiKeys__OpenAI env var
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

### Docker Compose Default Models

The `compose.yaml` overrides the default models with faster, cost-effective options:

| Role | Provider | Model |
|------|----------|-------|
| Council Member 1 | OpenAI | `gpt-5-mini` |
| Council Member 2 | Anthropic | `claude-haiku-4-5-20251001` |
| Council Member 3 | Google | `gemini-2.5-flash` |
| Chairman | Google | `gemini-2.5-pro` |
| SQL Generation | Anthropic | `claude-sonnet-4-5-20250929` |

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
| `Stage3SynthesizeFinalAsync()` | Chairman synthesizes final answer from all data |
| `GenerateConversationTitleAsync()` | Auto-generates 3-5 word conversation title |

Key behaviors:
- **Tool detection**: Keyword-based matching (sales, revenue, product, etc.) determines if `CompanyDataTool` should be offered to models
- **Chart propagation**: Charts from the top-ranked Stage 1 response are propagated to Stage 3 for display
- **Fallback**: If the chairman returns empty, falls back to the first Stage 1 response

### SemanticKernelLlmService

**File:** `SamurAICouncil.Core/Services/SemanticKernelLlmService.cs`

Multi-provider LLM integration implementing `ILlmService`:

| Provider | Implementation |
|----------|---------------|
| **OpenAI** | Semantic Kernel `IChatCompletionService` with `FunctionChoiceBehavior.Auto()` for tool calling |
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
4. Optionally generates chart recommendations based on query results

### Supporting Services

| Service | File | Purpose |
|---------|------|---------|
| `ResilientLlmService` | `Core/Services/` | Polly-wrapped decorator for LLM calls |
| `CompanyDataTool` | `Core/Services/` | `ILlmTool` implementation - bridges natural language to SQL |
| `CompanyDataPlugin` | `Core/Services/` | Semantic Kernel plugin wrapper for `CompanyDataTool` |
| `ChartDataTransformer` | `Core/Services/` | Parses and validates LLM chart JSON into `ChartRecommendation` |
| `RankingParser` | `Core/Services/` | Regex-based extraction of rankings from model responses |
| `AggregateRankingCalculator` | `Core/Services/` | Computes average rank positions across all peer reviews |
| `AnonymizationHelper` | `Core/Services/` | Assigns letter labels (A, B, C) to responses for blind peer review |

---

## Blazor Components

### Component Hierarchy

```
App.razor (HTML shell, CSS/JS imports, Prism.js CDN)
└── Routes.razor (Router + AppErrorBoundary, auto-recovers on navigation)
    └── MainLayout.razor (MudLayout: AppBar + Drawer + MainContent)
        ├── Sidebar.razor (inside MudDrawer - conversation list & navigation)
        └── [page content via @Body]
            └── Chat.razor (/, /chat, /chat/{conversationId:guid})
                └── [per message]
                    ├── MarkdownRenderer.razor (user messages)
                    └── AssistantMessagePanel.razor
                        ├── LoadingSpinner.razor (during processing)
                        └── MudTabs (lazy rendering per tab)
                            ├── Tab 0: Stage3Panel.razor (Final Answer)
                            │   ├── ToolUsagePanel.razor (if tools used)
                            │   ├── MarkdownRenderer.razor
                            │   └── ChartDisplay.razor (if chart present)
                            ├── Tab 1: Stage1Panel.razor (Responses)
                            │   ├── ToolUsagePanel.razor (per model)
                            │   └── MarkdownRenderer.razor (per model)
                            └── Tab 2: Stage2Panel.razor (Rankings)
                                └── MarkdownRenderer.razor (de-anonymized)
```

### Page Components

| Component | Route(s) | Purpose |
|-----------|----------|---------|
| `Chat.razor` | `/`, `/chat`, `/chat/{id:guid}` | Main chat interface - message display, input, council orchestration |
| `Error.razor` | `/Error` | User-friendly error page |
| `NotFound.razor` | `/not-found` | 404 page |

### Layout Components

| Component | Purpose |
|-----------|---------|
| `MainLayout.razor` | Application shell - sidebar + content area |
| `Sidebar.razor` | Conversation list, new conversation button, navigation |
| `ReconnectModal.razor` | SignalR reconnection overlay with retry logic |

### Shared Components

| Component | Purpose |
|-----------|---------|
| `AssistantMessagePanel` | Orchestrates display of Stage 1/2/3 panels for an assistant message |
| `Stage1Panel` | Tabbed view showing each model's independent response |
| `Stage2Panel` | Displays peer rankings with de-anonymization (reveals which model is A, B, C) |
| `Stage3Panel` | Shows chairman's synthesized final answer + chart visualization |
| `ChartDisplay` | Renders MudBlazor charts (Bar, Line, Pie, Donut) from `ChartRecommendation` |
| `ToolUsagePanel` | Displays tool invocations - SQL queries, parameters, results |
| `MarkdownRenderer` | Server-side markdown rendering via Markdig |
| `TabNavigation` | Reusable tab control used across stage panels |
| `LoadingSpinner` | Animated spinner shown during council deliberation |
| `Logo` | Application logo with configurable sizing |
| `AppErrorBoundary` | Global error boundary - catches component exceptions, shows user-friendly message |

### Web Services

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `ConversationService` | Scoped | CRUD operations for conversations with database persistence |
| `ConversationState` | Scoped | Observable state container for current conversation + conversation list |
| `ThemeService` | Scoped | Dark/light theme toggle with MudBlazor integration |
| `PdfExportService` | Scoped | Exports council responses to PDF (QuestPDF) and Excel (ClosedXML) |

### Message Flow & Progressive Loading

The chat interface provides real-time progress feedback through a `LoadingState` model with per-stage booleans:

```
User types message in Chat.razor
    |
    v
ConversationState.IsProcessing = true
    |
    v
AssistantMessage created with Loading.Stage1 = true
  --> UI shows: animated spinner + 3-step progress (Responses -> Review -> Synthesis)
    |
    v
Stage1CollectResponsesAsync() completes
  --> Loading.Stage2 = true, Stage1 data populated
  --> UI: step 1 checkmark, step 2 pulsing, Stage1Panel tab enabled
    |
    v
Stage2CollectRankingsAsync() completes + AggregateRankings calculated
  --> Loading.Stage3 = true, Stage2 data populated
  --> UI: step 2 checkmark, step 3 pulsing, Stage2Panel tab enabled
    |
    v
Stage3SynthesizeFinalAsync() completes + chart propagated from top-ranked Stage1
  --> All loading cleared, Stage3 data populated
  --> UI: all tabs enabled, "Final Answer" tab active by default
    |
    v
ConversationService.SaveAssistantMessageAsync()
GenerateTitleInBackgroundAsync() (fire-and-forget for first message)
ConversationState.IsProcessing = false
```

**State management**: `ConversationState` uses an event-driven pub/sub pattern. Both `Chat.razor` and `Sidebar.razor` subscribe to `OnChange` and independently call `InvokeAsync(StateHasChanged)`. All components implement `IDisposable` with a `_disposed` guard flag to prevent ghost updates from disconnected circuits.

---

## LLM Integration

### Multi-Provider Architecture

SamurAI Council uses a dual-SDK approach:

**OpenAI & Google** - via Microsoft Semantic Kernel:
- Kernel created per-call with appropriate connector
- `FunctionChoiceBehavior.Auto()` enables automatic tool invocation
- Tools registered as kernel plugins via `KernelFunctionFactory.CreateFromMethod()`
- Semantic Kernel handles multi-turn tool conversations automatically

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
- `ToolUsages` - List of `ToolUsage` records (tool name, input, output, optional chart)
- `UsedTools` - Boolean convenience property

---

## Tool Calling & Text-to-SQL

### How It Works

1. **Detection**: `CouncilService` checks if the user query contains company-data keywords (sales, revenue, product, customer, etc.)
2. **Tool Provision**: If detected, `CompanyDataTool` is provided to models as an available tool
3. **SQL Generation**: When a model invokes the tool, `CompanyDataService` uses an LLM to generate SQL from the natural language query against the embedded `ContosoRetailDW_Schema.md`
4. **Execution**: Generated SQL is executed against SQL Server with configurable timeout and row limits
5. **Chart Generation**: If the query returns tabular data, an LLM generates a `ChartRecommendation`
6. **Response**: Results (data + optional chart) are returned to the calling model as tool output

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
CompanyDataService.GenerateChartRecommendationAsync()
    |-- LLM analyzes results and recommends chart type
    |-- Returns ChartRecommendation (type, labels, series, axes)
    |
    v
Tool returns JSON: { success, sql, rowCount, data, chart }
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

SamurAI Council includes LLM-driven chart generation for data query results.

### Supported Chart Types

| Type | Use Case | Example Query |
|------|----------|---------------|
| **Bar** | Category comparisons | "Show top 5 products by revenue in 2008" |
| **Line** | Trends over time | "Show monthly sales trends" |
| **Pie** | Proportions of a whole | "What percentage of sales come from each channel?" |
| **Donut** | Proportions (variant) | "Show sales distribution by region" |
| **None** | Data unsuitable for charts | Single values, text-only results |

### Chart Data Model

```csharp
public record ChartRecommendation
{
    ChartType Type { get; init; }          // Bar, Line, Pie, Donut, None
    string Title { get; init; }            // Chart title
    string[] Labels { get; init; }         // X-axis labels / pie segments
    ChartSeriesData[] Series { get; init; } // Data series (name + values)
    string? XAxisLabel { get; init; }      // Optional axis label
    string? YAxisLabel { get; init; }      // Optional axis label
}
```

### Rendering

Charts are rendered client-side using MudBlazor's `MudChart` component in `ChartDisplay.razor`. The chart from the best-ranked Stage 1 response is automatically propagated to Stage 3 (the final answer panel).

---

## Export Features

Council responses can be exported in two formats:

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

Exports are triggered from the UI and downloaded via JS interop (`fileDownload.js`).

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
// In Program.cs
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

**File:** `SamurAICouncil.Core/Resilience/LlmResiliencePolicies.cs`

`ResilientLlmService` wraps `ILlmService` with a composed Polly policy stack:

| Policy | Configuration | Purpose |
|--------|--------------|---------|
| **Timeout** | 60 seconds per call | Prevent hanging LLM requests |
| **Circuit Breaker** | Opens after 5 failures, 30s break | Prevent cascade failures to LLM providers |
| **Retry** | 3 retries, exponential backoff (2s, 4s, 8s) | Handle transient HTTP failures |

Execution order (outermost first): Timeout -> Circuit Breaker -> Retry -> Actual LLM call

Handles `HttpRequestException`, non-cancellation `TaskCanceledException`, and `TimeoutRejectedException`. Returns null/empty on final failure rather than propagating exceptions.

### Application-Level Error Handling

- `AppErrorBoundary.razor` catches unhandled component exceptions
- User-friendly error messages with automatic recovery on navigation
- Graceful fallback when chairman model returns empty (uses first Stage 1 response)
- `ReconnectModal` handles SignalR disconnections in Blazor Server

---

## Testing

### Test Suite Overview

| Project | Tests | Focus | Frameworks |
|---------|-------|-------|------------|
| `SamurAICouncil.Core.Tests` | 232 | Business logic, services, ranking, anonymization | MSTest, Moq |
| `SamurAICouncil.Data.Tests` | 18 | Database integration, repository CRUD | MSTest, TestContainers |
| `SamurAICouncil.Web.Tests` | 110 | Component rendering, UI interactions | MSTest, bUnit, Moq |
| **Total** | **360** | | |

### Coverage Targets

| Project | Target |
|---------|--------|
| Core.Tests | 90% (business logic) |
| Data.Tests | Integration-focused |
| Web.Tests | 50% (critical components) |

### Running Tests

```bash
# Run all tests (360 total)
dotnet test

# Run specific test project
dotnet test SamurAICouncil.Core.Tests
dotnet test SamurAICouncil.Data.Tests      # Requires Docker (TestContainers)
dotnet test SamurAICouncil.Web.Tests       # Some tests require Docker

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~CouncilServiceTests"
```

### Testing Patterns

- **Unit tests** (Core.Tests): Mock `ILlmService` via Moq, test deliberation logic, ranking calculations, anonymization, chart validation (51 tests in `ChartDataTransformerTests` alone)
- **Integration tests** (Core.Tests): Opt-in LLM integration tests using real API keys (loaded from `env_vars` file or environment), tagged with `[TestCategory("Integration")]`, skip gracefully via `Assert.Inconclusive` when keys are absent
- **Integration tests** (Data.Tests): TestContainers spins up real `postgres:16-alpine`, tests repository CRUD + JSONB round-trip, cascade deletes; runs sequentially (`[assembly: DoNotParallelize]`)
- **Component tests** (Web.Tests): bUnit renders Blazor components in isolation with mocked services, `JSRuntimeMode.Loose` for MudBlazor interop (54 tests for `MarkdownRenderer` alone)
- **Integration tests** (Web.Tests): Full `ConversationService` end-to-end with real PostgreSQL via TestContainers

### Key Test Classes

| Class | Tests | Covers |
|-------|-------|--------|
| `MarkdownRendererTests` | 54 | Markdig pipeline: code blocks, typography, lists, tables, advanced elements |
| `ChartDataTransformerTests` | 51 | Chart parsing, validation, truncation, label handling per chart type |
| `ChartRecommendationSerializationTests` | 25 | JSON round-trip for all chart types and model properties |
| `CompanyDataServiceIntegrationTests` | ~40 | E2E Text-to-SQL across all ContosoRetailDW table categories |
| `SemanticKernelLlmServiceIntegrationTests` | 18 | OpenAI/Anthropic/Google queries + tool invocation |
| `CouncilServiceTests` | 16 | Stage 1/2/3 orchestration, fallbacks, title generation |
| `ConversationServiceIntegrationTests` | 13 | Full CRUD + multi-turn conversation flows |
| `RankingParserTests` | 11 | All 4 parsing fallback strategies |
| `MessageRepositoryTests` | 10 | JSONB serialization, message ordering, conversation isolation |
| `ConversationRepositoryTests` | 8 | CRUD, cascade delete, listing with message counts |

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
| **Semantic Kernel** for LLM integration | Native .NET support for OpenAI + Google, consistent API, built-in function calling |
| **Anthropic SDK** (direct, not via SK) | Full control over Anthropic's native tool use protocol with multi-turn support |
| **PostgreSQL + Dapper** | JSONB columns for flexible stage data storage; Dapper provides full SQL control |
| **FluentMigrator** | Code-first schema migrations that run on startup |
| **Polly** for resilience | Industry-standard retry + circuit breaker for both LLM APIs and database |
| **Blazor Server** (not WASM) | Server-side rendering keeps API keys secure; SignalR provides real-time UI updates |
| **MudBlazor** for UI | Material Design components with built-in chart rendering |
| **Scoped services** for UI state | `ConversationState` and `ConversationService` are scoped per Blazor circuit |

---

*SamurAI Council -- Where AI Models Deliberate, So You Can Decide with Confidence*
