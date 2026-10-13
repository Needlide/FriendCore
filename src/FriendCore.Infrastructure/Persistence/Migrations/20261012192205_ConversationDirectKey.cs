using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConversationDirectKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "direct_key",
                table: "conversations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversations_direct_key",
                table: "conversations",
                column: "direct_key",
                unique: true,
                filter: "direct_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversations_direct_key",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "direct_key",
                table: "conversations");
        }
    }
}
