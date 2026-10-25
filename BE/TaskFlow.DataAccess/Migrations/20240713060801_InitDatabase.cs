using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaskFlow.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Examples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateOfBirth = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Examples", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DisplayName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("RoleId", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedUserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SecurityStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("UserId", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("RoleClaimId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("UserClaimId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginProvider = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceGuid = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RefreshToken = table.Column<string>(type: "varchar(5000)", maxLength: 5000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RefreshTokenExpiryTime = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    LastLogin = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    Value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "CreatedAt", "CreatedBy", "DisplayName", "Name", "NormalizedName", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 1L, "005cf705-2e40-4e1c-8e73-afaec8ab2268", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, "System Admin", "SysAdmin", "SYSADMIN", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedBy", "Email", "EmailConfirmed", "IsDeleted", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UpdatedAt", "UpdatedBy", "UserName" },
                values: new object[] { 1L, 0, "616f1653-48e9-4a6f-81b3-1bdd52e565b5", new DateTimeOffset(new DateTime(2023, 10, 9, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, "admin@imt-soft.com", true, false, true, null, "ADMIN@IMT-SOFT.COM", "ADMIN", "AQAAAAEAACcQAAAAEELKNErj+EBVy3yZwAI32HSAQILEj5UAOooOEHTMPYU/yp0E28xNH1BjU/SEBw8kuA==", null, false, "ZY5BGSWBARTE74T6ZLO7WKKMMILBEB2E", false, new DateTimeOffset(new DateTime(2023, 10, 9, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, "admin" });

            migrationBuilder.InsertData(
                table: "RoleClaims",
                columns: new[] { "Id", "ClaimType", "ClaimValue", "CreatedAt", "CreatedBy", "RoleId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "RolePolicy", "ViewEmployees", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 2, "RolePolicy", "ManageEmployees", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 3, "RolePolicy", "ViewCustomers", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { 4, "RolePolicy", "ManageCustomers", new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 1L, 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L, new DateTimeOffset(new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(8363), new TimeSpan(0, 0, 0, 0, 0)), 1L });

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Examples");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
