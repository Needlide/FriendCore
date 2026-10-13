using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportSubjectSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "subject_author_id",
                table: "reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_author_username",
                table: "reports",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "subject_captured_at",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_content",
                table: "reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_kind",
                table: "reports",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "subject_author_id",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "subject_author_username",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "subject_captured_at",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "subject_content",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "subject_kind",
                table: "reports");
        }
    }
}
