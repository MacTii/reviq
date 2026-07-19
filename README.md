# Reviq — AI Code Review

Reviq to narzędzie do automatycznego code review wspieranego przez AI. Pozwala przeanalizować pojedynczy fragment kodu, cały zbiór plików, zmiany w lokalnym repozytorium git albo automatycznie zareagować na webhook PR-a z GitHuba/GitLaba — wszystko z wyborem dowolnego providera AI (lokalnego lub chmurowego).

Backend: ASP.NET Core Web API (.NET 10) w Clean Architecture + CQRS. Frontend: statyczny, zależny wyłącznie od przeglądarki (vanilla JS, moduły ES, bez bundlera).

## Zrzuty ekranu

| Analiza kodu (wklej/wgraj pliki) | Zarządzanie modelami Local AI |
|---|---|
| ![Analiza kodu](docs/screenshots/01-analyze-code.png) | ![Local AI](docs/screenshots/02-local-ai-models.png) |

| Review repozytorium git | Historia analiz |
|---|---|
| ![Repozytorium](docs/screenshots/03-repo-tab.jpg) | ![Historia](docs/screenshots/04-history.jpg) |

## Funkcjonalność

- **Analiza wklejonego kodu** — wklej fragment kodu (lub kilka plików naraz) i dostań ocenę (0–100), listę problemów z podziałem na Critical/Warning/Info, sugestie i diff „przed/po" dla wybranych issues.
- **Review lokalnego repozytorium git** — wskaż ścieżkę do repo i wybierz zakres zmian do analizy: ostatni commit, zmiany od ostatniego pusha, niezacommitowane zmiany albo wszystkie pliki. Reviq sam wykonuje `git diff`/`git ls-files` (przez CLI `git`) i wysyła zmienione pliki do AI.
- **Automatyczny review PR-ów (webhook)** — endpointy `POST /api/webhook/github` i `POST /api/webhook/gitlab` odbierają webhooki `pull_request`/`merge_request`, pobierają zmienione pliki z PR-a, uruchamiają analizę AI i odsyłają wynik jako komentarz na PR + status commita.
- **Lokalne modele AI (offline)** — wbudowany silnik na bazie **LLamaSharp**/llama.cpp (CPU, CUDA 12, Vulkan) do uruchamiania modeli `.gguf` bez wysyłania kodu na zewnątrz. Wyszukiwanie i pobieranie modeli bezpośrednio z Hugging Face, zarządzanie pobranymi plikami z poziomu UI.
- **Wielu providerów AI do wyboru w locie** — przełączanie providera i modelu bez restartu aplikacji, wraz z podglądem statusu dostępności każdego z nich.
- **Historia analiz** — lista wcześniejszych review'ów z podglądem szczegółów (przechowywana w pamięci procesu — patrz [Znane ograniczenia](#znane-ograniczenia-i-pomysły-na-rozwój)).
- **Eksport raportu** — wynik analizy można wyeksportować jako samodzielny plik HTML albo wydrukować/zapisać jako PDF.
- **PL/EN** — pełny interfejs z przełącznikiem języka (i18n oparte o pliki JSON, bez zewnętrznych bibliotek).

### Wspierani providerzy AI

| Provider | Typ | Wymaga |
|---|---|---|
| **LocalAI** | lokalny (LLamaSharp/llama.cpp, w procesie) | pliku `.gguf` w folderze modeli |
| **Ollama** | lokalny (serwer HTTP) | uruchomionego `ollama serve` (domyślnie `localhost:11434`) |
| **LM Studio** | lokalny (serwer HTTP, OpenAI-compatible) | uruchomionego LM Studio (domyślnie `localhost:1234`) |
| **Claude** | chmurowy | klucza API Anthropic |
| **OpenAI** | chmurowy | klucza API OpenAI |
| **Groq** | chmurowy | klucza API Groq |
| **OpenRouter** | chmurowy | klucza API OpenRouter |

## Architektura

Clean Architecture w 4 projektach, zależności skierowane do wewnątrz, CQRS przez własny, darmowy generator mediatora (`Mediator.SourceGenerator` — bez płatnej licencji MediatR v13+):

```
Presentation/API  ─┐
Infrastructure    ─┼──►  Application  ──►  Domain
```

