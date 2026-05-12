using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMembershipPlanWeeklyLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NEW CHANGES FOR THIS TASK
            migrationBuilder.RenameColumn(
                name: "is_session_pack",
                table: "membership_plans",
                newName: "sessions_per_week");

            migrationBuilder.DropColumn(
                name: "base_session_count",
                table: "membership_plans");

            // OTHER COLUMNS DETECTED BY EF AS MISSING BUT ALREADY EXIST IN DB (SKIP)
            /*
            migrationBuilder.AddColumn<string>(
                name: "light_palette",
                table: "gym_settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
            */
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "sessions_per_week",
                table: "membership_plans",
                newName: "is_session_pack");

            migrationBuilder.AddColumn<int>(
                name: "base_session_count",
                table: "membership_plans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
