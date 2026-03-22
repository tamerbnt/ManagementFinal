using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScanDirectionToAccessEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "direction",
                table: "access_events",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "direction",
                table: "access_events");
        }
    }
}
