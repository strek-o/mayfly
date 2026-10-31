using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mayfly.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDesktopShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks");

            migrationBuilder.AddColumn<string>(
                name: "DesktopOwnerId",
                table: "ShareLinks",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_DesktopOwnerId",
                table: "ShareLinks",
                column: "DesktopOwnerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks",
                sql: "([PasteId] IS NOT NULL AND [FileId] IS NULL AND [FolderId] IS NULL AND [DesktopOwnerId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NOT NULL AND [FolderId] IS NULL AND [DesktopOwnerId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NULL AND [FolderId] IS NOT NULL AND [DesktopOwnerId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NULL AND [FolderId] IS NULL AND [DesktopOwnerId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ShareLinks_AspNetUsers_DesktopOwnerId",
                table: "ShareLinks",
                column: "DesktopOwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShareLinks_AspNetUsers_DesktopOwnerId",
                table: "ShareLinks");

            migrationBuilder.DropIndex(
                name: "IX_ShareLinks_DesktopOwnerId",
                table: "ShareLinks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks");

            migrationBuilder.DropColumn(
                name: "DesktopOwnerId",
                table: "ShareLinks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks",
                sql: "([PasteId] IS NOT NULL AND [FileId] IS NULL AND [FolderId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NOT NULL AND [FolderId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NULL AND [FolderId] IS NOT NULL)");
        }
    }
}
