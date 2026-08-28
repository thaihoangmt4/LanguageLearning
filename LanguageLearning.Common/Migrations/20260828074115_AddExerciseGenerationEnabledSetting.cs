using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseGenerationEnabledSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExerciseGenerationEnabled",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "Id",
                keyValue: new Guid("389cd8b7-6f49-4c8f-bdf8-7bcae005b3cc"),
                column: "ExerciseGenerationEnabled",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExerciseGenerationEnabled",
                table: "system_settings");
        }
    }
}
