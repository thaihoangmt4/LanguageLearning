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

        foreach (var graph in missingCourses)
        {
            _dbContext.Courses.Add(graph.Course);
            _dbContext.Units.AddRange(graph.Units);
            _dbContext.Lessons.AddRange(graph.Lessons);
            _dbContext.LessonSections.AddRange(graph.Sections);
        }

        if (missingCourses.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await SeedBasicFruitsAsync(cancellationToken);

        _logger.LogInformation(
            "Synchronized the development catalog, adding {CourseCount} missing course(s): {CourseCodes}.",
            missingCourses.Count,
            missingCourses.Select(graph => graph.Course.Code).ToArray());
    }

    private async Task SeedBasicFruitsAsync(CancellationToken cancellationToken)
    {
        var unit = await _dbContext.Units
            .SingleAsync(unit => unit.Course.Code == EnglishA1Code && unit.Code == "A1-UNIT-02", cancellationToken);

        var lesson = await _dbContext.Lessons
            .SingleOrDefaultAsync(lesson => lesson.UnitId == unit.Id && lesson.Code == "A1-U02-L03", cancellationToken);

        if (lesson is null)
        {
            lesson = new Lesson { UnitId = unit.Id, Unit = unit, Code = "A1-U02-L03" };
            _dbContext.Lessons.Add(lesson);
        }

        lesson.Title = "Basic Fruits";
        lesson.Description = "Learn five common fruit words through pictures, audio, and short questions.";
        lesson.LearningObjectiveSummary = "Recognize, understand, and spell five basic fruit words in English.";
        lesson.EstimatedDurationMinutes = 12;
        lesson.DifficultyLevel = DifficultyLevel.Beginner;
        lesson.DisplayOrder = 30;
        lesson.Status = LessonStatus.Published;

        var vocabularySeeds = new[]
        {
            new VocabularySeed("apple", "quả táo", "/ˈæp.əl/", "This is a red apple.", "Đây là một quả táo đỏ."),
            new VocabularySeed("orange", "quả cam", "/ˈɒr.ɪndʒ/", "The orange is sweet.", "Quả cam có vị ngọt."),
            new VocabularySeed("banana", "quả chuối", "/bəˈnɑː.nə/", "I eat a banana for breakfast.", "Tôi ăn một quả chuối vào bữa sáng."),
            new VocabularySeed("grape", "quả nho", "/ɡreɪp/", "This grape is purple.", "Quả nho này có màu tím."),
            new VocabularySeed("strawberry", "quả dâu tây", "/ˈstrɔː.bər.i/", "She likes strawberry ice cream.", "Cô ấy thích kem dâu tây.")
        };

        var words = vocabularySeeds.Select(seed => seed.Word).ToArray();
        var vocabularies = await _dbContext.Vocabularies
            .Where(vocabulary => words.Contains(vocabulary.Word))
            .ToDictionaryAsync(vocabulary => vocabulary.Word, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var seed in vocabularySeeds)
        {
            if (!vocabularies.TryGetValue(seed.Word, out var vocabulary))
            {
                vocabulary = new Vocabulary { Word = seed.Word };
                vocabularies.Add(seed.Word, vocabulary);
                _dbContext.Vocabularies.Add(vocabulary);
            }

            vocabulary.Meaning = seed.Meaning;
            vocabulary.Phonetic = seed.Phonetic;
            vocabulary.PartOfSpeech = PartOfSpeech.Noun;
            vocabulary.ExampleSentence = seed.ExampleSentence;
            vocabulary.ExampleTranslation = seed.ExampleTranslation;
            vocabulary.ImageUrl = $"/media/vocabulary/{seed.Word}.webp";
            vocabulary.AudioUrl = $"/media/audio/{seed.Word}.mp3";
            vocabulary.DifficultyLevel = DifficultyLevel.Beginner;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var stepSeeds = CreateFruitStepSeeds(vocabularies);
        var existingSteps = await _dbContext.LearningSteps
            .Where(step => step.LessonId == lesson.Id)
            .Include(step => step.Question)
                .ThenInclude(question => question!.Options)
            .ToDictionaryAsync(step => step.DisplayOrder, cancellationToken);

        foreach (var seed in stepSeeds)
        {
            if (!existingSteps.TryGetValue(seed.DisplayOrder, out var step))
            {
                step = new LearningStep { LessonId = lesson.Id, Lesson = lesson, DisplayOrder = seed.DisplayOrder };
                _dbContext.LearningSteps.Add(step);
            }

            step.StepType = seed.StepType;
            step.IsRequired = true;
            step.VocabularyId = seed.Vocabulary.Id;
            step.Vocabulary = seed.Vocabulary;
            step.InstructionTitle = seed.InstructionTitle;
            step.InstructionText = seed.InstructionText;

            if (seed.Question is null)
            {
                if (step.Question is not null)
                {
                    _dbContext.Questions.Remove(step.Question);
                }

                continue;
            }

            var question = step.Question;
            if (question is null)
            {
                question = new Question { LearningStepId = step.Id, LearningStep = step };
                _dbContext.Questions.Add(question);
            }

            SynchronizeQuestion(question, seed.Question);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await ValidateBasicFruitsAsync(lesson.Id, cancellationToken);
    }

    private async Task ValidateBasicFruitsAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        var lesson = await _dbContext.Lessons
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.LearningSteps)
                .ThenInclude(step => step.Question)
                    .ThenInclude(question => question!.Options)
            .SingleAsync(item => item.Id == lessonId, cancellationToken);

        var steps = lesson.LearningSteps.ToArray();
        var questionSteps = steps.Where(step => step.StepType == LearningStepType.Question).ToArray();
        var instructionSteps = steps.Where(step => step.StepType == LearningStepType.Instruction).ToArray();
        var multipleChoiceQuestions = questionSteps
            .Select(step => step.Question!)
            .Where(question => question.QuestionType != QuestionType.TextInput)
            .ToArray();
        var textInputQuestions = questionSteps
            .Select(step => step.Question!)
            .Where(question => question.QuestionType == QuestionType.TextInput)
            .ToArray();

        var isValid = lesson.Status == LessonStatus.Published
            && steps.Any(step => step.IsRequired)
            && steps.Length == 10
            && steps.Select(step => step.DisplayOrder).Distinct().Count() == steps.Length
            && steps.All(step => step.VocabularyId is not null)
            && questionSteps.All(step => step.Question is not null)
            && instructionSteps.All(step => step.Question is null)
            && multipleChoiceQuestions.All(question =>
                question.Options.Count >= 2
                && question.Options.Count(option => option.IsCorrect) == 1
                && question.Options.Select(option => option.DisplayOrder).Distinct().Count() == question.Options.Count)
            && textInputQuestions.All(question =>
                !string.IsNullOrWhiteSpace(question.TextAnswer)
                && question.Options.Count == 0);

        if (!isValid)
        {
            throw new InvalidOperationException("The Basic Fruits interactive seed graph failed validation.");
        }

        _logger.LogInformation(
            "Validated Basic Fruits seed graph: {StepCount} steps, {QuestionCount} questions, {OptionCount} options.",
            steps.Length,
            questionSteps.Length,
            questionSteps.Sum(step => step.Question!.Options.Count));
    }

    private void SynchronizeQuestion(Question question, QuestionSeed seed)
    {
        question.QuestionType = seed.Type;
        question.Prompt = seed.Prompt;
        question.PromptImageUrl = seed.PromptImageUrl;
        question.PromptAudioUrl = seed.PromptAudioUrl;
        question.Explanation = seed.Explanation;
        question.TargetVocabularyId = seed.TargetVocabulary.Id;
        question.TargetVocabulary = seed.TargetVocabulary;
        question.TextAnswer = seed.TextAnswer;
        question.IsCaseSensitive = false;

        var existingOptions = question.Options.ToDictionary(option => option.DisplayOrder);
        var expectedOrders = seed.Options.Select(option => option.DisplayOrder).ToHashSet();
        foreach (var obsolete in question.Options.Where(option => !expectedOrders.Contains(option.DisplayOrder)).ToArray())
        {
            _dbContext.QuestionOptions.Remove(obsolete);
        }

        foreach (var seedOption in seed.Options)
        {
            if (!existingOptions.TryGetValue(seedOption.DisplayOrder, out var option))
            {
                option = new QuestionOption
                {
                    QuestionId = question.Id,
                    Question = question,
                    DisplayOrder = seedOption.DisplayOrder
                };
                _dbContext.QuestionOptions.Add(option);
            }

            option.Text = seedOption.Text;
            option.ImageUrl = seedOption.ImageUrl;
            option.AccessibilityText = seedOption.AccessibilityText;
            option.AudioUrl = null;
            option.IsCorrect = seedOption.IsCorrect;
        }
    }

    private static IReadOnlyCollection<StepSeed> CreateFruitStepSeeds(
        IReadOnlyDictionary<string, Vocabulary> vocabulary)
    {
        var apple = vocabulary["apple"];
        var orange = vocabulary["orange"];
        var banana = vocabulary["banana"];
        var grape = vocabulary["grape"];
        var strawberry = vocabulary["strawberry"];

        return
        [
            StepSeed.Instruction(1, apple, "Apple", "Apple means ‘quả táo’. Listen and repeat: apple."),
            StepSeed.QuestionStep(2, apple, new(QuestionType.TextMultipleChoice,
                "Choose the English word for ‘quả táo’.", null, null, "Apple is the English word for ‘quả táo’.", null,
                [new(1, "apple", null, null, true), new(2, "orange", null, null, false), new(3, "banana", null, null, false)], apple)),
            StepSeed.QuestionStep(3, apple, new(QuestionType.ImageMultipleChoice,
                "Select the apple image.", null, null, "The apple is the round red fruit.", null,
                [new(1, null, apple.ImageUrl, "A red apple", true), new(2, null, orange.ImageUrl, "An orange", false), new(3, null, banana.ImageUrl, "A yellow banana", false)], apple)),
            StepSeed.Instruction(4, orange, "Orange", "Orange means ‘quả cam’. Listen and repeat: orange."),
            StepSeed.QuestionStep(5, orange, new(QuestionType.AudioMultipleChoice,
                "Listen and choose the word you hear.", null, orange.AudioUrl, "The recording says ‘orange’.", null,
                [new(1, "apple", null, null, false), new(2, "orange", null, null, true), new(3, "grape", null, null, false)], orange)),
            StepSeed.Instruction(6, banana, "Banana", "Banana means ‘quả chuối’. Listen and repeat: banana."),
            StepSeed.QuestionStep(7, banana, new(QuestionType.TextInput,
                "Type the English word for ‘quả chuối’.", null, null, "Banana is the English word for ‘quả chuối’.", "banana", [], banana)),
            StepSeed.Instruction(8, grape, "Grape", "Grape means ‘quả nho’. Listen and repeat: grape."),
            StepSeed.Instruction(9, strawberry, "Strawberry", "Strawberry means ‘quả dâu tây’. Listen and repeat: strawberry."),
            StepSeed.QuestionStep(10, strawberry, new(QuestionType.ImageMultipleChoice,
                "Which image shows a strawberry?", null, null, "A strawberry is the small red fruit with seeds on its surface.", null,
                [new(1, null, grape.ImageUrl, "Purple grapes", false), new(2, null, strawberry.ImageUrl, "A red strawberry", true), new(3, null, apple.ImageUrl, "A red apple", false)], strawberry))
        ];
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
            DifficultyLevel.Beginner,
            10);

        var countries = CreateLesson(
            gettingStarted,
            "A1-U01-L02",
            "Countries and Nationalities",
            "Learn how to say where you are from and describe nationality.",
            "Ask and answer basic questions about countries and nationalities.",
            12,
            DifficultyLevel.Elementary,
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
            DifficultyLevel.Elementary,
            10);

        var tellingTime = CreateLesson(
            everydayLife,
            "A1-U02-L02",
            "Telling the Time",
            "Learn how to ask for and tell the time.",
            "Understand and use basic expressions for clock times.",
            10,
            DifficultyLevel.Elementary,
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
            DifficultyLevel.Beginner,
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
        DifficultyLevel difficulty,
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
            Status = LessonStatus.Published
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

    private sealed record VocabularySeed(
        string Word,
        string Meaning,
        string Phonetic,
        string ExampleSentence,
        string ExampleTranslation);

    private sealed record OptionSeed(
        int DisplayOrder,
        string? Text,
        string? ImageUrl,
        string? AccessibilityText,
        bool IsCorrect);

    private sealed record QuestionSeed(
        QuestionType Type,
        string Prompt,
        string? PromptImageUrl,
        string? PromptAudioUrl,
        string Explanation,
        string? TextAnswer,
        IReadOnlyCollection<OptionSeed> Options,
        Vocabulary TargetVocabulary);

    private sealed record StepSeed(
        int DisplayOrder,
        LearningStepType StepType,
        Vocabulary Vocabulary,
        string? InstructionTitle,
        string? InstructionText,
        QuestionSeed? Question)
    {
        public static StepSeed Instruction(int order, Vocabulary vocabulary, string title, string text) =>
            new(order, LearningStepType.Instruction, vocabulary, title, text, null);

        public static StepSeed QuestionStep(int order, Vocabulary vocabulary, QuestionSeed question) =>
            new(order, LearningStepType.Question, vocabulary, null, null, question);
    }
}
