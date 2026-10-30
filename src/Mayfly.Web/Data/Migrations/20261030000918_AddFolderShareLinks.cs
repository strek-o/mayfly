using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mayfly.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks");

            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_FolderId",
                table: "ShareLinks",
                column: "FolderId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks",
                sql: "([PasteId] IS NOT NULL AND [FileId] IS NULL AND [FolderId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NOT NULL AND [FolderId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NULL AND [FolderId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ShareLinks_Folders_FolderId",
                table: "ShareLinks",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShareLinks_Folders_FolderId",
                table: "ShareLinks");

            migrationBuilder.DropIndex(
                name: "IX_ShareLinks_FolderId",
                table: "ShareLinks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "ShareLinks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks",
                sql: "([PasteId] IS NOT NULL AND [FileId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NOT NULL)");
        }
    }
}
