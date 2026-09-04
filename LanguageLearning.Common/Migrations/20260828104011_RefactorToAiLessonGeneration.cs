using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class RefactorToAiLessonGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_lesson_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_lesson_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_lesson_progress_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_lesson_progress_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO user_lesson_progress ("Id", "UserId", "LessonId", "CompletedAt")
                SELECT DISTINCT ON ("UserId", "LessonId")
                    "Id", "UserId", "LessonId", "CompletedAt"
                FROM lesson_attempts
                WHERE "Status" = 'Completed' AND "CompletedAt" IS NOT NULL
                ORDER BY "UserId", "LessonId", "CompletedAt" DESC;
                """);

            migrationBuilder.DropTable(
                name: "exercise_attempts");

            migrationBuilder.DropTable(
                name: "exercise_generation_settings");

            migrationBuilder.DropTable(
                name: "lesson_attempt_exercises");

            migrationBuilder.DropTable(
                name: "lesson_attempts");

            migrationBuilder.DropTable(
                name: "user_exercise_mistakes");

            migrationBuilder.RenameColumn(
                name: "ExerciseGenerationEnabled",
                table: "system_settings",
                newName: "LessonGenerationEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_user_lesson_progress_LessonId",
                table: "user_lesson_progress",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_user_lesson_progress_UserId_LessonId",
                table: "user_lesson_progress",
                columns: new[] { "UserId", "LessonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_lesson_progress");

            migrationBuilder.RenameColumn(
                name: "LessonGenerationEnabled",
                table: "system_settings",
                newName: "ExerciseGenerationEnabled");

            migrationBuilder.CreateTable(
                name: "exercise_generation_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitialDelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    IntervalHours = table.Column<int>(type: "integer", nullable: false),
                    MaxExercisesPerLessonPerRun = table.Column<int>(type: "integer", nullable: false),
                    MinimumExerciseThreshold = table.Column<int>(type: "integer", nullable: false),
                    TargetExerciseCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_generation_settings", x => x.Id);
                    table.CheckConstraint("CK_exercise_generation_settings_ExerciseCounts", "\"MinimumExerciseThreshold\" BETWEEN 0 AND 500 AND \"TargetExerciseCount\" BETWEEN \"MinimumExerciseThreshold\" AND 500");
                    table.CheckConstraint("CK_exercise_generation_settings_InitialDelayMinutes", "\"InitialDelayMinutes\" BETWEEN 0 AND 1440");
                    table.CheckConstraint("CK_exercise_generation_settings_IntervalHours", "\"IntervalHours\" BETWEEN 1 AND 168");
                    table.CheckConstraint("CK_exercise_generation_settings_MaxPerRun", "\"MaxExercisesPerLessonPerRun\" BETWEEN 1 AND 200");
                    table.CheckConstraint("CK_exercise_generation_settings_Singleton", "\"Id\" = 'e76d6ef3-df4c-4f42-88df-41114da06401'::uuid");
                    table.ForeignKey(
                        name: "FK_exercise_generation_settings_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedActivityCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    IncorrectCount = table.Column<int>(type: "integer", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "InProgress"),
                    TotalActivityCount = table.Column<int>(type: "integer", nullable: false),
                    TotalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_attempts", x => x.Id);
                    table.CheckConstraint("CK_lesson_attempts_Counts", "\"CorrectCount\" >= 0 AND \"IncorrectCount\" >= 0 AND \"CompletedActivityCount\" >= 0 AND \"TotalActivityCount\" >= 0");
                    table.CheckConstraint("CK_lesson_attempts_TotalScore", "\"TotalScore\" >= 0 AND \"TotalScore\" <= 100");
                    table.ForeignKey(
                        name: "FK_lesson_attempts_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_attempts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_exercise_mistakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseVersion = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    FirstFailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastFailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    SuccessfulReviewCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_exercise_mistakes", x => x.Id);
                    table.CheckConstraint("CK_user_exercise_mistakes_Counts", "\"FailureCount\" > 0 AND \"SuccessfulReviewCount\" >= 0");
                    table.CheckConstraint("CK_user_exercise_mistakes_ExerciseVersion", "\"ExerciseVersion\" >= 1");
                    table.ForeignKey(
                        name: "FK_user_exercise_mistakes_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_exercise_mistakes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson_attempt_exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserExerciseMistakeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ExerciseVersion = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_attempt_exercises", x => x.Id);
                    table.CheckConstraint("CK_lesson_attempt_exercises_DisplayOrder", "\"DisplayOrder\" > 0");
                    table.CheckConstraint("CK_lesson_attempt_exercises_ExerciseVersion", "\"ExerciseVersion\" >= 1");
                    table.ForeignKey(
                        name: "FK_lesson_attempt_exercises_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_attempt_exercises_lesson_attempts_LessonAttemptId",
                        column: x => x.LessonAttemptId,
                        principalTable: "lesson_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_attempt_exercises_lessons_SourceLessonId",
                        column: x => x.SourceLessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_attempt_exercises_user_exercise_mistakes_UserExercis~",
                        column: x => x.UserExerciseMistakeId,
                        principalTable: "user_exercise_mistakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercise_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonAttemptExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerJson = table.Column<string>(type: "jsonb", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    EvaluationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "NotEvaluated"),
                    ExerciseVersion = table.Column<int>(type: "integer", nullable: false),
                    Feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_attempts", x => x.Id);
                    table.CheckConstraint("CK_exercise_attempts_AttemptNumber", "\"AttemptNumber\" > 0");
                    table.CheckConstraint("CK_exercise_attempts_ExerciseVersion", "\"ExerciseVersion\" >= 1");
                    table.CheckConstraint("CK_exercise_attempts_Score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 100)");
                    table.ForeignKey(
                        name: "FK_exercise_attempts_lesson_attempt_exercises_LessonAttemptExe~",
                        column: x => x.LessonAttemptExerciseId,
                        principalTable: "lesson_attempt_exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "exercise_generation_settings",
                columns: new[] { "Id", "InitialDelayMinutes", "IntervalHours", "MaxExercisesPerLessonPerRun", "MinimumExerciseThreshold", "TargetExerciseCount", "UpdatedAtUtc", "UpdatedByUserId", "Version" },
                values: new object[] { new Guid("e76d6ef3-df4c-4f42-88df-41114da06401"), 10, 24, 50, 20, 40, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("6d332c99-0a93-4cc0-a400-24931e424240") });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_attempts_LessonAttemptExerciseId_AttemptNumber",
                table: "exercise_attempts",
                columns: new[] { "LessonAttemptExerciseId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_attempts_SubmissionId",
                table: "exercise_attempts",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_generation_settings_UpdatedByUserId",
                table: "exercise_generation_settings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempt_exercises_ExerciseId",
                table: "lesson_attempt_exercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempt_exercises_LessonAttemptId_DisplayOrder",
                table: "lesson_attempt_exercises",
                columns: new[] { "LessonAttemptId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempt_exercises_SourceLessonId",
                table: "lesson_attempt_exercises",
                column: "SourceLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempt_exercises_UserExerciseMistakeId",
                table: "lesson_attempt_exercises",
                column: "UserExerciseMistakeId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempts_LessonId",
                table: "lesson_attempts",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempts_UserId_LessonId_InProgress",
                table: "lesson_attempts",
                columns: new[] { "UserId", "LessonId" },
                unique: true,
                filter: "\"Status\" = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempts_UserId_Status",
                table: "lesson_attempts",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_exercise_mistakes_ExerciseId",
                table: "user_exercise_mistakes",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_user_exercise_mistakes_UserId_ExerciseId",
                table: "user_exercise_mistakes",
                columns: new[] { "UserId", "ExerciseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_exercise_mistakes_UserId_Status_LastFailedAt",
                table: "user_exercise_mistakes",
                columns: new[] { "UserId", "Status", "LastFailedAt" });
        }
    }
}
