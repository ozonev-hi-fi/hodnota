using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hodnota.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    CheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderChecks", x => x.Id);
                    table.CheckConstraint("CK_ProviderCheck_ExactlyOneTarget", "(CASE WHEN \"ArtistId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"ReleaseId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"TrackId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_ProviderChecks_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderChecks_Platforms_PlatformId",
                        column: x => x.PlatformId,
                        principalTable: "Platforms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProviderChecks_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderChecks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderChecks_ArtistId",
                table: "ProviderChecks",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderChecks_PlatformId_ArtistId",
                table: "ProviderChecks",
                columns: new[] { "PlatformId", "ArtistId" },
                unique: true,
                filter: "\"ArtistId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderChecks_PlatformId_ReleaseId",
                table: "ProviderChecks",
                columns: new[] { "PlatformId", "ReleaseId" },
                unique: true,
                filter: "\"ReleaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderChecks_PlatformId_TrackId",
                table: "ProviderChecks",
                columns: new[] { "PlatformId", "TrackId" },
                unique: true,
                filter: "\"TrackId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderChecks_ReleaseId",
                table: "ProviderChecks",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderChecks_TrackId",
                table: "ProviderChecks",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderChecks");
        }
    }
}
