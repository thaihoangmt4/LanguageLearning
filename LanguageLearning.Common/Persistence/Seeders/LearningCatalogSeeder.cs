using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LanguageLearning.Common.Persistence.Seeders;

/// <summary>
/// Seeds the development learning catalog with stable, representative content.
/// </summary>
public sealed class LearningCatalogSeeder
{
    private const string EnglishA1Code = "ENGLISH-A1";
    private const string EnglishA2Code = "ENGLISH-A2";

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<LearningCatalogSeeder> _logger;

    public LearningCatalogSeeder(
        ApplicationDbContext dbContext,
        ILogger<LearningCatalogSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Inserts each missing development course and leaves existing catalog data unchanged.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var requiredCodes = new[] { EnglishA1Code, EnglishA2Code };
        var existingCodes = await _dbContext.Courses
            .AsNoTracking()
            .Where(course => requiredCodes.Contains(course.Code))
            .Select(course => course.Code)
            .ToHashSetAsync(cancellationToken);

        var missingCourses = new List<CourseSeedGraph>();

        if (!existingCodes.Contains(EnglishA1Code))
        {
            missingCourses.Add(CreateEnglishA1());
        }

        if (!existingCodes.Contains(EnglishA2Code))
        {
            missingCourses.Add(CreateEnglishA2());
        }

        if (missingCourses.Count == 0)
        {
            _logger.LogInformation(
                "Development learning catalog seed skipped because all required courses already exist.");
            return;
        }

        foreach (var graph in missingCourses)
        {
            _dbContext.Courses.Add(graph.Course);
            _dbContext.Units.AddRange(graph.Units);
            _dbContext.Lessons.AddRange(graph.Lessons);
            _dbContext.LessonSections.AddRange(graph.Sections);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {CourseCount} development learning catalog course(s): {CourseCodes}.",
            missingCourses.Count,
            missingCourses.Select(graph => graph.Course.Code).ToArray());
    }

    private static CourseSeedGraph CreateEnglishA1()
    {
        var course = new Course
        {
            Code = EnglishA1Code,
            Title = "English A1",
            Description = "Build a strong foundation for everyday English communication.",
            CefrLevel = CefrLevel.A1,
            DisplayOrder = 10,
            IsPublished = true
        };

        var gettingStarted = CreateUnit(
            course,
            "A1-UNIT-01",
            "Getting Started",
            "Learn essential English for greetings, introductions and basic personal information.",
            10);

        var greetings = CreateLesson(
            gettingStarted,
            "A1-U01-L01",
            "Greetings and Introductions",
            "Learn how to greet people and introduce yourself.",
            "Use common greetings, say your name and ask another person's name.",
            10,
            LessonDifficulty.Introductory,
            10);

        var countries = CreateLesson(
            gettingStarted,
            "A1-U01-L02",
            "Countries and Nationalities",
            "Learn how to say where you are from and describe nationality.",
            "Ask and answer basic questions about countries and nationalities.",
            12,
            LessonDifficulty.Standard,
            20);

        var everydayLife = CreateUnit(
            course,
            "A1-UNIT-02",
            "Everyday Life",
            "Learn basic language for routines, time and common daily activities.",
            20);

        var routines = CreateLesson(
            everydayLife,
            "A1-U02-L01",
            "Daily Routines",
            "Learn vocabulary and expressions for common daily activities.",
            "Describe a simple daily routine using the present simple tense.",
            15,
            LessonDifficulty.Standard,
            10);

        var tellingTime = CreateLesson(
            everydayLife,
            "A1-U02-L02",
            "Telling the Time",
            "Learn how to ask for and tell the time.",
            "Understand and use basic expressions for clock times.",
            10,
            LessonDifficulty.Standard,
            20);

        var lessons = new[] { greetings, countries, routines, tellingTime };
        var sections = new[]
        {
            CreateSection(greetings, LessonSectionType.Introduction, "Lesson introduction", 10, true),
            CreateSection(greetings, LessonSectionType.Vocabulary, "Basic greetings", 20, true),
            CreateSection(greetings, LessonSectionType.Grammar, "Subject pronouns and the verb to be", 30, true),
            CreateSection(greetings, LessonSectionType.Practice, "Introduce yourself", 40, true),
            CreateSection(greetings, LessonSectionType.Review, "Lesson review", 50, true),

            CreateSection(countries, LessonSectionType.Introduction, "Lesson introduction", 10, true),
            CreateSection(countries, LessonSectionType.Vocabulary, "Countries and nationalities", 20, true),
            CreateSection(countries, LessonSectionType.Grammar, "Where are you from?", 30, true),
            CreateSection(countries, LessonSectionType.Listening, "Listen to short introductions", 40, false),
            CreateSection(countries, LessonSectionType.Practice, "Talk about your country", 50, true),

            CreateSection(routines, LessonSectionType.Vocabulary, "Daily activities", 10, true),
            CreateSection(routines, LessonSectionType.Grammar, "Present simple for routines", 20, true),
            CreateSection(routines, LessonSectionType.Listening, "Listen to a daily routine", 30, false),
            CreateSection(routines, LessonSectionType.Practice, "Describe your routine", 40, true),
            CreateSection(routines, LessonSectionType.Summary, "Lesson summary", 50, true),

            CreateSection(tellingTime, LessonSectionType.Introduction, "Understanding clock time", 10, true),
            CreateSection(tellingTime, LessonSectionType.Vocabulary, "Numbers and time expressions", 20, true),
            CreateSection(tellingTime, LessonSectionType.Listening, "Listen and identify the time", 30, true),
            CreateSection(tellingTime, LessonSectionType.Speaking, "Say the time aloud", 40, false),
            CreateSection(tellingTime, LessonSectionType.Practice, "Ask and answer about time", 50, true)
        };

        return new CourseSeedGraph(
            course,
            [gettingStarted, everydayLife],
            lessons,
            sections);
    }

