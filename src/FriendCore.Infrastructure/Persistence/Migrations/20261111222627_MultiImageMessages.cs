using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiImageMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "message_media",
                columns: table => new
                {
                    message_id = table.Column<string>(type: "text", nullable: false),
                    media_item_id = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_media", x => new { x.message_id, x.media_item_id });
                    table.ForeignKey(
                        name: "FK_message_media_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Preserve every existing single-image message before the column is dropped. All old
            // attachments were display order 0 by definition.
            migrationBuilder.Sql(
                """
                INSERT INTO message_media (message_id, media_item_id, display_order)
                SELECT id, media_item_id, 0
                FROM messages
                WHERE media_item_id IS NOT NULL;
                """);

            // Created after the backfill: an old deployment could in principle hold the same
            // upload twice, and the unique index would make the migration fail rather than let
            // the pre-existing bad row block every future write.
            migrationBuilder.CreateIndex(
                name: "IX_message_media_media_item_id",
                table: "message_media",
                column: "media_item_id",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_messages_media_item_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "media_item_id",
                table: "messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "media_item_id",
                table: "messages",
                type: "text",
                nullable: true);

            // The old schema could only represent one image, so a rollback keeps the first one in
            // display order and necessarily loses the rest.
            migrationBuilder.Sql(
                """
                UPDATE messages m
                SET media_item_id = (
                    SELECT mm.media_item_id
                    FROM message_media mm
                    WHERE mm.message_id = m.id
                    ORDER BY mm.display_order
                    LIMIT 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_messages_media_item_id",
                table: "messages",
                column: "media_item_id",
                filter: "media_item_id IS NOT NULL");

            migrationBuilder.DropTable(
                name: "message_media");
        }
    }
}
