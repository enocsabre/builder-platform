using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint10_ValidationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuntimeHealth",
                table: "Products",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "healthy");

            migrationBuilder.CreateTable(
                name: "ValidationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "running"),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Logs = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: true),
                    Errors = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AutofixAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    GatesPassed = table.Column<int>(type: "INTEGER", nullable: false),
                    GatesFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    GateResults = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationRuns_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValidationRuns_ProductId",
                table: "ValidationRuns",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValidationRuns");

            migrationBuilder.DropColumn(
                name: "RuntimeHealth",
                table: "Products");
        }
    }
}
