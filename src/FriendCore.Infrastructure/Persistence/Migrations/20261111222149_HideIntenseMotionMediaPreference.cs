using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HideIntenseMotionMediaPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // true, not EF's scaffolded false: existing rows take the column DEFAULT, and false
            // would silently disable the intense-motion warning for every account that already
            // exists. The entity initializer only covers rows built in code.
            migrationBuilder.AddColumn<bool>(
                name: "hide_intense_motion_media",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hide_intense_motion_media",
                table: "user_preferences");
        }
    }
}
