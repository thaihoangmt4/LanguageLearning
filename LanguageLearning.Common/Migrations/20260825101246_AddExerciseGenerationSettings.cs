using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseGenerationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercise_generation_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InitialDelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    IntervalHours = table.Column<int>(type: "integer", nullable: false),
                    MinimumExerciseThreshold = table.Column<int>(type: "integer", nullable: false),
                    TargetExerciseCount = table.Column<int>(type: "integer", nullable: false),
                    MaxExercisesPerLessonPerRun = table.Column<int>(type: "integer", nullable: false),
                    GenerationBatchSize = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_generation_settings", x => x.Id);
                    table.CheckConstraint("CK_exercise_generation_settings_BatchSize", "\"GenerationBatchSize\" BETWEEN 1 AND 50");
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

            migrationBuilder.InsertData(
                table: "exercise_generation_settings",
                columns: new[] { "Id", "GenerationBatchSize", "InitialDelayMinutes", "IntervalHours", "MaxExercisesPerLessonPerRun", "MinimumExerciseThreshold", "TargetExerciseCount", "UpdatedAtUtc", "UpdatedByUserId", "Version" },
                values: new object[] { new Guid("e76d6ef3-df4c-4f42-88df-41114da06401"), 20, 10, 24, 50, 20, 40, new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("6d332c99-0a93-4cc0-a400-24931e424240") });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_generation_settings_UpdatedByUserId",
                table: "exercise_generation_settings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_generation_settings");
        }
    }
}
