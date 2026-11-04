using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HideArachnidMediaPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // true, not EF's scaffolded false, for the same reason as
            // send_message_on_enter: the entity initializer only covers rows built in code,
            // while existing rows take the column DEFAULT. A false here would silently turn
            // off the arachnophobia warning for every account that already exists.
            migrationBuilder.AddColumn<bool>(
                name: "hide_arachnid_media",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hide_arachnid_media",
                table: "user_preferences");
        }
    }
}
