using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5_ProjectAwareness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductModules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModuleName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RoutePath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ControllerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Layer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "full-stack"),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "scaffold"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductModules_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductModules_ProductId",
                table: "ProductModules",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductModules");
        }
    }
}
