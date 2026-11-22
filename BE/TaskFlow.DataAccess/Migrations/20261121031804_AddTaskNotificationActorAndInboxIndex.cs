using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskNotificationActorAndInboxIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the replacement composite index (it also starts with
            // RecipientUserId, so it covers the existing
            // FK_TaskNotifications_Users_RecipientUserId) before dropping the
            // single-column index the FK relied on - MySQL/InnoDB refuses to
            // drop an FK's supporting index if no other index covers it,
            // even inside the same migration transaction.
            migrationBuilder.AddColumn<long>(
                name: "ActorUserId",
                table: "TaskNotifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskNotifications_ActorUserId",
                table: "TaskNotifications",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskNotifications_RecipientUserId_IsRead_CreatedAt_Id",
                table: "TaskNotifications",
                columns: new[] { "RecipientUserId", "IsRead", "CreatedAt", "Id" });

            migrationBuilder.DropIndex(
                name: "IX_TaskNotifications_RecipientUserId",
                table: "TaskNotifications");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskNotifications_Users_ActorUserId",
                table: "TaskNotifications",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskNotifications_Users_ActorUserId",
                table: "TaskNotifications");

            // Recreate the single-column index the pre-existing
            // FK_TaskNotifications_Users_RecipientUserId relies on before
            // dropping the composite index that was covering it, for the
            // same MySQL/InnoDB reason as in Up().
            migrationBuilder.CreateIndex(
                name: "IX_TaskNotifications_RecipientUserId",
                table: "TaskNotifications",
                column: "RecipientUserId");

            migrationBuilder.DropIndex(
                name: "IX_TaskNotifications_ActorUserId",
                table: "TaskNotifications");

            migrationBuilder.DropIndex(
                name: "IX_TaskNotifications_RecipientUserId_IsRead_CreatedAt_Id",
                table: "TaskNotifications");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "TaskNotifications");
        }
    }
}
