using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hodnota.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotFoundRecheckIntervalDays = table.Column<int>(type: "integer", nullable: false),
                    FoundRecheckIntervalDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RefreshSettings",
                columns: new[] { "Id", "CreatedAtUtc", "FoundRecheckIntervalDays", "NotFoundRecheckIntervalDays", "UpdatedAtUtc" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000a001"), new DateTimeOffset(new DateTime(2026, 10, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 90, 7, new DateTimeOffset(new DateTime(2026, 10, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshSettings");
        }
    }
}
