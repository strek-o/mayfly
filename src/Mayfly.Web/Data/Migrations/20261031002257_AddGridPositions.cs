using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mayfly.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGridPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GridColumn",
                table: "Folders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridRow",
                table: "Folders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridColumn",
                table: "Files",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridRow",
                table: "Files",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GridColumn",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "GridRow",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "GridColumn",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "GridRow",
                table: "Files");
        }
    }
}
