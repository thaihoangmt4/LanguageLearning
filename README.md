##EF migrations - SystemConfig
dotnet ef migrations add MigrationName --startup-project LanguageLearning.WebApi/ --project LanguageLearning.Common --context ApplicationDbContext --output-dir Migrations

dotnet ef database update --startup-project LanguageLearning.WebApi/ --project LanguageLearning.Common --context ApplicationDbContext