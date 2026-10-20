using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mayfly.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShareLinks_Pastes_PasteId",
                table: "ShareLinks");

            migrationBuilder.AlterColumn<Guid>(
                name: "PasteId",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "FileId",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_FileId",
                table: "ShareLinks",
                column: "FileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks",
                sql: "([PasteId] IS NOT NULL AND [FileId] IS NULL) OR ([PasteId] IS NULL AND [FileId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShareLinks_Thread",
                table: "ShareLinks",
                sql: "[ThreadId] IS NULL OR [PasteId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ShareLinks_Files_FileId",
                table: "ShareLinks",
                column: "FileId",
                principalTable: "Files",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShareLinks_Pastes_PasteId",
                table: "ShareLinks",
                column: "PasteId",
                principalTable: "Pastes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShareLinks_Files_FileId",
                table: "ShareLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_ShareLinks_Pastes_PasteId",
                table: "ShareLinks");

            migrationBuilder.DropIndex(
                name: "IX_ShareLinks_FileId",
                table: "ShareLinks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShareLinks_Target",
                table: "ShareLinks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShareLinks_Thread",
                table: "ShareLinks");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "ShareLinks");

            migrationBuilder.AlterColumn<Guid>(
                name: "PasteId",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShareLinks_Pastes_PasteId",
                table: "ShareLinks",
                column: "PasteId",
                principalTable: "Pastes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
