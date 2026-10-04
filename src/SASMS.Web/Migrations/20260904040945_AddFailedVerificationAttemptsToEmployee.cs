using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SASMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedVerificationAttemptsToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AttemptsResetDate",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedCheckInAttempts",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FailedCheckOutAttempts",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptsResetDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FailedCheckInAttempts",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FailedCheckOutAttempts",
                table: "Employees");
        }
    }
}
