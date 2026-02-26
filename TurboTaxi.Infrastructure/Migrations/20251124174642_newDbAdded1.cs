using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurboTaxi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class newDbAdded1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastKnownLatitude",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "LastKnownLongitude",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "LastLocationUpdateTime",
                table: "Drivers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LastKnownLatitude",
                table: "Drivers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastKnownLongitude",
                table: "Drivers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationUpdateTime",
                table: "Drivers",
                type: "datetime2",
                nullable: true);
        }
    }
}
