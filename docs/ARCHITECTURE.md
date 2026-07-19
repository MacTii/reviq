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
- **Reviq.Infrastructure** — port implementations: AI providers, GitHub/GitLab integrations, local repo operations, LocalAI/Hugging Face engine, repository (in-memory).
- **Reviq.API** — controllers as a thin layer translating HTTP → Mediator, middleware, DI configuration, static frontend under `wwwroot`.

## Project structure

```
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
├── Configuration/         # options classes (Options pattern)
├── Git/                   # GitService (CLI), GitHub/GitLab providers, PR file fetcher
├── LocalAI/
│   ├── HuggingFace/       # model search/download client
│   ├── Models/
│   └── Services/          # downloaded .gguf model management
└── Persistence/           # ReviewRepository (in-memory)

Reviq.API/
├── Controllers/           # Code, Review, Git, History, AI, LocalAI, Webhook
├── Middleware/             # ErrorHandlingMiddleware
├── Requests/ / Responses/  # HTTP contracts
└── wwwroot/                # frontend (ES modules, no bundler)
    ├── css/
    ├── js/                  # api.js, i18n.js, providers.js, review.js, results.js,
    │                        # localai.js, history.js, export.js, app.js...
    └── locales/             # pl.json, en.json
```

## Tech stack

- **.NET 10**, latest C# language version, `Nullable` + `ImplicitUsings` enabled across all projects
- **Mediator.SourceGenerator** — CQRS with no runtime overhead (compiled mediator, a free alternative to commercially-licensed MediatR 13+)
- **FluentValidation** — command/query validation as a pipeline behavior
- **LLamaSharp** (CPU/CUDA12/Vulkan) — local `.gguf` models
- **Swashbuckle/Swagger** — API documentation (Development only)
- Frontend: **vanilla JS (ES modules)**, no framework, no build tooling

## Recently fixed

- **Scoped `IMediator` used after the request scope was disposed** — webhook handling (`WebhookController`) processed requests fire-and-forget via `Task.Run`, using the scoped `IMediator` injected into the controller. Once the HTTP response was sent, the request scope (and the mediator's dependencies with it) was disposed, risking a background `ObjectDisposedException` and silently swallowed failures. Fixed by creating a dedicated scope via `IServiceScopeFactory` for the background work, plus `try/catch` with logging.
- **Swagger UI exposed in production** — `app.UseSwagger()`/`UseSwaggerUI()` ran unconditionally. Now gated behind `app.Environment.IsDevelopment()`.
- **Hardcoded listen address** — `app.Run("http://localhost:5000")` ignored `ASPNETCORE_URLS`/`--urls`/environment variables. Replaced with `app.Run()` and a configurable `Urls` default in `appsettings.json` — still works out of the box on the same port, but can now be overridden without recompiling (e.g. in a container).

## Known limitations & ideas for improvement

What's genuinely worth fixing/adding to take this from "solid MVP" to "production-ready":

- **In-memory persistence only** — `ReviewRepository` is a `ConcurrentDictionary`, so history is lost on every app restart. The most important gap — worth backing the existing `IReviewRepository` interface with a real database (EF Core + SQLite to start, PostgreSQL for production), so the swap needs no changes in Application/Domain.
- **No tests** — there isn't a single test project in the solution. Domain and Application (parsers, builders, handlers) are well suited to pure unit tests without HTTP/DB mocks; a good starting point would be `AIResponseParser`, `ReviewSummaryBuilder`, `WebhookReviewParser`.
- **CORS `AllowAnyOrigin/Header/Method`** — hardcoded wide open in `Program.cs`; fine for dev, but needs narrowing to specific origins for production.
- **No authorization/authentication** — every endpoint (including running a review or managing providers/models) is publicly accessible. Exposing this beyond `localhost` needs at least minimal auth (API key / JWT).
- **Webhooks without signature verification** — `WebhookController` doesn't check `X-Hub-Signature-256` (GitHub) or a webhook token (GitLab), so anyone who knows the URL can trigger a fake review. Worth addressing before exposing this to the internet.
- **No rate limiting / queue for webhooks** — `HandleWebhookCommand` runs fire-and-forget with no concurrency limit; under heavier PR traffic this should go through a queue (e.g. `Channel<T>` or Hangfire).
- **Frontend has no tests and no TypeScript** — the ES modules are already split with clear boundaries, but it's still untyped JS; migrating to TS (or at least JSDoc + `checkJs`) would catch things like typos in object keys before they reach production.
- **appsettings ships with empty API keys in the repo** — works fine, but it's worth adding a `.gitignore` entry for `appsettings.*.Local.json` / documenting user-secrets, so nobody accidentally commits a real key.
- **No health checks / observability** — no `/health` endpoint or metrics; useful for real hosting (e.g. to verify the configured AI provider is actually responding).
