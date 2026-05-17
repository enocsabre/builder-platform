using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1_RuntimeEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProcessing",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RuntimePhase",
                table: "Products",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "idle");

            migrationBuilder.AddColumn<double>(
                name: "Confidence",
                table: "ChatMessages",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedIntent",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductMemories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMemories_ProductId_Key",
                table: "ProductMemories",
                columns: new[] { "ProductId", "Key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductMemories");

            migrationBuilder.DropColumn(
                name: "IsProcessing",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RuntimePhase",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DetectedIntent",
                table: "ChatMessages");
        }
    }
}
