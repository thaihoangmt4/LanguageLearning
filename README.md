# Language Learning Platform

See [Sprint 6 Exercise Engine v2](docs/sprint-6-exercise-engine.md) for the backend model, learner flow, seed data, safety limits, and deferred work.

See [Backend production deployment](docs/production-deployment.md) for the GitHub Actions, GHCR, and EC2 deployment runbook.

## EF migrations
dotnet ef migrations add MigrationName --startup-project LanguageLearning.WebApi/ --project LanguageLearning.Common --context ApplicationDbContext --output-dir Migrations

dotnet ef database update --startup-project LanguageLearning.WebApi/ --project LanguageLearning.Common --context ApplicationDbContext

## Sprint 8 DeepSeek configuration

Exercise generation uses DeepSeek's OpenAI-compatible Chat Completions API. A key is required at application startup and must not be committed.

From `backend/LanguageLearning`, configure local development with:

```powershell
dotnet user-secrets set "DeepSeek:ApiKey" "<your-api-key>" --project LanguageLearning.WebApi
```

For Docker or deployment, set:

```text
DeepSeek__ApiKey=<secret>
```

The default model is `deepseek-v4-flash`. Change it without recompiling through `DeepSeek__Model`, for example `DeepSeek__Model=deepseek-v4-pro`. Timeout, retry attempts, and output size can be overridden with `DeepSeek__TimeoutSeconds`, `DeepSeek__MaxRetryAttempts`, and `DeepSeek__MaxOutputTokens`.

DeepSeek usage is an externally billed service. Generation runs automatically on the configured Sprint 8 schedule after the application starts.

In the Development environment, authenticated developers can trigger the same command with `POST /api/test/generate-exercises`. Generated exercises that are not referenced by lesson attempts can be removed with `POST /api/test/reset-generated-exercises`. These controllers are not registered outside Development.
