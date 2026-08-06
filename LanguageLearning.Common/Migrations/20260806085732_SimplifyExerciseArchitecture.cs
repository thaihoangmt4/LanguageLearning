using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyExerciseArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exercise_attempts_exercises_ExerciseId",
                table: "exercise_attempts");

            migrationBuilder.DropForeignKey(
                name: "FK_exercise_attempts_lesson_attempts_LessonAttemptId",
                table: "exercise_attempts");

            migrationBuilder.DropForeignKey(
                name: "FK_lesson_attempts_lesson_attempt_exercises_CurrentActivityId",
                table: "lesson_attempts");

            migrationBuilder.DropTable(
                name: "lesson_sections");

            migrationBuilder.DropTable(
                name: "question_options");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "learning_steps");

            migrationBuilder.DropIndex(
                name: "IX_lesson_attempts_CurrentActivityId",
                table: "lesson_attempts");

            migrationBuilder.DropIndex(
                name: "IX_exercise_attempts_ExerciseId",
                table: "exercise_attempts");

            migrationBuilder.DropIndex(
                name: "IX_exercise_attempts_LessonAttemptExerciseId",
                table: "exercise_attempts");

            migrationBuilder.DropIndex(
                name: "IX_exercise_attempts_LessonAttemptId_ExerciseId_AttemptNumber",
                table: "exercise_attempts");

            migrationBuilder.DropIndex(
                name: "IX_exercise_attempts_LessonAttemptId_SubmissionId",
                table: "exercise_attempts");

            migrationBuilder.DropColumn(
                name: "CurrentActivityId",
                table: "lesson_attempts");

            migrationBuilder.DropColumn(
                name: "ExerciseId",
                table: "exercise_attempts");

            migrationBuilder.DropColumn(
                name: "LessonAttemptId",
                table: "exercise_attempts");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exercise_attempts_LessonAttemptExerciseId_AttemptNumber",
                table: "exercise_attempts");

            migrationBuilder.DropIndex(
                name: "IX_exercise_attempts_SubmissionId",
                table: "exercise_attempts");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentActivityId",
                table: "lesson_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExerciseId",
                table: "exercise_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LessonAttemptId",
                table: "exercise_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE exercise_attempts a SET \"ExerciseId\" = activity.\"ExerciseId\", " +
                "\"LessonAttemptId\" = activity.\"LessonAttemptId\" FROM lesson_attempt_exercises activity " +
                "WHERE activity.\"Id\" = a.\"LessonAttemptExerciseId\"");

            migrationBuilder.AlterColumn<Guid>(
                name: "ExerciseId",
                table: "exercise_attempts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "LessonAttemptId",
                table: "exercise_attempts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql(
                "UPDATE lesson_attempts attempt SET \"CurrentActivityId\" = (" +
                "SELECT activity.\"Id\" FROM lesson_attempt_exercises activity " +
                "WHERE activity.\"LessonAttemptId\" = attempt.\"Id\" AND activity.\"IsRequired\" " +
                "AND activity.\"CompletedAt\" IS NULL ORDER BY activity.\"DisplayOrder\" LIMIT 1)");

            migrationBuilder.CreateTable(
                name: "learning_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    VocabularyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    InstructionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InstructionTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    StepType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_steps", x => x.Id);
                    table.CheckConstraint("CK_learning_steps_DisplayOrder", "\"DisplayOrder\" > 0");
                    table.ForeignKey(
                        name: "FK_learning_steps_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_learning_steps_vocabularies_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SectionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_sections", x => x.Id);
                    table.CheckConstraint("CK_lesson_sections_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_lesson_sections_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetVocabularyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsCaseSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PromptAudioUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PromptImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    QuestionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TextAnswer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_questions_learning_steps_LearningStepId",
                        column: x => x.LearningStepId,
                        principalTable: "learning_steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_questions_vocabularies_TargetVocabularyId",
                        column: x => x.TargetVocabularyId,
                        principalTable: "vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessibilityText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AudioUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_options", x => x.Id);
                    table.CheckConstraint("CK_question_options_DisplayOrder", "\"DisplayOrder\" > 0");
                    table.ForeignKey(
                        name: "FK_question_options_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempts_CurrentActivityId",
                table: "lesson_attempts",
                column: "CurrentActivityId");

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
                name: "IX_learning_steps_LessonId_DisplayOrder",
                table: "learning_steps",
                columns: new[] { "LessonId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_steps_VocabularyId",
                table: "learning_steps",
                column: "VocabularyId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_sections_LessonId_DisplayOrder",
                table: "lesson_sections",
                columns: new[] { "LessonId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_options_QuestionId_DisplayOrder",
                table: "question_options",
                columns: new[] { "QuestionId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_questions_LearningStepId",
                table: "questions",
                column: "LearningStepId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_questions_TargetVocabularyId",
                table: "questions",
                column: "TargetVocabularyId");

            migrationBuilder.AddForeignKey(
                name: "FK_exercise_attempts_exercises_ExerciseId",
                table: "exercise_attempts",
                column: "ExerciseId",
                principalTable: "exercises",
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
                name: "FK_lesson_attempts_lesson_attempt_exercises_CurrentActivityId",
                table: "lesson_attempts",
                column: "CurrentActivityId",
                principalTable: "lesson_attempt_exercises",
                principalColumn: "Id");
        }
    }
}
