-- 在 Supabase SQL Editor 執行此檔案
-- R4：交友意圖欄位（users）+ 隱私/通知設定表（user_settings）

-- ── users：交友意圖欄位（皆可空）──
ALTER TABLE users ADD COLUMN IF NOT EXISTS dating_goal  varchar(40);
ALTER TABLE users ADD COLUMN IF NOT EXISTS looking_for  varchar(40);
ALTER TABLE users ADD COLUMN IF NOT EXISTS age_min      integer;
ALTER TABLE users ADD COLUMN IF NOT EXISTS age_max      integer;
ALTER TABLE users ADD COLUMN IF NOT EXISTS distance_km  integer;
ALTER TABLE users ADD COLUMN IF NOT EXISTS languages    text[] NOT NULL DEFAULT '{}';
ALTER TABLE users ADD COLUMN IF NOT EXISTS active_time  varchar(40);
ALTER TABLE users ADD COLUMN IF NOT EXISTS voice_first  boolean NOT NULL DEFAULT false;
ALTER TABLE users ADD COLUMN IF NOT EXISTS meet_soon    boolean NOT NULL DEFAULT false;

-- ── user_settings：隱私/通知（下一輪接 API；先建表）──
CREATE TABLE IF NOT EXISTS user_settings (
    user_id        varchar(64) PRIMARY KEY,
    profile_public boolean NOT NULL DEFAULT true,
    pause_matching boolean NOT NULL DEFAULT false,
    notify_match   boolean NOT NULL DEFAULT true,
    notify_message boolean NOT NULL DEFAULT true,
    notify_rant    boolean NOT NULL DEFAULT true,
    updated_at     timestamptz NOT NULL DEFAULT now()
);
