using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint26_RefactorRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefactorRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "medium"),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    Impact = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Risk = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "pending"),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefactorRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefactorRecommendations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefactorRecommendations_ProductId",
                table: "RefactorRecommendations",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefactorRecommendations");
        }
    }
}
