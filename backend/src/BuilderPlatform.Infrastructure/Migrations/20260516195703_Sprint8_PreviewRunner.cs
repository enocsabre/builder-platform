using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuilderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint8_PreviewRunner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviewError",
                table: "Products",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviewLastStartedAt",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviewPort",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviewStatus",
                table: "Products",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "stopped");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewError",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PreviewLastStartedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PreviewPort",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PreviewStatus",
                table: "Products");
        }
    }
}
