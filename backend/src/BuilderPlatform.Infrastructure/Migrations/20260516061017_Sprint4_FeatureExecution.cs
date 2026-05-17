using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_FeatureExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScaffoldChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "created"),
                    TargetPath = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    ModuleLabel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Layer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "backend"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScaffoldChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScaffoldChanges_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScaffoldChanges_ProductId",
                table: "ScaffoldChanges",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScaffoldChanges");
        }
    }
}
