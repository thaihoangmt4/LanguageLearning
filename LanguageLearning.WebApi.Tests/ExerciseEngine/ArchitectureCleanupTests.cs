using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseEngine;

public sealed class ArchitectureCleanupTests
{
    [Fact]
    public void EfModel_ContainsOnlyExerciseLearnerContentPipeline()
    {
        using var db = Db();
        var entityNames = db.Model.GetEntityTypes().Select(x => x.ClrType.Name).ToHashSet();
        Assert.Contains(nameof(Exercise), entityNames);
        Assert.DoesNotContain("LessonSection", entityNames);
        Assert.DoesNotContain("LearningStep", entityNames);
        Assert.DoesNotContain("Question", entityNames);
        Assert.DoesNotContain("QuestionOption", entityNames);
    }

    [Fact]
    public void LessonAttempt_DoesNotPersistCurrentActivityPointer()
    {
        Assert.Null(typeof(LessonAttempt).GetProperty("CurrentActivityId"));
        using var db = Db();
        Assert.Null(db.Model.FindEntityType(typeof(LessonAttempt))!.FindProperty("CurrentActivityId"));
    }

    [Fact]
    public void ExerciseAttempt_UsesLessonAttemptExerciseAsOnlyParent()
    {
        Assert.Null(typeof(ExerciseAttempt).GetProperty("LessonAttemptId"));
        Assert.Null(typeof(ExerciseAttempt).GetProperty("ExerciseId"));
        var properties = typeof(ExerciseAttempt).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.Contains(nameof(ExerciseAttempt.LessonAttemptExerciseId), properties);
        using var db = Db();
        var entity = db.Model.FindEntityType(typeof(ExerciseAttempt))!;
        Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(LessonAttemptExercise), entity.GetForeignKeys().Single().PrincipalEntityType.ClrType);
    }

    [Fact]
    public void LessonAndExerciseShareDifficultyLevelType()
    {
        Assert.Equal(
            typeof(LanguageLearning.Common.Entities.LearningCatalog.Lesson).GetProperty("DifficultyLevel")!.PropertyType,
            typeof(Exercise).GetProperty("Difficulty")!.PropertyType);
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
