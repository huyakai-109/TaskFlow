using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaskFlow.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SeedManagerAndMemberRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "CreatedAt", "CreatedBy", "DisplayName", "Name", "NormalizedName", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 3L, "7b0e5f3a-4c1d-4a8e-9b62-3d5f1c8a2e41", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, "Manager", "Manager", "MANAGER", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 4L, "c94a2d17-6e83-4f05-a1b9-8e7d03f5b6c2", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, "Member", "Member", "MEMBER", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L }
                });

            migrationBuilder.InsertData(
                table: "RoleClaims",
                columns: new[] { "Id", "ClaimType", "ClaimValue", "CreatedAt", "CreatedBy", "RoleId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1001, "RolePolicy", "ViewMembers", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1002, "RolePolicy", "CreateMembers", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1003, "RolePolicy", "DeleteMembers", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1004, "RolePolicy", "UpdateMembers", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1005, "RolePolicy", "ManageReports", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1004);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1005);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3L);
        }
    }
}
