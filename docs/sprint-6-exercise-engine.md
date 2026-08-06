# Sprint 6 Exercise Engine v2

## Architecture

Authored content is `Course -> Unit -> Lesson -> Exercise`. Runtime history is `LessonAttempt -> LessonAttemptExercise -> ExerciseAttempt`; `UserExerciseMistake` links a user to an exercise awaiting review. Exercise content remains validated `jsonb` on `Exercise`; activities reference its version and submissions are immutable.

Supported types are MultipleChoice, ImageMatching, AudioMatching, Typing, SentenceOrdering, Categorization, and Speaking. Enums are stored as strings. LessonSection, LearningStep, Question, and QuestionOption are absent from the application model; their names remain only where historical migrations need them for rollback.

## Learner flow and API

- `POST /api/v1/learning-sessions` resumes an in-progress attempt or starts the first eligible published lesson in course, unit, and lesson display order.
- `GET /api/v1/lesson-attempts/{lessonAttemptId}` returns the persisted ordered activities and derives the current activity as the first incomplete required row.
- `POST /api/v1/lesson-attempts/{lessonAttemptId}/activities/{activityId}/submissions` validates/evaluates an answer and atomically records its immutable submission, mistake changes, and progress.

Identity comes from authentication. ActivityType is Lesson or Review. Type-specific public mapping excludes raw ContentJson, answer keys, accepted answers, normalization settings, and scoring rules. Correct-answer feedback appears only after evaluation; typing exposes one canonical answer and speaking exposes none.

## Review and mistake lifecycle

A new attempt prepends at most three pending mistakes, oldest `LastFailedAt` first. Inactive exercises and exercises already in the selected lesson are excluded. Review rows reference the original exercise and never copy content.

Incorrect and partially-correct results create or update one pending mistake per user/exercise, preserve `FirstFailedAt`, increment failures, and clear resolution. A correct normal activity does not resolve it. A correct review increments `SuccessfulReviewCount`, resolves it, and preserves failure history. A future failure reopens the same row. NotEvaluated does not affect mistakes.

## Consistency and progress

Attempt creation and submission use serializable transactions. A partial unique PostgreSQL index permits one in-progress attempt per user. Unique activity order and attempt-number constraints protect sequencing and retries. SubmissionId is unique per attempt: replaying the same version and canonical JSON returns the stored result without mutation; a changed payload returns HTTP 409. A stale exercise version also returns 409 before evaluation or mutation.

TotalActivityCount includes lesson and review activities. CompletedActivityCount increases only on the first valid submission. TotalScore sums the best score per activity. CorrectCount and IncorrectCount describe each completed activity's current best evaluation (PartiallyCorrect is incorrect; NotEvaluated is neither), so an improved retry changes rather than duplicates counters. Completion requires a valid submission for every required activity, not correctness; optional activities do not block it. CompletedAt is assigned once and current activity is derived.

## Seeder and media

Development startup runs the idempotent `ExerciseEngineSeeder`. It upserts two published courses, four units, four lessons, and eight exercises by stable keys. Typed definitions are validated and serialized using production components. Examples include morning greetings, punctuation-normalized typing, audio recognition, speaking acknowledgement, fruit matching/categorization, and ordering repeated token text by stable IDs.

Media fields use stable GUID references. This repository currently has no MediaAsset store or URL resolver that converts them to playable URLs, so media playback remains an integration risk. No direct external URLs or competing resolver were introduced.

## Safety, query shape, and verification

Submission bodies are limited to 64 KiB. Definitions allow at most 20 options, 20 accepted answers, 100 tokens, 100 matching pairs, 20 categories, and 100 categorization items; typing is limited to 500 characters and may be further constrained by its definition. ASP.NET JSON defaults bound nesting. Type-aware validation rejects duplicate and unknown IDs.

Player state uses three bounded async projections: attempt/lesson metadata, ordered activity/exercise fields, and only the latest result per activity. Content is deserialized once per activity. No Redis cache or JSON GIN index was added.

Verification commands are `dotnet restore LanguageLearning.slnx`, `dotnet build LanguageLearning.slnx --no-restore`, and `dotnet test LanguageLearning.slnx --no-build`. The seeder test executes it twice and validates every persisted definition.

Deferred: frontend player, runtime AI generation and mistake variants, spaced repetition, adaptive paths, exercise-version history, speech recognition and scoring, admin authoring, Redis, and advanced analytics.
