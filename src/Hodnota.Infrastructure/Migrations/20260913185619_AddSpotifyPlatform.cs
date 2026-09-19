using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hodnota.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpotifyPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Platforms",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "IconUrl", "IsActive", "Name", "Type", "UpdatedAtUtc", "WebsiteUrl" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), "spotify", new DateTimeOffset(new DateTime(2026, 9, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Spotify", "StreamingService", new DateTimeOffset(new DateTime(2026, 9, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Platforms",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"));
        }
    }
}
