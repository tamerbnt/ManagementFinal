using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_member_card_id",
                table: "members");

            migrationBuilder.CreateIndex(
                name: "idx_member_card_id_unique",
                table: "members",
                column: "card_id",
                unique: true,
                filter: "[card_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_member_email_unique",
                table: "members",
                column: "email",
                unique: true,
                filter: "[email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_member_phone_unique",
                table: "members",
                column: "phone_number",
                unique: true,
                filter: "[phone_number] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_member_card_id_unique",
                table: "members");

            migrationBuilder.DropIndex(
                name: "idx_member_email_unique",
                table: "members");

            migrationBuilder.DropIndex(
                name: "idx_member_phone_unique",
                table: "members");

            migrationBuilder.CreateIndex(
                name: "idx_member_card_id",
                table: "members",
                column: "card_id");
        }
    }
}
