using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_staff_performance_composite",
                table: "staff_members",
                columns: new[] { "facility_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "idx_sale_performance_composite",
                table: "sales",
                columns: new[] { "facility_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_order_performance_composite",
                table: "restaurant_orders",
                columns: new[] { "facility_id", "created_at", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_member_performance_composite",
                table: "members",
                columns: new[] { "facility_id", "status", "expiration_date" });

            migrationBuilder.CreateIndex(
                name: "idx_appointment_performance_composite",
                table: "appointments",
                columns: new[] { "facility_id", "start_time", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_access_event_performance_composite",
                table: "access_events",
                columns: new[] { "facility_id", "timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_staff_performance_composite",
                table: "staff_members");

            migrationBuilder.DropIndex(
                name: "idx_sale_performance_composite",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "idx_order_performance_composite",
                table: "restaurant_orders");

            migrationBuilder.DropIndex(
                name: "idx_member_performance_composite",
                table: "members");

            migrationBuilder.DropIndex(
                name: "idx_appointment_performance_composite",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "idx_access_event_performance_composite",
                table: "access_events");
        }
    }
}
