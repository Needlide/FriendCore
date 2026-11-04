using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MediaSafetyFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One JSONB dictionary replaces the two hard-coded boolean columns. Add first, so the
            // existing rows can be carried over instead of losing their declarations.
            migrationBuilder.AddColumn<string>(
                name: "safety_flags",
                table: "media_items",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.Sql(
                """
                UPDATE media_items
                SET safety_flags = jsonb_strip_nulls(jsonb_build_object(
                    'flashing', CASE WHEN has_flashing_warning THEN true END,
                    'intenseMotion', CASE WHEN has_intense_motion_warning THEN true END))
                WHERE has_flashing_warning OR has_intense_motion_warning;
                """);

            migrationBuilder.DropColumn(
                name: "has_flashing_warning",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "has_intense_motion_warning",
                table: "media_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_flashing_warning",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_intense_motion_warning",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Flags added after this migration cannot round-trip through the old two columns;
            // the two that predate it can, so restore those.
            migrationBuilder.Sql(
                """
                UPDATE media_items
                SET has_flashing_warning = COALESCE((safety_flags ->> 'flashing')::boolean, false),
                    has_intense_motion_warning = COALESCE((safety_flags ->> 'intenseMotion')::boolean, false);
                """);

            migrationBuilder.DropColumn(
                name: "safety_flags",
                table: "media_items");
        }
    }
}
