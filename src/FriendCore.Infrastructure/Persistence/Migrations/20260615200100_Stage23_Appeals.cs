using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage23_Appeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appeals",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    audit_log_id = table.Column<string>(type: "text", nullable: false),
                    appellant_id = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_moderator_id = table.Column<string>(type: "text", nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appeals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appeals_audit_log_id",
                table: "appeals",
                column: "audit_log_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appeals_status_deadline",
                table: "appeals",
                columns: new[] { "status", "deadline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appeals");
        }
    }
}
