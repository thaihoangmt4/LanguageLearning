using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningWorkflowConcurrencyAndProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_lesson_attempts_Counts",
                table: "lesson_attempts");

            migrationBuilder.AddColumn<int>(
                name: "CompletedActivityCount",
                table: "lesson_attempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalActivityCount",
                table: "lesson_attempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE lesson_attempts a SET \"CompletedActivityCount\" = counts.completed, \"TotalActivityCount\" = counts.total " +
                "FROM (SELECT \"LessonAttemptId\", COUNT(*)::integer AS total, " +
                "COUNT(\"CompletedAt\")::integer AS completed FROM lesson_attempt_exercises GROUP BY \"LessonAttemptId\") counts " +
                "WHERE a.\"Id\" = counts.\"LessonAttemptId\"");

            migrationBuilder.Sql(
                "WITH ranked AS (SELECT \"Id\", ROW_NUMBER() OVER " +
                "(PARTITION BY \"UserId\" ORDER BY \"StartedAt\", \"Id\") AS position " +
                "FROM lesson_attempts WHERE \"Status\" = 'InProgress') " +
                "UPDATE lesson_attempts a SET \"Status\" = 'Abandoned' FROM ranked " +
                "WHERE a.\"Id\" = ranked.\"Id\" AND ranked.position > 1");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempts_UserId_InProgress",
                table: "lesson_attempts",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'InProgress'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_lesson_attempts_Counts",
                table: "lesson_attempts",
                sql: "\"CorrectCount\" >= 0 AND \"IncorrectCount\" >= 0 AND \"CompletedActivityCount\" >= 0 AND \"TotalActivityCount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lesson_attempts_UserId_InProgress",
                table: "lesson_attempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_lesson_attempts_Counts",
                table: "lesson_attempts");

            migrationBuilder.DropColumn(
                name: "CompletedActivityCount",
                table: "lesson_attempts");

            migrationBuilder.DropColumn(
                name: "TotalActivityCount",
                table: "lesson_attempts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_lesson_attempts_Counts",
                table: "lesson_attempts",
                sql: "\"CorrectCount\" >= 0 AND \"IncorrectCount\" >= 0");
        }
    }
}
