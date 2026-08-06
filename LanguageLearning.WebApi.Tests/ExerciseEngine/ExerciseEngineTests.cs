using System.Text.Json;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Evaluation;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.ExerciseEngine.PublicContent;
using LanguageLearning.Common.ExerciseEngine.Serialization;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.Common.Results;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseEngine;

public sealed class ExerciseEngineTests
{
    private readonly Guid _one = Guid.NewGuid();
    private readonly Guid _two = Guid.NewGuid();

    [Theory]
    [InlineData(ExerciseType.MultipleChoice)]
    [InlineData(ExerciseType.ImageMatching)]
    [InlineData(ExerciseType.AudioMatching)]
    [InlineData(ExerciseType.Typing)]
    [InlineData(ExerciseType.SentenceOrdering)]
    [InlineData(ExerciseType.Categorization)]
    [InlineData(ExerciseType.Speaking)]
    public void ContentSerializer_RoundTripsEveryType(ExerciseType type)
    {
        var serializer = new ExerciseContentSerializer();
        var serialized = serializer.Serialize(type, Content(type));
        Assert.True(serialized.IsSuccess);
        Assert.True(serializer.Deserialize(type, serialized.Value).IsSuccess);
    }

    [Fact]
    public void Serializers_ReturnClearErrorsForInvalidJsonAndUnsupportedType()
    {
        Assert.Equal(ExerciseEngineErrors.ContentDeserializationFailed,
            new ExerciseContentSerializer().Deserialize(ExerciseType.Typing, "{").Error);
        Assert.Equal(ExerciseEngineErrors.AnswerDeserializationFailed,
            new ExerciseAnswerSerializer().Deserialize(ExerciseType.Typing, "{").Error);
        Assert.Equal(ExerciseEngineErrors.UnsupportedExerciseType,
            new ExerciseContentSerializer().Deserialize((ExerciseType)999, "{}").Error);
    }

    [Fact]
    public void DefinitionValidators_RejectInvalidDefinitions()
    {
        var duplicatedOptions = new MultipleChoiceContent("Question", [new(_one, "A"), new(_one, "B")], _one, null);
        Assert.Equal(ExerciseEngineErrors.InvalidDefinition, new MultipleChoiceDefinitionValidator().Validate(duplicatedOptions).Error);

        var duplicateNormalizedTyping = new TypingContent("Prompt", ["Hello!", " hello "], false, true, null, null);
        Assert.Equal(ExerciseEngineErrors.InvalidDefinition, new TypingDefinitionValidator().Validate(duplicateNormalizedTyping).Error);

        var incompleteOrder = new SentenceOrderingContent("Prompt", [new(_one, "same"), new(_two, "same")], [_one], null);
        Assert.Equal(ExerciseEngineErrors.InvalidDefinition, new SentenceOrderingDefinitionValidator().Validate(incompleteOrder).Error);
    }

    [Fact]
    public void AnswerValidators_RejectStructurallyInvalidAnswers()
    {
        var choice = (MultipleChoiceContent)Content(ExerciseType.MultipleChoice);
        Assert.Equal(ExerciseEngineErrors.InvalidAnswer,
            new MultipleChoiceAnswerValidator().Validate(choice, new MultipleChoiceAnswer(Guid.NewGuid())).Error);

        var order = (SentenceOrderingContent)Content(ExerciseType.SentenceOrdering);
        Assert.Equal(ExerciseEngineErrors.InvalidAnswer,
            new SentenceOrderingAnswerValidator().Validate(order, new SentenceOrderingAnswer([_one, _one])).Error);

        var categorization = (CategorizationContent)Content(ExerciseType.Categorization);
        Assert.Equal(ExerciseEngineErrors.InvalidAnswer,
            new CategorizationAnswerValidator().Validate(categorization,
                new CategorizationAnswer([new(_one, _one), new(_one, _two)])).Error);
    }

