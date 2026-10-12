using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaraTaraz.Modules.Ingestion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FTW2R3CheckpointState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "SyncCheckpoints",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "State",
                table: "SyncCheckpoints");
        }
    }
}
