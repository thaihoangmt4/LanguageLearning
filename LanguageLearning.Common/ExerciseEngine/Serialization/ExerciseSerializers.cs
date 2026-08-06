using System.Text.Json;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.Results;

namespace LanguageLearning.Common.ExerciseEngine.Serialization;

public sealed class ExerciseContentSerializer : IExerciseContentSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Result<object> Deserialize(ExerciseType type, string json) => DeserializeKnown(type, json);

    public Result<string> Serialize(ExerciseType type, object content)
    {
        var expectedType = ModelType(type);
        if (expectedType is null)
            return Result<string>.Failure(ExerciseEngineErrors.UnsupportedExerciseType);
        if (!expectedType.IsInstanceOfType(content))
            return Result<string>.Failure(ExerciseEngineErrors.InvalidDefinition);

        return Result<string>.Success(JsonSerializer.Serialize(content, expectedType, Options));
    }

    private static Result<object> DeserializeKnown(ExerciseType type, string json)
    {
        var modelType = ModelType(type);
        if (modelType is null)
            return Result<object>.Failure(ExerciseEngineErrors.UnsupportedExerciseType);

        try
        {
            var value = JsonSerializer.Deserialize(json, modelType, Options);
            return value is null
                ? Result<object>.Failure(ExerciseEngineErrors.ContentDeserializationFailed)
                : Result<object>.Success(value);
        }
        catch (JsonException)
        {
            return Result<object>.Failure(ExerciseEngineErrors.ContentDeserializationFailed);
        }
    }

    private static Type? ModelType(ExerciseType type) => type switch
    {
        ExerciseType.MultipleChoice => typeof(MultipleChoiceContent),
        ExerciseType.ImageMatching => typeof(ImageMatchingContent),
        ExerciseType.AudioMatching => typeof(AudioMatchingContent),
        ExerciseType.Typing => typeof(TypingContent),
        ExerciseType.SentenceOrdering => typeof(SentenceOrderingContent),
        ExerciseType.Categorization => typeof(CategorizationContent),
        ExerciseType.Speaking => typeof(SpeakingContent),
        _ => null
    };
}

public sealed class ExerciseAnswerSerializer : IExerciseAnswerSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Result<object> Deserialize(ExerciseType type, string json)
    {
        var modelType = ModelType(type);
        if (modelType is null)
            return Result<object>.Failure(ExerciseEngineErrors.UnsupportedExerciseType);

        try
        {
            var value = JsonSerializer.Deserialize(json, modelType, Options);
            return value is null
                ? Result<object>.Failure(ExerciseEngineErrors.AnswerDeserializationFailed)
                : Result<object>.Success(value);
        }
        catch (JsonException)
        {
            return Result<object>.Failure(ExerciseEngineErrors.AnswerDeserializationFailed);
        }
    }

    private static Type? ModelType(ExerciseType type) => type switch
    {
        ExerciseType.MultipleChoice => typeof(MultipleChoiceAnswer),
        ExerciseType.ImageMatching => typeof(ImageMatchingAnswer),
        ExerciseType.AudioMatching => typeof(AudioMatchingAnswer),
        ExerciseType.Typing => typeof(TypingAnswer),
        ExerciseType.SentenceOrdering => typeof(SentenceOrderingAnswer),
        ExerciseType.Categorization => typeof(CategorizationAnswer),
        ExerciseType.Speaking => typeof(SpeakingAnswer),
        _ => null
    };
}
