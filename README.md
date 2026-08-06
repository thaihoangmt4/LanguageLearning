# Language Learning Platform

See [Sprint 6 Exercise Engine v2](docs/sprint-6-exercise-engine.md) for the backend model, learner flow, seed data, safety limits, and deferred work.

## EF migrations
dotnet ef migrations add MigrationName --startup-project LanguageLearning.WebApi/ --project LanguageLearning.Common --context ApplicationDbContext --output-dir Migrations

dotnet ef database update --startup-project LanguageLearning.WebApi/ --project LanguageLearning.Common --context ApplicationDbContext
