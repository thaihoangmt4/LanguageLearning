using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Persistence;

public sealed class LearningProgressModelTests
{
    [Fact]
    public void AssignmentModel_HasRequiredUniquenessAndForeignKeys()
    {
        using var db = Db();
        var entity = db.Model.FindEntityType(typeof(UserCourseAssignment))!;

        Assert.Contains(entity.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(UserCourseAssignment.UserId), nameof(UserCourseAssignment.CourseId)]));

        var activeIndex = entity.GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_user_course_assignments_UserId_Active");
        Assert.True(activeIndex.IsUnique);
        Assert.Equal("\"Status\" IN ('Assigned', 'InProgress')", activeIndex.GetFilter());

        Assert.Equal(2, entity.GetForeignKeys().Count());
        Assert.Contains(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(User));
        Assert.Contains(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType.Name == "Course");
    }

    [Fact]
    public void SprintSixAttemptConstraints_RemainConfigured()
    {
        using var db = Db();

        Assert.Contains(db.Model.FindEntityType(typeof(LessonAttemptExercise))!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(LessonAttemptExercise.LessonAttemptId), nameof(LessonAttemptExercise.DisplayOrder)]));
        Assert.Contains(db.Model.FindEntityType(typeof(ExerciseAttempt))!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(ExerciseAttempt.SubmissionId));
        Assert.Contains(db.Model.FindEntityType(typeof(ExerciseAttempt))!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ExerciseAttempt.LessonAttemptExerciseId), nameof(ExerciseAttempt.AttemptNumber)]));
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
