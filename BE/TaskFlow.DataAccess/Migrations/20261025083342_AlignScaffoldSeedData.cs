using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AlignScaffoldSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Examples",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1,
                column: "ClaimValue",
                value: "ViewMembers");

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 2,
                column: "ClaimValue",
                value: "CreateMembers");

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 3,
                column: "ClaimValue",
                value: "DeleteMembers");

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 4,
                column: "ClaimValue",
                value: "UpdateMembers");

            migrationBuilder.InsertData(
                table: "RoleClaims",
                columns: new[] { "Id", "ClaimType", "ClaimValue", "CreatedAt", "CreatedBy", "RoleId", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 5, "RolePolicy", "ManageReports", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "DisplayName", "Name", "NormalizedName" },
                values: new object[] { "Admin", "Admin", "ADMIN" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Examples");

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1,
                column: "ClaimValue",
                value: "ViewEmployees");

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 2,
                column: "ClaimValue",
                value: "ManageEmployees");

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 3,
                column: "ClaimValue",
                value: "ViewCustomers");

            migrationBuilder.UpdateData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 4,
                column: "ClaimValue",
                value: "ManageCustomers");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "DisplayName", "Name", "NormalizedName" },
                values: new object[] { "System Admin", "SysAdmin", "SYSADMIN" });
        }
    }
}
