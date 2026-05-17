using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_ScaffoldEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectPath",
                table: "Products",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScaffoldStatus",
                table: "Products",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.CreateTable(
                name: "ScaffoldEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EntryType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "file"),
                    Language = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScaffoldEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScaffoldEntries_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScaffoldEntries_ProductId",
                table: "ScaffoldEntries",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScaffoldEntries");

            migrationBuilder.DropColumn(
                name: "ProjectPath",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ScaffoldStatus",
                table: "Products");
        }
    }
}
