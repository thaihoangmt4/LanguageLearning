using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddInteractiveLearningFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lessons_UnitId_IsPublished_DisplayOrder",
                table: "lessons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_lessons_DisplayOrder",
                table: "lessons");

            migrationBuilder.AddCheckConstraint(
                name: "CK_lessons_DisplayOrder",
                table: "lessons",
                sql: "\"DisplayOrder\" > 0");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "lessons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.Sql(
                "UPDATE lessons SET \"Status\" = CASE WHEN \"IsPublished\" THEN 'Published' ELSE 'Draft' END");

            migrationBuilder.Sql(
                "UPDATE lessons SET \"DifficultyLevel\" = CASE " +
                "WHEN \"DifficultyLevel\" = 'Introductory' THEN 'Beginner' " +
                "WHEN \"DifficultyLevel\" = 'Standard' THEN 'Elementary' " +
                "ELSE \"DifficultyLevel\" END");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "lessons");

            migrationBuilder.CreateTable(
                name: "vocabularies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Word = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Meaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Phonetic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PartOfSpeech = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExampleSentence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExampleTranslation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AudioUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DifficultyLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocabularies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "learning_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    VocabularyId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstructionTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InstructionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Prompt = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PromptImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PromptAudioUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TargetVocabularyId = table.Column<Guid>(type: "uuid", nullable: true),
                    TextAnswer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCaseSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AudioUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AccessibilityText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "IX_lessons_UnitId_Status_DisplayOrder",
                table: "lessons",
                columns: new[] { "UnitId", "Status", "DisplayOrder" });

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

            migrationBuilder.CreateIndex(
                name: "IX_vocabularies_DifficultyLevel",
                table: "vocabularies",
                column: "DifficultyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_vocabularies_Word",
                table: "vocabularies",
                column: "Word");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_options");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "learning_steps");

            migrationBuilder.DropTable(
                name: "vocabularies");

            migrationBuilder.DropIndex(
                name: "IX_lessons_UnitId_Status_DisplayOrder",
                table: "lessons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_lessons_DisplayOrder",
                table: "lessons");

            migrationBuilder.AddCheckConstraint(
                name: "CK_lessons_DisplayOrder",
                table: "lessons",
                sql: "\"DisplayOrder\" >= 0");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "lessons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE lessons SET \"IsPublished\" = (\"Status\" = 'Published')");

            migrationBuilder.Sql(
                "UPDATE lessons SET \"DifficultyLevel\" = CASE " +
                "WHEN \"DifficultyLevel\" = 'Beginner' THEN 'Introductory' " +
                "WHEN \"DifficultyLevel\" = 'Elementary' THEN 'Standard' " +
                "ELSE \"DifficultyLevel\" END");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "lessons");

            migrationBuilder.CreateIndex(
                name: "IX_lessons_UnitId_IsPublished_DisplayOrder",
                table: "lessons",
                columns: new[] { "UnitId", "IsPublished", "DisplayOrder" });
        }
    }
}
