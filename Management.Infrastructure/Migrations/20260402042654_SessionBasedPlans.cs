using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionBasedPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 'price' column already exists in 'salon_services' table in SQLite.
            // EF generated this because the snapshot was out of sync. Removing to fix Startup crash.

            migrationBuilder.AddColumn<int>(
                name: "base_session_count",
                table: "membership_plans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "idx_sale_member_id",
                table: "sales",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "idx_member_created_at",
                table: "members",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_sale_member_id",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "idx_member_created_at",
                table: "members");

            // DropColumn for 'price' in 'salon_services' removed.

            migrationBuilder.DropColumn(
                name: "base_session_count",
                table: "membership_plans");
        }
    }
}
