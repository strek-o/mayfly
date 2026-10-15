using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mayfly.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddThreadsAndEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Pastes",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Threads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PasteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Threads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Threads_Pastes_PasteId",
                        column: x => x.PasteId,
                        principalTable: "Pastes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entries_Threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "Threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pastes_OwnerId",
                table: "Pastes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_ThreadId",
                table: "Entries",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_PasteId",
                table: "Threads",
                column: "PasteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pastes_AspNetUsers_OwnerId",
                table: "Pastes",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pastes_AspNetUsers_OwnerId",
                table: "Pastes");

            migrationBuilder.DropTable(
                name: "Entries");

            migrationBuilder.DropTable(
                name: "Threads");

            migrationBuilder.DropIndex(
                name: "IX_Pastes_OwnerId",
                table: "Pastes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Pastes");
        }
    }
}
