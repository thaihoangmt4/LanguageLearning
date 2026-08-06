using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseEngineV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instruction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.Id);
                    table.CheckConstraint("CK_exercises_DisplayOrder", "\"DisplayOrder\" > 0");
                    table.CheckConstraint("CK_exercises_Version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_exercises_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_exercise_mistakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    FirstFailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastFailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulReviewCount = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "exercise_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonAttemptExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseVersion = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    AnswerJson = table.Column<string>(type: "jsonb", nullable: false),
                    EvaluationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "NotEvaluated"),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_attempts", x => x.Id);
                    table.CheckConstraint("CK_exercise_attempts_AttemptNumber", "\"AttemptNumber\" > 0");
                    table.CheckConstraint("CK_exercise_attempts_ExerciseVersion", "\"ExerciseVersion\" >= 1");
                    table.CheckConstraint("CK_exercise_attempts_Score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 100)");
                    table.ForeignKey(
                        name: "FK_exercise_attempts_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson_attempt_exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseVersion = table.Column<int>(type: "integer", nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SourceLessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserExerciseMistakeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "lesson_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "InProgress"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    IncorrectCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_attempts", x => x.Id);
                    table.CheckConstraint("CK_lesson_attempts_Counts", "\"CorrectCount\" >= 0 AND \"IncorrectCount\" >= 0");
                    table.CheckConstraint("CK_lesson_attempts_TotalScore", "\"TotalScore\" >= 0 AND \"TotalScore\" <= 100");
                    table.ForeignKey(
                        name: "FK_lesson_attempts_lesson_attempt_exercises_CurrentActivityId",
                        column: x => x.CurrentActivityId,
                        principalTable: "lesson_attempt_exercises",
                        principalColumn: "Id");
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

            migrationBuilder.CreateIndex(
                name: "IX_exercise_attempts_ExerciseId",
                table: "exercise_attempts",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_attempts_LessonAttemptExerciseId",
                table: "exercise_attempts",
                column: "LessonAttemptExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_attempts_LessonAttemptId_ExerciseId_AttemptNumber",
                table: "exercise_attempts",
                columns: new[] { "LessonAttemptId", "ExerciseId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_attempts_LessonAttemptId_SubmissionId",
                table: "exercise_attempts",
                columns: new[] { "LessonAttemptId", "SubmissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercises_LessonId_DisplayOrder",
                table: "exercises",
                columns: new[] { "LessonId", "DisplayOrder" },
                unique: true);

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
                name: "IX_lesson_attempts_CurrentActivityId",
                table: "lesson_attempts",
                column: "CurrentActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempts_LessonId",
                table: "lesson_attempts",
                column: "LessonId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_exercise_attempts_lesson_attempt_exercises_LessonAttemptExe~",
                table: "exercise_attempts",
                column: "LessonAttemptExerciseId",
                principalTable: "lesson_attempt_exercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_exercise_attempts_lesson_attempts_LessonAttemptId",
                table: "exercise_attempts",
                column: "LessonAttemptId",
                principalTable: "lesson_attempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lesson_attempt_exercises_lesson_attempts_LessonAttemptId",
                table: "lesson_attempt_exercises",
                column: "LessonAttemptId",
                principalTable: "lesson_attempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lesson_attempt_exercises_exercises_ExerciseId",
                table: "lesson_attempt_exercises");

            migrationBuilder.DropForeignKey(
                name: "FK_user_exercise_mistakes_exercises_ExerciseId",
                table: "user_exercise_mistakes");

            migrationBuilder.DropForeignKey(
                name: "FK_lesson_attempts_lesson_attempt_exercises_CurrentActivityId",
                table: "lesson_attempts");

            migrationBuilder.DropTable(
                name: "exercise_attempts");

            migrationBuilder.DropTable(
                name: "exercises");

            migrationBuilder.DropTable(
                name: "lesson_attempt_exercises");

            migrationBuilder.DropTable(
                name: "lesson_attempts");

            migrationBuilder.DropTable(
                name: "user_exercise_mistakes");
        }
    }
}
