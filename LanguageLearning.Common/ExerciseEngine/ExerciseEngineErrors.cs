namespace LanguageLearning.Common.ExerciseEngine;

public static class ExerciseEngineErrors
{
    public const string UnsupportedExerciseType = "exercise.unsupported_type";
    public const string ContentDeserializationFailed = "exercise.content_deserialization_failed";
    public const string AnswerDeserializationFailed = "exercise.answer_deserialization_failed";
    public const string InvalidDefinition = "exercise.invalid_definition";
    public const string InvalidAnswer = "exercise.invalid_answer";
    public const string EvaluatorNotRegistered = "exercise.evaluator_not_registered";
    public const string PublicMapperNotRegistered = "exercise.public_mapper_not_registered";
}
