using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.Results;

namespace LanguageLearning.Common.ExerciseEngine;

public interface IExerciseContentSerializer
{
    Result<object> Deserialize(ExerciseType type, string json);
    Result<string> Serialize(ExerciseType type, object content);
}
public interface IExerciseAnswerSerializer { Result<object> Deserialize(ExerciseType type, string json); }
public interface IExerciseDefinitionValidator { ExerciseType ExerciseType { get; } Result Validate(object content); }
public interface IExerciseAnswerValidator { ExerciseType ExerciseType { get; } Result Validate(object content, object answer); }
public interface IExerciseDefinitionValidatorResolver { Result Validate(ExerciseType type, object content); }
public interface IExerciseAnswerValidatorResolver { Result Validate(ExerciseType type, object content, object answer); }
public interface IExercisePublicContentMapper { Result<object> Map(ExerciseType type, object content); }
public interface IExercisePublicContentMappingStrategy { ExerciseType ExerciseType { get; } object Map(object content); }
public interface IExerciseEvaluator<TContent, TAnswer> { ExerciseEvaluationResult Evaluate(TContent content, TAnswer answer); }
public interface IExerciseEvaluationStrategy { ExerciseType ExerciseType { get; } ExerciseEvaluationResult Evaluate(object content, object answer); }
public interface IExerciseEvaluatorResolver { Result<ExerciseEvaluationResult> Evaluate(ExerciseType type, object content, object answer); }
