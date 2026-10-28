using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaskFlow.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SeedTaskPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RoleClaims",
                columns: new[] { "Id", "ClaimType", "ClaimValue", "CreatedAt", "CreatedBy", "RoleId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 6, "RolePolicy", "ViewTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 7, "RolePolicy", "CreateTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 8, "RolePolicy", "UpdateTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 9, "RolePolicy", "MoveTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 10, "RolePolicy", "AssignTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 11, "RolePolicy", "DeleteTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 12, "RolePolicy", "ReopenTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1006, "RolePolicy", "ViewTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1007, "RolePolicy", "CreateTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1008, "RolePolicy", "UpdateTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1009, "RolePolicy", "MoveTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1010, "RolePolicy", "AssignTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1011, "RolePolicy", "DeleteTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 1012, "RolePolicy", "ReopenTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 3L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 2001, "RolePolicy", "ViewTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 4L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 2002, "RolePolicy", "CreateTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 4L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 2003, "RolePolicy", "UpdateTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 4L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 2004, "RolePolicy", "MoveTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 4L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 2005, "RolePolicy", "AssignTasks", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 4L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1006);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1007);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1008);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1009);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1010);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1011);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 1012);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 2001);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 2002);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 2003);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 2004);

            migrationBuilder.DeleteData(
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: 2005);
        }
    }
}
