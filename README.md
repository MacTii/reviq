# Reviq — AI Code Review

Reviq reviews your code with AI. Paste a snippet, drop in a batch of files, point it at a local git repository, or wire it up to your GitHub/GitLab pull requests so every PR gets reviewed automatically. Runs fully offline with a local model, or use your own API key for Claude, OpenAI, Groq, or OpenRouter — your choice.

## Screenshots

| Code analysis (paste/upload files) | Local AI model management |
|---|---|
| ![Code analysis](docs/screenshots/01-analyze-code.png) | ![Local AI](docs/screenshots/02-local-ai-models.png) |

| Git repository review | Analysis history |
|---|---|
| ![Repository](docs/screenshots/03-repo-tab.jpg) | ![History](docs/screenshots/04-history.jpg) |

## What you can do with it

- **Review a code snippet** — paste code or drag in files, pick a language, hit analyze. You get a score out of 100, issues grouped by severity (Critical / Warning / Info), plain-language explanations, and a before/after fix suggestion where relevant.
- **Review a local git repository** — point Reviq at a folder on your machine and choose what to review: the last commit, everything since your last push, uncommitted changes, or the whole repo. No need to manually copy files around.
- **Get PRs reviewed automatically** — connect a GitHub or GitLab webhook and every opened or updated pull request gets an AI review comment plus a pass/fail commit status, with no manual step on your end.
- **Run everything offline** — download a code model straight from Hugging Face inside the app and review code without your source ever leaving your machine.
- **Switch AI providers anytime** — flip between a local model and a cloud provider from the UI, no restart needed. Reviq shows you which providers are actually reachable right now.
- **Keep a history of past reviews** — revisit anything you've analyzed in the current session and open it back up.
- **Export a report** — save any analysis as a standalone HTML file, or print it to PDF, to share with your team.
- **Use it in Polish or English** — full UI translation with a one-click language switch.

## Getting started

**Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/download), and `git` available on your PATH if you want to review local repositories.

```bash
git clone <repo-url>
cd Reviq
dotnet build
cd Reviq.API
dotnet run
```

Open `http://localhost:5000` in your browser — that's the whole app, no separate frontend server needed.

### Setting up an AI provider

You need at least one working provider before you can run a review:

- **Easiest: a local model.** Open the "Local AI" panel in the app, pick one of the recommended models (or search Hugging Face for any GGUF model), and download it. Nothing to configure — Reviq runs it in-process.
- **Ollama / LM Studio.** Install and start either one on your machine, then just select it as the provider in the app. By default Reviq looks for Ollama at `localhost:11434` and LM Studio at `localhost:1234`.
- **A cloud provider (Claude, OpenAI, Groq, or OpenRouter).** Add your API key to `Reviq.API/appsettings.json` (or, better, to `appsettings.Development.json` / [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) so it never ends up committed) under the matching `AI:<Provider>:ApiKey` entry, then restart the app. It'll show up as configured in the provider switcher.

### Setting up automatic PR reviews

1. Add your `Git:GitHub:Token` / `Git:GitLab:Token` to `appsettings.json` — this is what Reviq uses to post comments and commit statuses back to your repo.
2. In your GitHub/GitLab repository settings, add a webhook pointing at `https://<your-server>/api/webhook/github` (or `/gitlab`), triggered on pull request / merge request events.
3. Open a PR — Reviq picks it up, reviews the changed files, and posts the result as a comment with a commit status.

This needs Reviq to be reachable from GitHub/GitLab, so for real usage you'll want to deploy it somewhere public (or tunnel it, e.g. with ngrok, for testing).

## More for developers

Architecture, project layout, tech stack, and the current list of known limitations live in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
