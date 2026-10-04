using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SASMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveTypeAndAttachmentToLeaveApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentOriginalFileName",
                table: "LeaveApplications",
                type: "varchar(260)",
                maxLength: 260,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentStoredFileName",
                table: "LeaveApplications",
                type: "varchar(260)",
                maxLength: 260,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // EF's scaffolded default was an empty string, which isn't a valid LeaveType enum
            // value — existing rows would fail to deserialize. Backfilled with "Other" instead
            // so every pre-existing LeaveApplication row stays valid after this migration.
            migrationBuilder.AddColumn<string>(
                name: "LeaveType",
                table: "LeaveApplications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Other")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentOriginalFileName",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "AttachmentStoredFileName",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "LeaveType",
                table: "LeaveApplications");
        }
    }
}
