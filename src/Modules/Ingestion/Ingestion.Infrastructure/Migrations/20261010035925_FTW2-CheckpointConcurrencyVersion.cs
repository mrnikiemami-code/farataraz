using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaraTaraz.Modules.Ingestion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FTW2CheckpointConcurrencyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "SyncCheckpoints",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "SyncCheckpoints");
        }
    }
}
