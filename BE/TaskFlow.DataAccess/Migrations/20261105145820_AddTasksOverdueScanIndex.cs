using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTasksOverdueScanIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tasks_IsDeleted_IsOverdue_Status_DueDate",
                table: "Tasks",
                columns: new[] { "IsDeleted", "IsOverdue", "Status", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_IsDeleted_IsOverdue_Status_DueDate",
                table: "Tasks");
        }
    }
}
