using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurboTaxi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCityBasedTariffing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert Baku tariffs for Eco vehicle type
            migrationBuilder.InsertData(
                table: "Tariffs",
                columns: new[] { "Country", "City", "VehicleType", "BaseFare", "PricePerKm", "PricePerMinute", "MinimumFare", "IsActive" },
                values: new object[] { "AZ", "Baku", 0, 2.0m, 0.30m, 0.15m, 5.0m, true });

            // Insert Baku tariffs for Comfort vehicle type
            migrationBuilder.InsertData(
                table: "Tariffs",
                columns: new[] { "Country", "City", "VehicleType", "BaseFare", "PricePerKm", "PricePerMinute", "MinimumFare", "IsActive" },
                values: new object[] { "AZ", "Baku", 1, 3.0m, 0.40m, 0.20m, 7.0m, true });

            // Insert Baku tariffs for Premium vehicle type
            migrationBuilder.InsertData(
                table: "Tariffs",
                columns: new[] { "Country", "City", "VehicleType", "BaseFare", "PricePerKm", "PricePerMinute", "MinimumFare", "IsActive" },
                values: new object[] { "AZ", "Baku", 2, 5.0m, 0.60m, 0.30m, 10.0m, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete Baku Eco tariff
            migrationBuilder.DeleteData(
                table: "Tariffs",
                keyColumns: new[] { "Country", "City", "VehicleType" },
                keyValues: new object[] { "AZ", "Baku", 0 });

            // Delete Baku Comfort tariff
            migrationBuilder.DeleteData(
                table: "Tariffs",
                keyColumns: new[] { "Country", "City", "VehicleType" },
                keyValues: new object[] { "AZ", "Baku", 1 });

            // Delete Baku Premium tariff
            migrationBuilder.DeleteData(
                table: "Tariffs",
                keyColumns: new[] { "Country", "City", "VehicleType" },
                keyValues: new object[] { "AZ", "Baku", 2 });
        }
    }
}
