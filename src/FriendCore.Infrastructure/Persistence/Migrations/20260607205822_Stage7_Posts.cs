using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage7_Posts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    author_id = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    sensitivity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    like_count = table.Column<int>(type: "integer", nullable: false),
                    comment_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_posts_author_id",
                table: "posts",
                column: "author_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "posts");
        }
    }
}
