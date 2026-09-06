using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hodnota.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharePage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharePages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharePages", x => x.Id);
                    table.CheckConstraint("CK_SharePage_ExactlyOneTarget", "(CASE WHEN \"ArtistId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"ReleaseId\" IS NOT NULL THEN 1 ELSE 0 END) +\n(CASE WHEN \"TrackId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_SharePages_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharePages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SharePages_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharePages_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharePageLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharePageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharePageLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharePageLinks_ProviderLinks_ProviderLinkId",
                        column: x => x.ProviderLinkId,
                        principalTable: "ProviderLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharePageLinks_SharePages_SharePageId",
                        column: x => x.SharePageId,
                        principalTable: "SharePages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharePageLinks_ProviderLinkId",
                table: "SharePageLinks",
                column: "ProviderLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_SharePageLinks_SharePageId_ProviderLinkId",
                table: "SharePageLinks",
                columns: new[] { "SharePageId", "ProviderLinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharePages_ArtistId",
                table: "SharePages",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_SharePages_ReleaseId",
                table: "SharePages",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SharePages_TrackId",
                table: "SharePages",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_SharePages_UserId",
                table: "SharePages",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharePageLinks");

            migrationBuilder.DropTable(
                name: "SharePages");
        }
    }
}
