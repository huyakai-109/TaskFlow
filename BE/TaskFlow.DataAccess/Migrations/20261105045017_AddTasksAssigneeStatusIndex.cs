using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTasksAssigneeStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tasks_IsDeleted_AssigneeId_Status",
                table: "Tasks",
                columns: new[] { "IsDeleted", "AssigneeId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_IsDeleted_AssigneeId_Status",
                table: "Tasks");
        }
    }
}
