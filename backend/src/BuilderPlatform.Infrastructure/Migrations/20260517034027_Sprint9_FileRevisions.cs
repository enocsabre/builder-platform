using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint9_FileRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    PatchType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    BeforeContent = table.Column<string>(type: "TEXT", nullable: true),
                    AfterContent = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileRevisions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileRevisions_ProductId",
                table: "FileRevisions",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileRevisions");
        }
    }
}
