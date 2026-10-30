using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mayfly.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderParents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Folders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ParentId",
                table: "Folders",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Folders_ParentId",
                table: "Folders",
                column: "ParentId",
                principalTable: "Folders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Folders_Folders_ParentId",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Folders_ParentId",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Folders");
        }
    }
}
