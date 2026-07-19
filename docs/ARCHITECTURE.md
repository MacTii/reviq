# Architecture

Technical reference for contributors. For what the app does and how to run it, see the main [README](../README.md).

## Layers

Clean Architecture across 4 projects, dependencies pointing inward, CQRS via a self-hosted, free mediator source generator (`Mediator.SourceGenerator` — no MediatR v13+ commercial license required):

```
Presentation/API  ─┐
Infrastructure    ─┼──►  Application  ──►  Domain
```

- **Reviq.Domain** — entities (`ReviewResult`, `FileReview`, `ReviewIssue`, `WebhookPayload`...), enums, value objects, port interfaces (`IGitProvider`, `IGitHostProvider`, `IReviewRepository`). No outward dependencies.
- **Reviq.Application** — use-case logic as Commands/Queries (feature folders: `Features/Reviews`, `Features/Webhook`, `Features/Git`, `Features/AI`), validation via FluentValidation, DTOs, mapping.
- **Reviq.Infrastructure** — port implementations: AI providers, GitHub/GitLab integrations, local repo operations, LocalAI/Hugging Face engine, EF Core + SQLite repository.
- **Reviq.API** — controllers as a thin layer translating HTTP → Mediator, middleware, DI configuration, static frontend under `wwwroot`.
- **Reviq.Application.Tests** — xUnit unit tests for the Application layer (parsers, builders — no HTTP/DB involved).

## Project structure

Source projects live under `src/`, test projects under `tests/` — a standard .NET solution layout:

```
src/
  Reviq.Domain/
  ├── Entities/            # ReviewResult, FileReview, ReviewIssue, WebhookPayload, PrFile...
  ├── Enums/                # IssueSeverity, IssueCategory, ProviderName, DiffScope...
  ├── Interfaces/           # IGitProvider, IGitHostProvider, IReviewRepository
  └── ValueObjects/         # ProviderInfo

  Reviq.Application/
  ├── Common/                # AIResponseParser, ReviewSummaryBuilder, ValidationBehavior
  ├── DTOs/
  ├── Features/
  │   ├── AI/Queries/            # provider status
  │   ├── Git/Queries/           # repo info
  │   ├── Reviews/Commands+Queries/  # snippet/batch/repo review, history
  │   └── Webhook/Commands/      # PR webhook handling
  ├── Interfaces/            # IAIProvider(Factory), IGitHostProviderFactory, ILocalAIService...
  └── Requests/

  Reviq.Infrastructure/
  ├── AI/
  │   ├── Providers/         # LocalAI, Ollama, Claude, OpenAI, Groq, OpenRouter, LMStudio
  │   └── Parsing/           # prompt building
  ├── Configuration/         # options classes bound to external integrations (Ollama, Git, HuggingFace...)
  ├── Git/                   # GitService (CLI), GitHub/GitLab providers, PR file fetcher
  ├── LocalAI/
  │   ├── HuggingFace/       # model search/download client
  │   ├── Models/
  │   └── Services/          # downloaded .gguf model management
  └── Persistence/
      ├── Entities/           # EF Core persistence models (kept separate from Domain entities)
      ├── Migrations/         # EF Core migrations (dotnet-ef, installed as a local tool)
      ├── ReviqDbContext.cs
      └── SqliteReviewRepository.cs

  Reviq.API/
  ├── Controllers/           # Code, Review, Git, History, AI, LocalAI, Webhook
  ├── Configuration/         # options classes for hosting-level concerns (CorsOptions, SecurityOptions)
  ├── Middleware/             # ErrorHandlingMiddleware, ApiKeyMiddleware
  ├── Webhooks/               # IWebhookQueue/WebhookQueue (bounded Channel<T>), WebhookProcessingService (BackgroundService)
  ├── Requests/ / Responses/  # HTTP contracts
  └── wwwroot/                # frontend (ES modules, no bundler)
      ├── css/
      ├── js/                  # api.js, i18n.js, providers.js, review.js, results.js,
      │                        # localai.js, history.js, export.js, app.js...
      └── locales/             # pl.json, en.json

tests/
  Reviq.Application.Tests/    # xUnit — Application-layer logic only, no HTTP/DB
```

Config values are read through the strongly-typed **Options pattern** end to end (`GitOptions`, `OllamaOptions`, `CorsOptions`, `SecurityOptions`, ...) rather than raw `IConfiguration["Key:SubKey"]` string indexing — options specific to external integrations live in `Reviq.Infrastructure/Configuration`, options specific to web-hosting concerns (CORS, the optional API key) live in `Reviq.API/Configuration`.

## Tech stack

- **.NET 10**, latest C# language version, `Nullable` + `ImplicitUsings` enabled across all projects
- **Mediator.SourceGenerator** — CQRS with no runtime overhead (compiled mediator, a free alternative to commercially-licensed MediatR 13+)
- **FluentValidation** — command/query validation as a pipeline behavior
- **EF Core + SQLite** — review history persistence, migrations via `dotnet-ef` (installed as a local tool, see `dotnet-tools.json`)
- **LLamaSharp** (CPU/CUDA12/Vulkan) — local `.gguf` models
- **Swashbuckle/Swagger** — API documentation (Development only)
- **xUnit** — Application-layer unit tests
- Frontend: **vanilla JS (ES modules)**, no framework, no build tooling

## Testing

```bash
dotnet test tests/Reviq.Application.Tests
```

Covers the pure Application-layer logic (`AIResponseParser`, `ReviewSummaryBuilder`, `WebhookReviewParser`, `WebhookCommentBuilder`, `PrFileLanguageDetector`) with no HTTP/DB dependencies.

## Security

- **CORS** — restricted to origins listed in `Cors:AllowedOrigins` (defaults to `http://localhost:5000`).
- **Webhook verification** — GitHub payloads are checked against `X-Hub-Signature-256` (HMAC-SHA256, `Git:GitHub:WebhookSecret`); GitLab payloads against `X-Gitlab-Token` (`Git:GitLab:WebhookSecret`). If a secret isn't configured, the corresponding check is skipped (a warning is logged) rather than rejecting everything — set the secret to enforce it.
- **Optional API key** — set `Security:ApiKey` to require an `X-Api-Key` header on `/api/*` (excluding `/api/webhook/*`, which authenticate via the mechanism above). Empty by default, matching how the AI provider keys behave.
- **Health check** — `GET /health` for liveness monitoring.

## Known limitations & ideas for improvement

What's still genuinely worth adding:

- **API key is all-or-nothing, no user accounts** — fine for a single self-hosted instance behind a reverse proxy, but there's no per-user auth, roles, or session model. A real multi-user deployment would need OAuth/JWT with actual identities.
- **Test coverage is Application-layer only** — Domain, Infrastructure (EF repository, AI providers, git integrations), and API controllers have no automated tests yet.
- **Frontend has no tests and no TypeScript** — the ES modules are already split with clear boundaries, but it's still untyped JS; migrating to TS (or at least JSDoc + `checkJs`) would catch things like typos in object keys before they reach production.
- **SQLite, not built for concurrent multi-instance deployments** — fine for a single-process self-hosted setup; scaling out to multiple app instances would need PostgreSQL or similar.
- **No metrics/tracing beyond the basic `/health` check** — no request metrics, no distributed tracing; would matter for a real production deployment with multiple moving parts.
