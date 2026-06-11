using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage12_MediaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    uploader_id = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    has_flashing_warning = table.Column<bool>(type: "boolean", nullable: false),
                    has_intense_motion_warning = table.Column<bool>(type: "boolean", nullable: false),
                    safety_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    safety_flagged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "post_media",
                columns: table => new
                {
                    post_id = table.Column<string>(type: "text", nullable: false),
                    media_item_id = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_media", x => new { x.post_id, x.media_item_id });
                });

            migrationBuilder.CreateTable(
                name: "media_variants",
                columns: table => new
                {
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    media_item_id = table.Column<string>(type: "text", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    width_px = table.Column<int>(type: "integer", nullable: false),
                    height_px = table.Column<int>(type: "integer", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_variants", x => new { x.media_item_id, x.type });
                    table.ForeignKey(
                        name: "FK_media_variants_media_items_media_item_id",
                        column: x => x.media_item_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_media_items_uploader_id",
                table: "media_items",
                column: "uploader_id",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_post_media_media_item_id",
                table: "post_media",
                column: "media_item_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_variants");

            migrationBuilder.DropTable(
                name: "post_media");

            migrationBuilder.DropTable(
                name: "media_items");
        }
    }
}
