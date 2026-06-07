using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddParentReplyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    chat_room_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sender_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chat_room_users",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    chat_room_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_room_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chat_rooms",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    matched_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    match_score = table.Column<int>(type: "integer", nullable: false),
                    match_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    shared_tags = table.Column<string[]>(type: "text[]", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    icebreaker = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    user_liked = table.Column<bool>(type: "boolean", nullable: false),
                    matched_user_liked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rant_posts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nickname = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    content = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    emotion_tags = table.Column<string[]>(type: "text[]", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    report_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rant_posts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rant_reactions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rant_post_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reaction_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rant_reactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rant_replies",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rant_post_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nickname = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    parent_reply_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rant_replies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rant_reports",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rant_post_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rant_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "registration_verification_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nickname = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    birth_year = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    gender = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    code_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    terms_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_verification_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_participants",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    task_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_participants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    duration = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    participant_limit = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "today_entries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    event_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    emotion_tags = table.Column<string[]>(type: "text[]", nullable: false),
                    value_tags = table.Column<string[]>(type: "text[]", nullable: false),
                    interest_tags = table.Column<string[]>(type: "text[]", nullable: false),
                    response_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_today_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nickname = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    age_range = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    gender = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    location_area = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    interest_tags = table.Column<string[]>(type: "text[]", nullable: false),
                    value_tags = table.Column<string[]>(type: "text[]", nullable: false),
                    response_preference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_accounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    username = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_auth_accounts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auth_accounts_user_id",
                table: "auth_accounts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_auth_accounts_username",
                table: "auth_accounts",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_chat_room_id_created_at",
                table: "chat_messages",
                columns: new[] { "chat_room_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_room_users_chat_room_id_user_id",
                table: "chat_room_users",
                columns: new[] { "chat_room_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_source_type_source_id",
                table: "chat_rooms",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_matches_user_id_match_score",
                table: "matches",
                columns: new[] { "user_id", "match_score" });

            migrationBuilder.CreateIndex(
                name: "IX_matches_user_id_matched_user_id",
                table: "matches",
                columns: new[] { "user_id", "matched_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id_expires_at",
                table: "password_reset_tokens",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_rant_posts_is_hidden_created_at",
                table: "rant_posts",
                columns: new[] { "is_hidden", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_rant_reactions_rant_post_id_user_id_reaction_type",
                table: "rant_reactions",
                columns: new[] { "rant_post_id", "user_id", "reaction_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rant_replies_rant_post_id_created_at",
                table: "rant_replies",
                columns: new[] { "rant_post_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_rant_reports_rant_post_id_user_id",
                table: "rant_reports",
                columns: new[] { "rant_post_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registration_verification_tokens_email_expires_at",
                table: "registration_verification_tokens",
                columns: new[] { "email", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_participants_task_id_user_id",
                table: "task_participants",
                columns: new[] { "task_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_category",
                table: "tasks",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_today_entries_event_type_created_at",
                table: "today_entries",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_today_entries_user_id_created_at",
                table: "today_entries",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_accounts");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "chat_room_users");

            migrationBuilder.DropTable(
                name: "chat_rooms");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "rant_posts");

            migrationBuilder.DropTable(
                name: "rant_reactions");

            migrationBuilder.DropTable(
                name: "rant_replies");

            migrationBuilder.DropTable(
                name: "rant_reports");

            migrationBuilder.DropTable(
                name: "registration_verification_tokens");

            migrationBuilder.DropTable(
                name: "task_participants");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "today_entries");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
