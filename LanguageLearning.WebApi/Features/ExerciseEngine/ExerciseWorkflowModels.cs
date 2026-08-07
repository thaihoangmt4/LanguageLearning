using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Models;

namespace LanguageLearning.WebApi.Features.ExerciseEngine;

public enum LearningSessionMode { Started = 1, Resumed = 2 }

public enum LearningPathState { Resume = 1, StartNextLesson = 2, CourseCompleted = 3, NoActiveAssignment = 4 }
public sealed record LearningPathResolution(
    LearningPathState State,
    Guid? AssignmentId,
    Guid? CourseId,
    Guid? LessonAttemptId,
    Guid? LessonId,
    Guid? NextActivityId,
    string? LessonTitle,
    string? UnitTitle,
    int? EstimatedDurationMinutes)
{
    public bool IsResume => State == LearningPathState.Resume;
}
public sealed record LearningSessionResult(Guid LessonAttemptId, Guid LessonId, LearningSessionMode Mode, LessonAttemptStatus Status);
public sealed record ExerciseSubmission(Guid LessonAttemptId, Guid LessonAttemptExerciseId,
    int ExerciseVersion, Guid SubmissionId, string AnswerJson);
public sealed record ExerciseSubmissionResult(Guid ExerciseAttemptId, Guid SubmissionId, Guid LessonAttemptId,
    Guid ActivityId, Guid ExerciseId, ExerciseType ExerciseType, int ExerciseVersion, int AttemptNumber,
    bool IsReplay, ExerciseEvaluationResult Evaluation, int CompletedActivityCount, int TotalActivityCount,
    Guid? NextActivityId, LessonAttemptStatus LessonAttemptStatus, DateTime SubmittedAt);
