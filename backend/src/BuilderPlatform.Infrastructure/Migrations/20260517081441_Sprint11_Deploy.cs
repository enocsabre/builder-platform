using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint11_Deploy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeployBranch",
                table: "Products",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeployCommitHash",
                table: "Products",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeployLogs",
                table: "Products",
                type: "TEXT",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeployStatus",
                table: "Products",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "not_deployed");

            migrationBuilder.AddColumn<string>(
                name: "DeployUrl",
                table: "Products",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeployedAt",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulDeployAt",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeployRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "running"),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Logs = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    Errors = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DeployUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CommitHash = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GateResults = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeployRuns_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeployRuns_ProductId",
                table: "DeployRuns",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeployRuns");

            migrationBuilder.DropColumn(
                name: "DeployBranch",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeployCommitHash",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeployLogs",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeployStatus",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeployUrl",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeployedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulDeployAt",
                table: "Products");
        }
    }
}
