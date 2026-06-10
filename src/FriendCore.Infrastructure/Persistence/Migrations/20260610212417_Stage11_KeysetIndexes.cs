using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage11_KeysetIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_posts_author_id_created_at",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_posts_created_at",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_comments_post_id_created_at_deleted_at",
                table: "comments");

            migrationBuilder.CreateIndex(
                name: "IX_posts_author_id_created_at_id",
                table: "posts",
                columns: new[] { "author_id", "created_at", "id" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_posts_created_at_id",
                table: "posts",
                columns: new[] { "created_at", "id" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_comments_post_id_created_at_id",
                table: "comments",
                columns: new[] { "post_id", "created_at", "id" },
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_posts_author_id_created_at_id",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_posts_created_at_id",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_comments_post_id_created_at_id",
                table: "comments");

            migrationBuilder.CreateIndex(
                name: "IX_posts_author_id_created_at",
                table: "posts",
                columns: new[] { "author_id", "created_at" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_posts_created_at",
                table: "posts",
                column: "created_at",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_comments_post_id_created_at_deleted_at",
                table: "comments",
                columns: new[] { "post_id", "created_at", "deleted_at" },
                filter: "deleted_at IS NULL");
        }
    }
}
