using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningProgressAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lesson_attempts_UserId_InProgress",
                table: "lesson_attempts");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAccessedAt",
                table: "lesson_attempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_course_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_course_assignments", x => x.Id);
                    table.CheckConstraint("CK_user_course_assignments_CompletedAt", "(\"Status\" = 'Completed' AND \"CompletedAt\" IS NOT NULL) OR (\"Status\" <> 'Completed' AND \"CompletedAt\" IS NULL)");
                    table.CheckConstraint("CK_user_course_assignments_Timestamps", "\"StartedAt\" IS NULL OR \"StartedAt\" >= \"AssignedAt\"");
                    table.ForeignKey(
                        name: "FK_user_course_assignments_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_course_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempts_UserId_LessonId_InProgress",
                table: "lesson_attempts",
                columns: new[] { "UserId", "LessonId" },
                unique: true,
                filter: "\"Status\" = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "IX_user_course_assignments_CourseId",
                table: "user_course_assignments",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_user_course_assignments_UserId_Active",
                table: "user_course_assignments",
                column: "UserId",
                unique: true,
                filter: "\"Status\" IN ('Assigned', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_user_course_assignments_UserId_CourseId",
                table: "user_course_assignments",
                columns: new[] { "UserId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_course_assignments_UserId_Status_LastAccessedAt",
                table: "user_course_assignments",
                columns: new[] { "UserId", "Status", "LastAccessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_course_assignments");

            migrationBuilder.DropIndex(
                name: "IX_lesson_attempts_UserId_LessonId_InProgress",
                table: "lesson_attempts");

            migrationBuilder.DropColumn(
                name: "LastAccessedAt",
                table: "lesson_attempts");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_attempts_UserId_InProgress",
                table: "lesson_attempts",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'InProgress'");
        }
    }
}
