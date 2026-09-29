using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "messages",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000);

            migrationBuilder.AddColumn<string>(
                name: "media_item_id",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_media_item_id",
                table: "messages",
                column: "media_item_id",
                filter: "media_item_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_messages_media_item_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "media_item_id",
                table: "messages");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "messages",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000,
                oldNullable: true);
        }
    }
}
