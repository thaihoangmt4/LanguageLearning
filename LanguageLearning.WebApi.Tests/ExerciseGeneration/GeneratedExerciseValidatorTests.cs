using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseGeneration;

public sealed class GeneratedExerciseValidatorTests
{
    private readonly GeneratedExerciseValidator _validator = new();

    [Fact]
    public async Task Validator_AcceptsValidPayloadForEveryExerciseType()
    {
        foreach (var exercise in ValidExercises())
        {
            var result = await _validator.ValidateAsync(exercise, TestContext.Current.CancellationToken);
            Assert.True(result.IsValid, $"{exercise.Type}: {string.Join(", ", result.Errors)}");
        }
    }

    [Fact]
    public async Task Validator_RejectsUnknownExerciseType()
    {
        var exercise = new GeneratedExercise((ExerciseType)999, "Question", [], null, null);
        var result = await _validator.ValidateAsync(exercise, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validator_RejectsMalformedTypeSpecificPayloads()
    {
        GeneratedExercise[] malformed =
        [
            new(ExerciseType.MultipleChoice, "Question", ["same", "SAME"], "same", null),
            new(ExerciseType.ImageMatching, "Match", [], null, null, ImageMatches:
                [new(Guid.Empty, "Apple"), new(Guid.NewGuid(), "Apple")]),
            new(ExerciseType.AudioMatching, "Listen", ["A", "B"], "C", null,
                PronunciationText: "A"),
            new(ExerciseType.Typing, "Type", [], "", null),
            new(ExerciseType.SentenceOrdering, "Order", [], null, null,
                OrderedSegments: ["only one"]),
            new(ExerciseType.Categorization, "Sort", [], null, null,
                Categories: [new("Same", ["A", "B"]), new("same", ["C", "D"])]),
            new(ExerciseType.Speaking, "Speak", [], null, null, ReferenceText: "")
        ];

        foreach (var exercise in malformed)
        {
            var result = await _validator.ValidateAsync(exercise, TestContext.Current.CancellationToken);
            Assert.False(result.IsValid, exercise.Type.ToString());
        }
    }

    private static GeneratedExercise[] ValidExercises()
    {
        var imageOne = Guid.NewGuid();
        var imageTwo = Guid.NewGuid();
        return
        [
            new(ExerciseType.MultipleChoice, "Choose.", ["Yes", "No"], "Yes", "Why"),
            new(ExerciseType.ImageMatching, "Match.", [], null, "Why", ImageMatches:
                [new(imageOne, "Apple"), new(imageTwo, "Banana")]),
            new(ExerciseType.AudioMatching, "Choose what you hear.", ["Hello", "Goodbye"], "Hello", "Why",
                PronunciationText: "Hello"),
            new(ExerciseType.Typing, "Type a greeting.", [], "Hello", "Why"),
            new(ExerciseType.SentenceOrdering, "Build a sentence.", [], null, "Why",
                OrderedSegments: ["I", "am", "ready"]),
            new(ExerciseType.Categorization, "Sort the words.", [], null, "Why",
                Categories: [new("Fruit", ["Apple", "Pear"]), new("Vegetable", ["Carrot", "Pea"])]),
            new(ExerciseType.Speaking, "Read aloud.", [], null, null, ReferenceText: "Hello, how are you?")
        ];
    }
}
