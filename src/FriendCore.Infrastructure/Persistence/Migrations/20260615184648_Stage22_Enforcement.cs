using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage22_Enforcement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suspended_until",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "admin_audit_log",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    moderator_id = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_id = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    related_report_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_log_created_at",
                table: "admin_audit_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_log_target_type_target_id_created_at",
                table: "admin_audit_log",
                columns: new[] { "target_type", "target_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_audit_log");

            migrationBuilder.DropColumn(
                name: "suspended_until",
                table: "users");
        }
    }
}
