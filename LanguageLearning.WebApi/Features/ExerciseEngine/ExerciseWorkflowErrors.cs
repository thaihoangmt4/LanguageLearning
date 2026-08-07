namespace LanguageLearning.WebApi.Features.ExerciseEngine;

public static class ExerciseWorkflowErrors
{
    public const string LearningPathCompleted = "learning_path.completed";
    public const string NoPublishedContent = "learning_path.no_published_content";
    public const string NoActiveAssignment = "learning_path.no_active_assignment";
    public const string ActiveLessonAttemptConflict = "lesson_attempt.active_conflict";
    public const string LessonAttemptNotFound = "lesson_attempt.not_found";
    public const string LessonAttemptCompleted = "lesson_attempt.completed";
    public const string LessonAttemptForbidden = "lesson_attempt.forbidden";
    public const string LessonAttemptExerciseNotFound = "lesson_attempt_exercise.not_found";
    public const string ExerciseNotPartOfAttempt = "exercise.not_part_of_attempt";
    public const string ExerciseNotCurrent = "exercise.not_current";
    public const string ExerciseInactive = "exercise.inactive";
    public const string ExerciseVersionMismatch = "exercise.version_mismatch";
    public const string SubmissionPayloadMismatch = "exercise.submission_payload_mismatch";
    public const string CurrentUserUnavailable = "current_user.unavailable";
}
