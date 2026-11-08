using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommentEditedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "edited_at",
                table: "comments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "edited_at",
                table: "comments");
        }
    }
}
