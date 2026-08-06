using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Persistence;

public sealed class ExerciseEngineSeeder(
    ApplicationDbContext dbContext,
    IExerciseContentSerializer serializer,
    IExerciseDefinitionValidatorResolver definitionValidator,
    ILogger<ExerciseEngineSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var foundations = await UpsertCourseAsync("FOUNDATIONS", "English Foundations", 1, CefrLevel.A1, cancellationToken);
        var communication = await UpsertCourseAsync("COMMUNICATION", "Everyday Communication", 2, CefrLevel.A2, cancellationToken);

        var vocabulary = await UpsertUnitAsync(foundations, "FOUND-VOCAB", "Vocabulary Essentials", 1, cancellationToken);
        var listening = await UpsertUnitAsync(foundations, "FOUND-LISTEN", "Listening and Speaking", 2, cancellationToken);
        var visual = await UpsertUnitAsync(communication, "COMM-VISUAL", "Visual Connections", 1, cancellationToken);
        var sentences = await UpsertUnitAsync(communication, "COMM-SENTENCE", "Building Sentences", 2, cancellationToken);

        var greetings = await UpsertLessonAsync(vocabulary, "GREETINGS", "Useful Greetings", 1, cancellationToken);
        var sounds = await UpsertLessonAsync(listening, "SOUNDS", "Listen and Respond", 1, cancellationToken);
        var connections = await UpsertLessonAsync(visual, "CONNECTIONS", "Words and Categories", 1, cancellationToken);
        var ordering = await UpsertLessonAsync(sentences, "ORDERING", "Natural Sentence Order", 1, cancellationToken);

        await UpsertExerciseAsync(greetings, 1, ExerciseType.MultipleChoice, "Choose the right greeting",
            "Choose the greeting normally used before noon.", new MultipleChoiceContent(
                "Which greeting is most appropriate at 9:00 a.m.?",
                [new(Id(101), "Good morning"), new(Id(102), "Good evening"), new(Id(103), "Good night")],
                Id(101), "Good morning is used from early morning until around noon."), cancellationToken);
        await UpsertExerciseAsync(greetings, 2, ExerciseType.Typing, "Write a morning greeting",
            "Type an appropriate morning greeting.", new TypingContent(
                "Write a friendly greeting for someone you meet at 8:00 a.m.",
                ["Good morning!", "Morning!"], false, true,
                "Good morning is the standard morning greeting.", 80), cancellationToken);

        await UpsertExerciseAsync(sounds, 1, ExerciseType.AudioMatching, "Identify the spoken phrase",
            "Listen and choose the phrase you hear.", new AudioMatchingContent("How are you?",
                [new(Id(202), "How are you?"), new(Id(203), "Where are you?"), new(Id(204), "Who are you?")],
                Id(202), "The recording says: How are you?"), cancellationToken);
        await UpsertExerciseAsync(sounds, 2, ExerciseType.Speaking, "Introduce yourself",
            "Read the sentence aloud, then acknowledge completion.", new SpeakingContent(
                "Read this introduction aloud.", "Hello, my name is Alex. It is nice to meet you.", Id(205)), cancellationToken);

        await UpsertExerciseAsync(connections, 1, ExerciseType.ImageMatching, "Match pictures and words",
            "Match each picture to the correct English word.", new ImageMatchingContent(
                [new(Id(301), Id(302), "A red apple"), new(Id(303), Id(304), "A yellow banana")],
                [new(Id(305), "Apple"), new(Id(306), "Banana")],
                [new(Id(301), Id(305)), new(Id(303), Id(306))],
                "Apple and banana are common fruit words."), cancellationToken);
        await UpsertExerciseAsync(connections, 2, ExerciseType.Categorization, "Sort food words",
            "Put each word into the correct category.", new CategorizationContent(
                [new(Id(307), "Apple"), new(Id(308), "Carrot"), new(Id(309), "Banana"), new(Id(310), "Potato")],
                [new(Id(311), "Fruit"), new(Id(312), "Vegetable")],
                [new(Id(307), Id(311)), new(Id(308), Id(312)), new(Id(309), Id(311)), new(Id(310), Id(312))],
                "Apples and bananas are fruits; carrots and potatoes are vegetables."), cancellationToken);

        await UpsertExerciseAsync(ordering, 1, ExerciseType.SentenceOrdering, "Build a natural sentence",
            "Arrange the tokens using their identifiers.", new SentenceOrderingContent(
                "Arrange the words into a grammatical sentence.",
                [new(Id(401), "I"), new(Id(402), "think"), new(Id(403), "that"), new(Id(404), "that"), new(Id(405), "works")],
                [Id(401), Id(402), Id(403), Id(404), Id(405)],
                "The repeated word 'that' is ordered by token ID, not by text."), cancellationToken);
        await UpsertExerciseAsync(ordering, 2, ExerciseType.MultipleChoice, "Choose the natural sentence",
            "Select the sentence with natural English word order.", new MultipleChoiceContent(
                "Which sentence has natural English word order?",
                [new(Id(406), "She drinks tea every morning."), new(Id(407), "She every morning tea drinks."), new(Id(408), "Drinks she tea every morning.")],
                Id(406), "English statements normally follow subject, verb, object, then time expression."), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Exercise Engine development curriculum seeded: {CourseCount} courses, {UnitCount} units, {LessonCount} lessons, {ExerciseCount} exercises",
            2, 4, 4, 8);
    }

    private async Task<Course> UpsertCourseAsync(string code, string title, int order, CefrLevel level, CancellationToken token)
    {
        var value = await dbContext.Courses.SingleOrDefaultAsync(x => x.Code == code, token);
        if (value is null) { value = new Course { Code = code }; dbContext.Courses.Add(value); }
        value.Title = title; value.DisplayOrder = order; value.CefrLevel = level; value.IsPublished = true;
        return value;
    }

    private async Task<Unit> UpsertUnitAsync(Course course, string code, string title, int order, CancellationToken token)
    {
        var value = await dbContext.Units.SingleOrDefaultAsync(x => x.CourseId == course.Id && x.Code == code, token);
        if (value is null) { value = new Unit { Course = course, Code = code }; dbContext.Units.Add(value); }
        value.Title = title; value.DisplayOrder = order;
        return value;
    }

    private async Task<Lesson> UpsertLessonAsync(Unit unit, string code, string title, int order, CancellationToken token)
    {
        var value = await dbContext.Lessons.SingleOrDefaultAsync(x => x.UnitId == unit.Id && x.Code == code, token);
        if (value is null) { value = new Lesson { Unit = unit, Code = code }; dbContext.Lessons.Add(value); }
        value.Title = title; value.Description = $"Practice {title.ToLowerInvariant()} with interactive exercises.";
        value.LearningObjectiveSummary = $"Complete the core activities in {title}.";
        value.DisplayOrder = order; value.EstimatedDurationMinutes = 10; value.DifficultyLevel = DifficultyLevel.Beginner;
        value.Status = LessonStatus.Published;
        return value;
    }

    private async Task UpsertExerciseAsync(Lesson lesson, int order, ExerciseType type, string title,
        string instruction, object content, CancellationToken token)
    {
        var validation = definitionValidator.Validate(type, content);
        if (validation.IsFailure)
            throw new InvalidOperationException($"Seed exercise '{title}' has an invalid {type} definition: {validation.Error}");
        var serialized = serializer.Serialize(type, content);
        if (serialized.IsFailure)
            throw new InvalidOperationException($"Seed exercise '{title}' could not be serialized: {serialized.Error}");
        var value = await dbContext.Exercises.SingleOrDefaultAsync(x => x.LessonId == lesson.Id && x.DisplayOrder == order, token);
        if (value is null) { value = new Exercise { Lesson = lesson, DisplayOrder = order }; dbContext.Exercises.Add(value); }
        value.Type = type; value.Title = title; value.Instruction = instruction; value.Difficulty = DifficultyLevel.Beginner;
        value.ContentJson = serialized.Value; value.Version = 1; value.IsRequired = true; value.IsActive = true;
    }

    private static Guid Id(int suffix) => Guid.Parse($"00000000-0000-0000-0000-{suffix:D12}");
}
