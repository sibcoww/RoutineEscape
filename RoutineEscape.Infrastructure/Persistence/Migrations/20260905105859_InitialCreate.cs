using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoutineEscape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    telegram_user_id = table.Column<long>(type: "bigint", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    first_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    last_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    time_zone_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    telegram_user_id = table.Column<long>(type: "bigint", nullable: true),
                    telegram_chat_id = table.Column<long>(type: "bigint", nullable: true),
                    telegram_message_id = table.Column<long>(type: "bigint", nullable: true),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    chat_title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    author_signature = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    original_message_date_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_hidden_user = table.Column<bool>(type: "boolean", nullable: false),
                    raw_metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    telegram_message_id = table.Column<long>(type: "bigint", nullable: false),
                    intent = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drafts", x => x.id);
                    table.ForeignKey(
                        name: "FK_drafts_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_calendar_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_calendar_events_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calendar_events_message_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "message_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_notes_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notes_message_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "message_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    trigger_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    triggered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminders", x => x.id);
                    table.ForeignKey(
                        name: "FK_reminders_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reminders_message_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "message_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    deadline_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_tasks_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tasks_message_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "message_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_users_telegram_user_id",
                table: "app_users",
                column: "telegram_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_events_source_id",
                table: "calendar_events",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_events_user_id_start_utc",
                table: "calendar_events",
                columns: new[] { "user_id", "start_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_drafts_status_expires_at",
                table: "drafts",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_drafts_user_id_telegram_message_id",
                table: "drafts",
                columns: new[] { "user_id", "telegram_message_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notes_source_id",
                table: "notes",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_notes_user_id",
                table: "notes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_source_id",
                table: "reminders",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_status_trigger_at_utc",
                table: "reminders",
                columns: new[] { "status", "trigger_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_reminders_user_id",
                table: "reminders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_deadline_utc",
                table: "tasks",
                column: "deadline_utc");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_source_id",
                table: "tasks",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_user_id_status",
                table: "tasks",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_events");

            migrationBuilder.DropTable(
                name: "drafts");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "reminders");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "message_sources");
        }
    }
}
