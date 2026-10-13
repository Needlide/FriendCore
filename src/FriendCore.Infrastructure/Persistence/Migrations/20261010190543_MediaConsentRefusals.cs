using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MediaConsentRefusals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_refusals",
                columns: table => new
                {
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recipient_id = table.Column<string>(type: "text", nullable: false),
                    sender_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_refusals", x => new { x.recipient_id, x.sender_id, x.content_hash });
                });

            migrationBuilder.CreateIndex(
                name: "IX_media_refusals_sender_id_created_at",
                table: "media_refusals",
                columns: new[] { "sender_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_refusals");
        }
    }
}
