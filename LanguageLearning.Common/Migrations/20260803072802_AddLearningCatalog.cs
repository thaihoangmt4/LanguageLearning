using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CefrLevel = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.Id);
                    table.CheckConstraint("CK_courses_DisplayOrder", "\"DisplayOrder\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.Id);
                    table.CheckConstraint("CK_units_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_units_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LearningObjectiveSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lessons", x => x.Id);
                    table.CheckConstraint("CK_lessons_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_lessons_EstimatedDurationMinutes", "\"EstimatedDurationMinutes\" > 0");
                    table.ForeignKey(
                        name: "FK_lessons_units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_courses_Code",
                table: "courses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courses_IsPublished_DisplayOrder",
                table: "courses",
                columns: new[] { "IsPublished", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_sections_LessonId_DisplayOrder",
                table: "lesson_sections",
                columns: new[] { "LessonId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lessons_UnitId_Code",
                table: "lessons",
                columns: new[] { "UnitId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lessons_UnitId_DisplayOrder",
                table: "lessons",
                columns: new[] { "UnitId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lessons_UnitId_IsPublished_DisplayOrder",
                table: "lessons",
                columns: new[] { "UnitId", "IsPublished", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_units_CourseId_Code",
                table: "units",
                columns: new[] { "CourseId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_CourseId_DisplayOrder",
                table: "units",
                columns: new[] { "CourseId", "DisplayOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_sections");

            migrationBuilder.DropTable(
                name: "lessons");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "courses");
        }
    }
}
