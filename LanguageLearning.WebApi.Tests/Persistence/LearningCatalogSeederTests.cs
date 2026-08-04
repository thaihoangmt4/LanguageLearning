using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Persistence;

public sealed class LearningCatalogSeederTests
{
    [Fact]
    public async Task Seeder_IsIdempotentAndProducesValidBasicFruitsGraph()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var seeder = new LearningCatalogSeeder(
            db,
            NullLogger<LearningCatalogSeeder>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;

        await seeder.SeedAsync(cancellationToken);
        var first = await CountsAsync(db, cancellationToken);
        await seeder.SeedAsync(cancellationToken);
        var second = await CountsAsync(db, cancellationToken);

        Assert.Equal(first, second);
        Assert.Equal((2, 3, 6, 5, 10, 5, 12), second);

        var fruitLesson = await db.Lessons
            .AsNoTracking()
            .SingleAsync(lesson => lesson.Code == "A1-U02-L03", cancellationToken);
        Assert.Equal("Published", fruitLesson.Status.ToString());
    }

    private static async Task<(int Courses, int Units, int Lessons, int Vocabulary, int Steps, int Questions, int Options)>
        CountsAsync(ApplicationDbContext db, CancellationToken cancellationToken) =>
        (await db.Courses.CountAsync(cancellationToken),
            await db.Units.CountAsync(cancellationToken),
            await db.Lessons.CountAsync(cancellationToken),
            await db.Vocabularies.CountAsync(cancellationToken),
            await db.LearningSteps.CountAsync(cancellationToken),
            await db.Questions.CountAsync(cancellationToken),
            await db.QuestionOptions.CountAsync(cancellationToken));
}