- **Reviq.Domain** — encje (`ReviewResult`, `FileReview`, `ReviewIssue`, `WebhookPayload`...), enumy, value objecty, interfejsy portów (`IGitProvider`, `IGitHostProvider`, `IReviewRepository`). Zero zależności na zewnątrz.
- **Reviq.Application** — logika przypadków użycia jako Commands/Queries (feature-folder: `Features/Reviews`, `Features/Webhook`, `Features/Git`, `Features/AI`), walidacja przez FluentValidation, DTO, mapowanie.
- **Reviq.Infrastructure** — implementacje portów: providerzy AI, integracje z GitHub/GitLab, operacje na lokalnym repo, silnik LocalAI/Hugging Face, repozytorium (in-memory).
- **Reviq.API** — kontrolery jako cienka warstwa tłumacząca HTTP → Mediator, middleware, konfiguracja DI, statyczny frontend w `wwwroot`.

### Struktura katalogów

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
│   ├── AI/Queries/            # status providera
│   ├── Git/Queries/           # info o repo
│   ├── Reviews/Commands+Queries/  # review snippetu/batcha/repo, historia
│   └── Webhook/Commands/      # obsługa PR webhooków
├── Interfaces/            # IAIProvider(Factory), IGitHostProviderFactory, ILocalAIService...
└── Requests/

Reviq.Infrastructure/
├── AI/
│   ├── Providers/         # LocalAI, Ollama, Claude, OpenAI, Groq, OpenRouter, LMStudio
│   └── Parsing/           # budowa promptu
├── Configuration/         # klasy opcji (Options pattern)
├── Git/                   # GitService (CLI), GitHub/GitLab providerzy, fetcher plików PR
├── LocalAI/
│   ├── HuggingFace/       # klient wyszukiwania/pobierania modeli
│   ├── Models/
│   └── Services/          # zarządzanie pobranymi modelami .gguf
└── Persistence/           # ReviewRepository (in-memory)

Reviq.API/
├── Controllers/           # Code, Review, Git, History, AI, LocalAI, Webhook
├── Middleware/             # ErrorHandlingMiddleware
├── Requests/ / Responses/  # kontrakty HTTP
└── wwwroot/                # frontend (moduły ES, bez bundlera)
    ├── css/
    ├── js/                  # api.js, i18n.js, providers.js, review.js, results.js,
    │                        # localai.js, history.js, export.js, app.js...
    └── locales/             # pl.json, en.json
```

## Uruchomienie

**Wymagania:** .NET 10 SDK, zainstalowany `git` w PATH (do review'u lokalnych repozytoriów). Opcjonalnie: [Ollama](https://ollama.com)/[LM Studio](https://lmstudio.ai) uruchomione lokalnie, albo klucz API jednego z providerów chmurowych.

```bash
git clone <repo-url>
cd Reviq
dotnet build
cd Reviq.API
dotnet run
```

Aplikacja wystartuje pod `http://localhost:5000` — pod tym samym adresem dostępny jest zarówno frontend (`/`), jak i Swagger (`/swagger`).

### Konfiguracja (`Reviq.API/appsettings.json`)

| Sekcja | Klucz | Opis |
|---|---|---|
| `Ollama` | `BaseUrl`, `DefaultModel` | adres lokalnego serwera Ollama |
| `LocalAI` | `ModelsDir` | folder na pobrane pliki `.gguf` |
| `AI:Claude` / `OpenAI` / `Groq` / `OpenRouter` / `LMStudio` | `ApiKey`, `BaseUrl`, `DefaultModel` | dane dostępowe do providerów chmurowych — puste domyślnie, provider jest wtedy oznaczony jako nieskonfigurowany |
| `Git:GitHub` / `GitLab` | `Token` | token używany przy obsłudze webhooków (komentarz na PR, status commita) |

Wszystkie klucze API są puste w repozytorium — nigdy nie commituj tam realnych sekretów; do lokalnej pracy użyj `appsettings.Development.json` albo [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets).

### Naprawione

