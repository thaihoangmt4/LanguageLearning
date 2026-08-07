using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "exercises",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercises_LessonId_ContentHash",
                table: "exercises",
                columns: new[] { "LessonId", "ContentHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exercises_LessonId_ContentHash",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "exercises");
        }
    }
}
