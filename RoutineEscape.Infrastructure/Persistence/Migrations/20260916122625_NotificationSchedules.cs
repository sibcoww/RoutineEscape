using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoutineEscape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_explicit_time",
                table: "tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "notification_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intent = table.Column<string>(type: "text", nullable: false),
                    source_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    action_token = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<int>(type: "integer", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_schedules_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_schedules_action_token",
                table: "notification_schedules",
                column: "action_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_schedules_intent_record_id",
                table: "notification_schedules",
                columns: new[] { "intent", "record_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_schedules_next_attempt_at",
                table: "notification_schedules",
                column: "next_attempt_at");

            migrationBuilder.CreateIndex(
                name: "IX_notification_schedules_user_id",
                table: "notification_schedules",
                column: "user_id");

            // First activation deliberately excludes historical overdue records and ambiguous old task times.
            migrationBuilder.Sql("""
                INSERT INTO notification_schedules
                  (id, user_id, record_id, intent, source_at, due_at, next_attempt_at, action_token, stage, attempts)
                SELECT gen_random_uuid(), user_id, id, 'Reminder', trigger_at_utc, trigger_at_utc, trigger_at_utc, gen_random_uuid(), 0, 0
                FROM reminders WHERE status = 'Pending' AND trigger_at_utc > CURRENT_TIMESTAMP;
                INSERT INTO notification_schedules
                  (id, user_id, record_id, intent, source_at, due_at, next_attempt_at, action_token, stage, attempts)
                SELECT gen_random_uuid(), user_id, id, 'Event', start_utc, start_utc, start_utc, gen_random_uuid(), 0, 0
                FROM calendar_events WHERE start_utc > CURRENT_TIMESTAMP;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_schedules");

            migrationBuilder.DropColumn(
                name: "has_explicit_time",
                table: "tasks");
        }
    }
}