    [Theory]
    [InlineData(ExerciseType.MultipleChoice)]
    [InlineData(ExerciseType.ImageMatching)]
    [InlineData(ExerciseType.AudioMatching)]
    [InlineData(ExerciseType.Typing)]
    [InlineData(ExerciseType.SentenceOrdering)]
    [InlineData(ExerciseType.Categorization)]
    [InlineData(ExerciseType.Speaking)]
    public void PublicContent_DoesNotExposeAnswerKeys(ExerciseType type)
    {
        var mapper = PublicMapper(type);
        var json = JsonSerializer.Serialize(mapper.Map(Content(type)));
        string[] forbidden = ["correctOptionId", "acceptedAnswers", "correctOrder", "correctMatches", "correctAssignments", "caseSensitive", "ignorePunctuation"];
        Assert.DoesNotContain(forbidden, key => json.Contains(key, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MultipleChoiceAndAudioMatching_EvaluateBinaryScores()
    {
        var choice = (MultipleChoiceContent)Content(ExerciseType.MultipleChoice);
        Assert.Equal(EvaluationStatus.Correct, new MultipleChoiceEvaluator().Evaluate(choice, new(_one)).Status);
        Assert.Equal(0m, new MultipleChoiceEvaluator().Evaluate(choice, new(_two)).Score);
        var audio = (AudioMatchingContent)Content(ExerciseType.AudioMatching);
        Assert.Equal(100m, new AudioMatchingEvaluator().Evaluate(audio, new(_one)).Score);
    }

    [Fact]
    public void AudioMatching_PublicContentExposesPronunciationTextWithoutMediaOrAnswerKey()
    {
        var content = new AudioMatchingContent("How are you?",
            [new(_one, "How are you?"), new(_two, "Where are you?")], _one, "Explanation");
        var json = JsonSerializer.Serialize(new AudioMatchingPublicMapper().Map(content));
        Assert.Contains("pronunciationText", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("How are you?", json);
        Assert.DoesNotContain("audioMediaId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctOptionId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TypingEvaluator_AppliesConfiguredNormalizationAndReturnsOneExpectedAnswer()
    {
        var content = new TypingContent("Prompt", ["Hello, world!", "Hi world"], false, true, "Why", null);
        var result = new TypingEvaluator().Evaluate(content, new("  hello world  "));
        Assert.Equal(EvaluationStatus.Correct, result.Status);
        Assert.Equal("Hello, world!", result.CorrectAnswer);
    }

    [Fact]
    public void SentenceOrdering_UsesIdsWhenTokenTextIsDuplicated()
    {
        var content = (SentenceOrderingContent)Content(ExerciseType.SentenceOrdering);
        Assert.Equal(EvaluationStatus.Correct, new SentenceOrderingEvaluator().Evaluate(content, new([_one, _two])).Status);
        Assert.Equal(EvaluationStatus.Incorrect, new SentenceOrderingEvaluator().Evaluate(content, new([_two, _one])).Status);
    }

    [Fact]
    public void ImageMatching_CalculatesPartialScoreAndItemDetails()
    {
        var three = Guid.NewGuid();
        var content = new ImageMatchingContent(
            [new(_one, Guid.NewGuid(), "One"), new(_two, Guid.NewGuid(), "Two"), new(three, Guid.NewGuid(), "Three")],
            [new(_one, "One"), new(_two, "Two"), new(three, "Three")],
            [new(_one, _one), new(_two, _two), new(three, three)], "Explanation");
        var result = new ImageMatchingEvaluator().Evaluate(content, new([new(_one, _one), new(_two, three), new(three, _two)]));
        Assert.Equal(EvaluationStatus.PartiallyCorrect, result.Status);
        Assert.Equal(33.33m, result.Score);
        Assert.IsType<ItemEvaluationDetail[]>(result.Details);
    }

    [Fact]
    public void Categorization_CalculatesPartialScore()
    {
        var content = (CategorizationContent)Content(ExerciseType.Categorization);
        var result = new CategorizationEvaluator().Evaluate(content, new([new(_one, _one), new(_two, _one)]));
        Assert.Equal(EvaluationStatus.PartiallyCorrect, result.Status);
        Assert.Equal(50m, result.Score);
    }

    [Fact]
    public void Speaking_IsAcknowledgedThenNotEvaluated()
    {
        var content = (SpeakingContent)Content(ExerciseType.Speaking);
        Assert.False(new SpeakingAnswerValidator().Validate(content, new SpeakingAnswer(false)).IsSuccess);
        var result = new SpeakingEvaluator().Evaluate(content, new(true));
        Assert.Equal(EvaluationStatus.NotEvaluated, result.Status);
        Assert.Null(result.Score);
    }

    [Fact]
    public void Resolver_DetectsDuplicateRegistrations()
    {
        IExerciseEvaluationStrategy[] duplicates = [new MultipleChoiceEvaluator(), new MultipleChoiceEvaluator()];
        Assert.Throws<InvalidOperationException>(() => new ExerciseEvaluatorResolver(duplicates));
    }

    private object Content(ExerciseType type) => type switch
    {
        ExerciseType.MultipleChoice => new MultipleChoiceContent("Question", [new(_one, "A"), new(_two, "B")], _one, "Explanation"),
        ExerciseType.ImageMatching => new ImageMatchingContent(
            [new(_one, Guid.NewGuid(), "One"), new(_two, Guid.NewGuid(), "Two")],
            [new(_one, "One"), new(_two, "Two")], [new(_one, _one), new(_two, _two)], "Explanation"),
        ExerciseType.AudioMatching => new AudioMatchingContent("A", [new(_one, "A"), new(_two, "B")], _one, "Explanation"),
        ExerciseType.Typing => new TypingContent("Prompt", ["answer"], false, true, "Explanation", 100),
        ExerciseType.SentenceOrdering => new SentenceOrderingContent("Prompt", [new(_one, "same"), new(_two, "same")], [_one, _two], "Explanation"),
        ExerciseType.Categorization => new CategorizationContent([new(_one, "One"), new(_two, "Two")],
            [new(_one, "First"), new(_two, "Second")], [new(_one, _one), new(_two, _two)], "Explanation"),
        ExerciseType.Speaking => new SpeakingContent("Say this", "Reference", null),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static IExercisePublicContentMappingStrategy PublicMapper(ExerciseType type) => type switch
    {
        ExerciseType.MultipleChoice => new MultipleChoicePublicMapper(),
        ExerciseType.ImageMatching => new ImageMatchingPublicMapper(),
        ExerciseType.AudioMatching => new AudioMatchingPublicMapper(),
        ExerciseType.Typing => new TypingPublicMapper(),
        ExerciseType.SentenceOrdering => new SentenceOrderingPublicMapper(),
        ExerciseType.Categorization => new CategorizationPublicMapper(),
        ExerciseType.Speaking => new SpeakingPublicMapper(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
