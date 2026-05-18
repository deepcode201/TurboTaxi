using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TurboTaxi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBakuTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var utcNow = new DateTime(2026, 5, 18, 7, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "Tariffs",
                columns: new[] { "Country", "City", "VehicleType", "BaseFare", "PricePerKm", "PricePerMinute", "MinimumFare", "IsActive", "CreatedTime" },
                values: new object[,]
                {
                    { "AZ", "Baku", 0, 2.0m, 0.30m, 0.15m, 5.0m, true, utcNow },
                    { "AZ", "Baku", 1, 3.0m, 0.40m, 0.20m, 7.0m, true, utcNow },
                    { "AZ", "Baku", 2, 5.0m, 0.60m, 0.30m, 10.0m, true, utcNow }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Tariffs",
                keyColumns: new[] { "Country", "City", "VehicleType" },
                keyValues: new object[] { "AZ", "Baku", 0 });

            migrationBuilder.DeleteData(
                table: "Tariffs",
                keyColumns: new[] { "Country", "City", "VehicleType" },
                keyValues: new object[] { "AZ", "Baku", 1 });

            migrationBuilder.DeleteData(
                table: "Tariffs",
                keyColumns: new[] { "Country", "City", "VehicleType" },
                keyValues: new object[] { "AZ", "Baku", 2 });
        }
    }
}

