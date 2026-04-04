using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<string>(type: "TEXT", nullable: false),
                    facility_id = table.Column<string>(type: "TEXT", nullable: false),
                    product_id = table.Column<string>(type: "TEXT", nullable: false),
                    transaction_type = table.Column<int>(type: "INTEGER", nullable: false),
                    quantity_change = table.Column<int>(type: "INTEGER", nullable: false),
                    resulting_stock = table.Column<int>(type: "INTEGER", nullable: false),
                    unit_cost_amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    unit_cost_currency = table.Column<string>(type: "TEXT", nullable: false),
                    total_cost_amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    total_cost_currency = table.Column<string>(type: "TEXT", nullable: false),
                    new_sale_price_amount = table.Column<decimal>(type: "TEXT", nullable: true),
                    new_sale_price_currency = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_synced = table.Column<bool>(type: "INTEGER", nullable: false),
                    row_version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_inventory_performance_composite",
                table: "inventory_transactions",
                columns: new[] { "facility_id", "product_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_facility_id",
                table: "inventory_transactions",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_is_deleted",
                table: "inventory_transactions",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_is_synced",
                table: "inventory_transactions",
                column: "is_synced");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_tenant_id",
                table: "inventory_transactions",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_transactions");
        }
    }
}
