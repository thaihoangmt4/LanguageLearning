# Language Learning Platform

See [Sprint 6 Exercise Engine v2](docs/sprint-6-exercise-engine.md) for the backend model, learner flow, seed data, safety limits, and deferred work.

See [Backend production deployment](docs/production-deployment.md) for the GitHub Actions, GHCR, and EC2 deployment runbook.

## EF migrations
dotnet ef migrations add MigrationName --startup-project LanguageLearning.WebApi/ --project LanguageLearning.Common --context ApplicationDbContext --output-dir Migrations

dotnet ef database update --startup-project LanguageLearning.WebApi/ --project LanguageLearning.Common --context ApplicationDbContext

## AI Provider Configuration

Exercise generation uses a provider-neutral `IChatClient` boundary over the OpenAI-compatible Chat Completions protocol. The provider is selected operationally through configuration; changing between compatible providers does not require application code changes or a rebuild.

From `backend/LanguageLearning`, configure local development with:

```powershell
dotnet user-secrets set "Ai:ApiKey" "<your-api-key>" --project LanguageLearning.WebApi
```

Docker and production deployment use:

```text
AI_API_KEY=<secret>
AI_BASE_URL=https://generativelanguage.googleapis.com/v1beta/openai/
AI_MODEL=gemini-2.5-flash-lite
```

Gemini 2.5 Flash-Lite is the current low-cost operational default, not an architectural dependency. For example, OpenAI can be selected with `AI_BASE_URL=https://api.openai.com/v1/` and `AI_MODEL=gpt-5-nano`. OpenRouter and other OpenAI-compatible providers use the same three variables.

`AI_API_KEY` must remain in environment configuration or user secrets and must never be committed or stored in Admin settings. Missing or invalid provider credentials do not prevent the API from starting; scheduled generation records a failure while existing exercises remain available. Timeout, retry attempts, and output size retain their safe application defaults and can be overridden through `Ai__TimeoutSeconds`, `Ai__MaxRetryAttempts`, and `Ai__MaxOutputTokens` when needed.

AI usage is externally billed. Generation runs automatically on the configured schedule after the application starts.

In the Development environment, authenticated developers can trigger the same command with `POST /api/test/generate-exercises`. Generated exercises that are not referenced by lesson attempts can be removed with `POST /api/test/reset-generated-exercises`. These controllers are not registered outside Development.