    private static CourseSeedGraph CreateEnglishA2()
    {
        var course = new Course
        {
            Code = EnglishA2Code,
            Title = "English A2",
            Description = "Develop practical English for familiar everyday situations.",
            CefrLevel = CefrLevel.A2,
            DisplayOrder = 20,
            IsPublished = false
        };

        var unit = CreateUnit(
            course,
            "A2-UNIT-01",
            "Expanding Everyday English",
            "Build confidence in common everyday conversations.",
            10);

        var lesson = CreateLesson(
            unit,
            "A2-U01-L01",
            "Talking About Past Experiences",
            "Learn basic language for describing completed past events.",
            "Describe a simple past experience using common past-tense forms.",
            15,
            LessonDifficulty.Introductory,
            10);

        var sections = new[]
        {
            CreateSection(lesson, LessonSectionType.Introduction, "Introduction to past experiences", 10, true),
            CreateSection(lesson, LessonSectionType.Grammar, "Past simple forms", 20, true),
            CreateSection(lesson, LessonSectionType.Practice, "Describe a past experience", 30, true),
            CreateSection(lesson, LessonSectionType.Review, "Lesson review", 40, true)
        };

        return new CourseSeedGraph(course, [unit], [lesson], sections);
    }

    private static Unit CreateUnit(
        Course course,
        string code,
        string title,
        string description,
        int displayOrder)
    {
        return new Unit
        {
            CourseId = course.Id,
            Course = course,
            Code = code,
            Title = title,
            Description = description,
            DisplayOrder = displayOrder
        };
    }

    private static Lesson CreateLesson(
        Unit unit,
        string code,
        string title,
        string description,
        string learningObjectiveSummary,
        int estimatedDurationMinutes,
        LessonDifficulty difficulty,
        int displayOrder)
    {
        return new Lesson
        {
            UnitId = unit.Id,
            Unit = unit,
            Code = code,
            Title = title,
            Description = description,
            LearningObjectiveSummary = learningObjectiveSummary,
            EstimatedDurationMinutes = estimatedDurationMinutes,
            DifficultyLevel = difficulty,
            DisplayOrder = displayOrder,
            IsPublished = true
        };
    }

    private static LessonSection CreateSection(
        Lesson lesson,
        LessonSectionType sectionType,
        string title,
        int displayOrder,
        bool isRequired)
    {
        return new LessonSection
        {
            LessonId = lesson.Id,
            Lesson = lesson,
            SectionType = sectionType,
            Title = title,
            DisplayOrder = displayOrder,
            IsRequired = isRequired
        };
    }

    private sealed record CourseSeedGraph(
        Course Course,
        IReadOnlyCollection<Unit> Units,
        IReadOnlyCollection<Lesson> Lessons,
        IReadOnlyCollection<LessonSection> Sections);
}
