using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_ArtifactSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArtifactId",
                table: "Approvals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArtifactId",
                table: "ActivityEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artifacts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_ArtifactId",
                table: "Approvals",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_ArtifactId",
                table: "ActivityEvents",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_ProductId_Type_Version",
                table: "Artifacts",
                columns: new[] { "ProductId", "Type", "Version" });

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityEvents_Artifacts_ArtifactId",
                table: "ActivityEvents",
                column: "ArtifactId",
                principalTable: "Artifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Approvals_Artifacts_ArtifactId",
                table: "Approvals",
                column: "ArtifactId",
                principalTable: "Artifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityEvents_Artifacts_ArtifactId",
                table: "ActivityEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Approvals_Artifacts_ArtifactId",
                table: "Approvals");

            migrationBuilder.DropTable(
                name: "Artifacts");

            migrationBuilder.DropIndex(
                name: "IX_Approvals_ArtifactId",
                table: "Approvals");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEvents_ArtifactId",
                table: "ActivityEvents");

            migrationBuilder.DropColumn(
                name: "ArtifactId",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "ArtifactId",
                table: "ActivityEvents");
        }
    }
}
