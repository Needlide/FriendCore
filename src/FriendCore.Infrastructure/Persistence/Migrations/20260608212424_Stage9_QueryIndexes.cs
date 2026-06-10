using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage9_QueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tags_slug",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "IX_posts_author_id",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_follows_followed_id",
                table: "follows");

            migrationBuilder.DropIndex(
                name: "IX_comments_post_id",
                table: "comments");

            migrationBuilder.CreateIndex(
                name: "IX_tags_slug",
                table: "tags",
                column: "slug",
                unique: true)
                .Annotation("Npgsql:IndexOperators", new[] { "text_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_tags_status_last_used_at",
                table: "tags",
                columns: new[] { "status", "last_used_at" });

            migrationBuilder.CreateIndex(
                name: "IX_posts_author_id",
                table: "posts",
                column: "author_id",
                filter: "deleted_at IS NULL");

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
                name: "IX_follows_followed_id_created_at",
                table: "follows",
                columns: new[] { "followed_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_follows_follower_id_created_at",
                table: "follows",
                columns: new[] { "follower_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_post_id",
                table: "comments",
                column: "post_id",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_comments_post_id_created_at_deleted_at",
                table: "comments",
                columns: new[] { "post_id", "created_at", "deleted_at" },
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tags_slug",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "IX_tags_status_last_used_at",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "IX_posts_author_id",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_posts_author_id_created_at",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_posts_created_at",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_follows_followed_id_created_at",
                table: "follows");

            migrationBuilder.DropIndex(
                name: "IX_follows_follower_id_created_at",
                table: "follows");

            migrationBuilder.DropIndex(
                name: "IX_comments_post_id",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "IX_comments_post_id_created_at_deleted_at",
                table: "comments");

            migrationBuilder.CreateIndex(
                name: "IX_tags_slug",
                table: "tags",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_posts_author_id",
                table: "posts",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_follows_followed_id",
                table: "follows",
                column: "followed_id");

            migrationBuilder.CreateIndex(
                name: "IX_comments_post_id",
                table: "comments",
                column: "post_id");
        }
    }
}