- **Scoped `IMediator` używany po zamknięciu scope'a requestu** — obsługa webhooków (`WebhookController`) odpalała przetwarzanie fire-and-forget przez `Task.Run`, korzystając ze scoped `IMediator` wstrzykniętego do kontrolera. Po zwróceniu odpowiedzi HTTP scope requestu (a wraz z nim zależności mediatora) był dysponowany, co groziło `ObjectDisposedException` w tle i cichym gubieniem błędów. Naprawione przez `IServiceScopeFactory.CreateScope()` wewnątrz przetwarzania w tle + `try/catch` z logowaniem.
- **Swagger UI wystawiony też na produkcji** — `app.UseSwagger()`/`UseSwaggerUI()` działały bezwarunkowo. Zawężone do `app.Environment.IsDevelopment()`.
- **Zahardkodowany adres nasłuchu** — `app.Run("http://localhost:5000")` ignorował `ASPNETCORE_URLS`/`--urls`/zmienne środowiskowe. Zamienione na `app.Run()` z domyślnym portem 5000 ustawionym przez konfigurowalny klucz `Urls` w `appsettings.json` — nadal działa "out of the box" na tym samym porcie, ale teraz można go nadpisać bez rekompilacji (np. w kontenerze).

## Znane ograniczenia i pomysły na rozwój

To, co realnie warto poprawić/dodać, żeby projekt przeszedł z "solidne MVP" do "gotowe do produkcji":

- **Persystencja tylko w pamięci** — `ReviewRepository` to `ConcurrentDictionary`, więc historia znika po restarcie aplikacji. Najważniejsza brakująca rzecz — warto dodać prawdziwą bazę (EF Core + SQLite do startu, PostgreSQL docelowo) za istniejącym interfejsem `IReviewRepository`, więc wymiana nie wymaga zmian w Application/Domain.
- **Brak testów** — w solucji nie ma ani jednego projektu testowego. Domain i Application (parsery, buildery, handlery) nadają się do czystych testów jednostkowych bez mocków HTTP/DB; warto zacząć od `AIResponseParser`, `ReviewSummaryBuilder`, `WebhookReviewParser`.
- **CORS `AllowAnyOrigin/Header/Method`** — otwarte na sztywno w `Program.cs`, sensowne w dev, ale do produkcji wymaga zawężenia do konkretnych originów.
- **Brak autoryzacji/autentykacji** — każdy endpoint (w tym uruchamianie review'u i zarządzanie providerami/modelami) jest publicznie dostępny. Do wystawienia poza `localhost` potrzebny jest choć minimalny auth (API key / JWT).
- **Webhooki bez weryfikacji podpisu** — `WebhookController` nie sprawdza `X-Hub-Signature-256` (GitHub) ani tokena webhooka (GitLab), więc każdy znający URL może wywołać fałszywy review. Do rozważenia przy realnym wystawieniu na świat.
- **Brak rate-limitingu / kolejki dla webhooków** — `HandleWebhookCommand` jest odpalany fire-and-forget bez ograniczenia równoległości; przy większym ruchu PR-ów warto to przepuścić przez kolejkę (np. `Channel<T>` albo Hangfire). (Wyciek scoped zależności przy tym fire-and-forget i brak logowania błędów zostały już naprawione — patrz niżej.)
- **Frontend bez testów i bez TypeScript** — moduły ES są już podzielone i mają jasne granice, ale całość to nadal "gołe" JS bez statycznego typowania; migracja do TS (lub choć JSDoc + `checkJs`) złapałaby błędy typu literówek w kluczach obiektów przed wysłaniem do produkcji.
- **appsettings z pustymi kluczami API w repo** — działa, ale warto dorzucić `.gitignore` dla `appsettings.*.Local.json` / dokumentację user-secrets, żeby nikt przez pomyłkę nie wrzucił tam prawdziwego klucza.
- **Health checks / obserwowalność** — brak `/health` endpointu i metryk; przydałyby się przy realnym hostingu (np. do sprawdzania, czy skonfigurowany provider AI faktycznie odpowiada).

## Stos technologiczny

- **.NET 10**, C# najnowsza wersja języka, `Nullable` + `ImplicitUsings` włączone we wszystkich projektach
- **Mediator.SourceGenerator** — CQRS bez narzutu runtime (kompilowany mediator, alternatywa dla płatnego MediatR 13+)
- **FluentValidation** — walidacja komend/zapytań jako pipeline behavior
- **LLamaSharp** (CPU/CUDA12/Vulkan) — lokalne modele `.gguf`
- **Swashbuckle/Swagger** — dokumentacja API
- Frontend: **vanilla JS (moduły ES)**, bez frameworka i bez build tooling
