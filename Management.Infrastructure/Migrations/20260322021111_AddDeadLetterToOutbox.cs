using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadLetterToOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_dead_letter",
                table: "outbox_messages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_StockNonNegative",
                table: "products",
                sql: "stock_quantity >= 0");

            migrationBuilder.CreateIndex(
                name: "idx_payroll_staff_id",
                table: "payroll_entries",
                column: "staff_member_id");

            migrationBuilder.AddForeignKey(
                name: "fk_payroll_entries_staff_members_staff_member_id",
                table: "payroll_entries",
                column: "staff_member_id",
                principalTable: "staff_members",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payroll_entries_staff_members_staff_member_id",
                table: "payroll_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_StockNonNegative",
                table: "products");

            migrationBuilder.DropIndex(
                name: "idx_payroll_staff_id",
                table: "payroll_entries");

            migrationBuilder.DropColumn(
                name: "is_dead_letter",
                table: "outbox_messages");
        }
    }
}
