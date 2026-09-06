using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hodnota.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderLinkExternalIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProviderLinks_PlatformId_ExternalId",
                table: "ProviderLinks",
                columns: new[] { "PlatformId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProviderLinks_PlatformId_ExternalId",
                table: "ProviderLinks");
        }
    }
}
