using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBatchGenSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_exercise_generation_settings_BatchSize",
                table: "exercise_generation_settings");

            migrationBuilder.DropColumn(
                name: "GenerationBatchSize",
                table: "exercise_generation_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GenerationBatchSize",
                table: "exercise_generation_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "exercise_generation_settings",
                keyColumn: "Id",
                keyValue: new Guid("e76d6ef3-df4c-4f42-88df-41114da06401"),
                column: "GenerationBatchSize",
                value: 20);

            migrationBuilder.AddCheckConstraint(
                name: "CK_exercise_generation_settings_BatchSize",
                table: "exercise_generation_settings",
                sql: "\"GenerationBatchSize\" BETWEEN 1 AND 50");
        }
    }
}
