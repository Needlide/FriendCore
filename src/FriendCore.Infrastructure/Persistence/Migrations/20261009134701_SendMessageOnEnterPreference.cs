using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SendMessageOnEnterPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // true, not EF's scaffolded false, and deliberately hand-edited. The entity's
            // `= true` initializer only covers rows built in code; adding the column to a
            // populated table takes the DEFAULT, so false here would silently move every
            // existing user from Enter-to-send to Ctrl+Enter on upgrade, with nothing in the
            // UI having asked them. That is the one migration shape in this project where the
            // domain default has to be restated in SQL.
            migrationBuilder.AddColumn<bool>(
                name: "send_message_on_enter",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "send_message_on_enter",
                table: "user_preferences");
        }
    }
}
