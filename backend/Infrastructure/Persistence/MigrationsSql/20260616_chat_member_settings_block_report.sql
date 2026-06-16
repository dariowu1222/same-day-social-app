-- 在 Supabase SQL Editor 執行此檔案
-- 聊天：房間成員個人設定（備註名/釘選/靜音）、封鎖、檢舉

CREATE TABLE IF NOT EXISTS chat_member_settings (
    id           varchar(64) PRIMARY KEY,
    chat_room_id varchar(64) NOT NULL,
    user_id      varchar(64) NOT NULL,
    note_name    varchar(100),
    pinned       boolean NOT NULL DEFAULT false,
    muted        boolean NOT NULL DEFAULT false,
    updated_at   timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_chat_member_settings_user_room
    ON chat_member_settings (user_id, chat_room_id);

CREATE TABLE IF NOT EXISTS user_blocks (
    id         varchar(64) PRIMARY KEY,
    blocker_id varchar(64) NOT NULL,
    blocked_id varchar(64) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_user_blocks_blocker_blocked
    ON user_blocks (blocker_id, blocked_id);

CREATE TABLE IF NOT EXISTS chat_reports (
    id               varchar(64) PRIMARY KEY,
    reporter_id      varchar(64) NOT NULL,
    reported_user_id varchar(64) NOT NULL,
    chat_room_id     varchar(64) NOT NULL,
    reason           varchar(500),
    created_at       timestamptz NOT NULL DEFAULT now()
);
