-- 在 Supabase SQL Editor 執行此檔案
-- 推播：裝置 token 與「上次推到哪」的水位。
--
-- 前置：20261031_chat_last_read.sql 必須先跑過（未讀訊息數依賴 last_read_at）。

-- 一個使用者可以有多台裝置，所以用 token 當唯一鍵而不是 user_id。
-- 同一台裝置換帳號登入時，token 會被改綁到新的 user_id。
CREATE TABLE IF NOT EXISTS device_tokens (
    id         varchar(64) PRIMARY KEY,
    user_id    varchar(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token      varchar(512) NOT NULL,
    platform   varchar(20) NOT NULL DEFAULT 'android',
    updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_device_tokens_token ON device_tokens (token);
CREATE INDEX IF NOT EXISTS ix_device_tokens_user ON device_tokens (user_id);

-- last_event_at 存「上次推播時，那批未讀裡最新一則的時間」。
-- 下次執行算出目前未讀的 max(created_at)，不大於它就代表沒有新東西，直接跳過。
-- 用事件時間而不是比對未讀數：使用者讀掉 3 則又來 3 則時數字不變，比數字會漏推。
CREATE TABLE IF NOT EXISTS push_digest_states (
    user_id        varchar(64) PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    last_event_at  timestamptz,
    last_pushed_at timestamptz
);

ALTER TABLE device_tokens DISABLE ROW LEVEL SECURITY;
ALTER TABLE push_digest_states DISABLE ROW LEVEL SECURITY;
