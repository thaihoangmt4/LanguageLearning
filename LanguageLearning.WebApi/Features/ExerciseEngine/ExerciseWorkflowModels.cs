using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Models;

namespace LanguageLearning.WebApi.Features.ExerciseEngine;

public enum LearningSessionMode { Started = 1, Resumed = 2 }

public sealed record LearningPathResolution(Guid? LessonAttemptId, Guid LessonId, bool IsResume);
public sealed record LearningSessionResult(Guid LessonAttemptId, Guid LessonId, LearningSessionMode Mode, LessonAttemptStatus Status);
public sealed record ExerciseSubmission(Guid LessonAttemptId, Guid LessonAttemptExerciseId,
    int ExerciseVersion, Guid SubmissionId, string AnswerJson);
public sealed record ExerciseSubmissionResult(Guid ExerciseAttemptId, Guid SubmissionId, Guid LessonAttemptId,
    Guid ActivityId, Guid ExerciseId, ExerciseType ExerciseType, int ExerciseVersion, int AttemptNumber,
    bool IsReplay, ExerciseEvaluationResult Evaluation, int CompletedActivityCount, int TotalActivityCount,
    Guid? NextActivityId, LessonAttemptStatus LessonAttemptStatus, DateTime SubmittedAt);
