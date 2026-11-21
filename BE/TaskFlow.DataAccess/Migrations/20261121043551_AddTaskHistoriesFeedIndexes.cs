using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskHistoriesFeedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the composite index that covers
            // FK_TaskHistories_Users_ActorUserId before dropping the
            // single-column index it relied on - MySQL/InnoDB refuses to
            // drop an FK's supporting index with no replacement already in
            // place, same reasoning as AddTaskNotificationActorAndInboxIndex.
            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_ActorUserId_CreatedAt_Id",
                table: "TaskHistories",
                columns: new[] { "ActorUserId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_CreatedAt_Id",
                table: "TaskHistories",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.DropIndex(
                name: "IX_TaskHistories_ActorUserId",
                table: "TaskHistories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_ActorUserId",
                table: "TaskHistories",
                column: "ActorUserId");

            migrationBuilder.DropIndex(
                name: "IX_TaskHistories_ActorUserId_CreatedAt_Id",
                table: "TaskHistories");

            migrationBuilder.DropIndex(
                name: "IX_TaskHistories_CreatedAt_Id",
                table: "TaskHistories");
        }
    }
}
