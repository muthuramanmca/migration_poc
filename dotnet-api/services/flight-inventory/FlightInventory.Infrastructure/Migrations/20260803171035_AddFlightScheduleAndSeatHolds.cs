using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightScheduleAndSeatHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AvailableSeats",
                table: "Flights",
                newName: "SeatCapacity");

            // Scaffolded as defaultValue: false (the CLR default), which would have backfilled every
            // existing row as soft-deleted and therefore invisible to every read path. A flight is
            // active unless explicitly deactivated -- matching Flight.Active's initializer.
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "Flights",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "Flights",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Fare",
                table: "Flights",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Flights",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Flights",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeatHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlightId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeatCount = table.Column<int>(type: "int", nullable: false),
                    HeldAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatHolds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_Active_DepartureAtUtc",
                table: "Flights",
                columns: new[] { "Active", "DepartureAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_FlightNumber",
                table: "Flights",
                column: "FlightNumber",
                unique: true,
                filter: "[Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SeatHolds_BookingId",
                table: "SeatHolds",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatHolds_BookingId_FlightId",
                table: "SeatHolds",
                columns: new[] { "BookingId", "FlightId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeatHolds");

            migrationBuilder.DropIndex(
                name: "IX_Flights_Active_DepartureAtUtc",
                table: "Flights");

            migrationBuilder.DropIndex(
                name: "IX_Flights_FlightNumber",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Fare",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Flights");

            migrationBuilder.RenameColumn(
                name: "SeatCapacity",
                table: "Flights",
                newName: "AvailableSeats");
        }
    }
}
