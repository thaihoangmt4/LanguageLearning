using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Serialization;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Persistence;

public sealed class ExerciseEngineSeederTests
{
    [Fact]
    public async Task Seeder_IsIdempotentAndCreatesValidTypedCurriculum()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var serializer = new ExerciseContentSerializer();
        var definitions = new ExerciseDefinitionValidatorResolver([
            new MultipleChoiceDefinitionValidator(), new ImageMatchingDefinitionValidator(),
            new AudioMatchingDefinitionValidator(), new TypingDefinitionValidator(),
            new SentenceOrderingDefinitionValidator(), new CategorizationDefinitionValidator(),
            new SpeakingDefinitionValidator()
        ]);
        var seeder = new ExerciseEngineSeeder(db, serializer, definitions, NullLogger<ExerciseEngineSeeder>.Instance);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        var firstCounts = await CountsAsync(db);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        Assert.Equal(firstCounts, await CountsAsync(db));
        Assert.Equal((2, 4, 4, 40), firstCounts);
        Assert.Equal(Enum.GetValues<ExerciseType>().Order(), await db.Exercises.Select(x => x.Type).Distinct().Order().ToArrayAsync(TestContext.Current.CancellationToken));

        foreach (var exercise in await db.Exercises.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken))
        {
            var content = serializer.Deserialize(exercise.Type, exercise.ContentJson);
            Assert.True(content.IsSuccess);
            Assert.True(definitions.Validate(exercise.Type, content.Value).IsSuccess);
        }
        var audioExercise = await db.Exercises.AsNoTracking().OrderBy(x => x.DisplayOrder).FirstAsync(
            x => x.Type == ExerciseType.AudioMatching, TestContext.Current.CancellationToken);
        var audioContent = Assert.IsType<LanguageLearning.Common.ExerciseEngine.Models.AudioMatchingContent>(
            serializer.Deserialize(ExerciseType.AudioMatching, audioExercise.ContentJson).Value);
        Assert.Equal("How are you?", audioContent.PronunciationText);
        Assert.DoesNotContain("audioMediaId", audioExercise.ContentJson, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.UserLessonProgress);
    }

    private static async Task<(int Courses, int Units, int Lessons, int Exercises)> CountsAsync(ApplicationDbContext db) =>
        (await db.Courses.CountAsync(TestContext.Current.CancellationToken),
            await db.Units.CountAsync(TestContext.Current.CancellationToken),
            await db.Lessons.CountAsync(TestContext.Current.CancellationToken),
            await db.Exercises.CountAsync(TestContext.Current.CancellationToken));
}
