using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MinimumLogLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Id);
                    table.CheckConstraint("CK_system_settings_MinimumLogLevel", "\"MinimumLogLevel\" IN ('Debug', 'Information', 'Warning', 'Error', 'Fatal')");
                    table.CheckConstraint("CK_system_settings_Singleton", "\"Id\" = '389cd8b7-6f49-4c8f-bdf8-7bcae005b3cc'::uuid");
                    table.ForeignKey(
                        name: "FK_system_settings_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "Id", "MinimumLogLevel", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[] { new Guid("389cd8b7-6f49-4c8f-bdf8-7bcae005b3cc"), "Information", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_UpdatedByUserId",
                table: "system_settings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings");
        }
    }
}
