-- 在 Supabase SQL Editor 執行此檔案
-- 站內通知中心：notifications 表
-- type：LIKE（被按讚/有共鳴）、RANT_REPLY（樹洞被回覆）
-- link_type/link_id：前端點擊跳轉用（match→配對頁、rant→樹洞詳情）

CREATE TABLE IF NOT EXISTS notifications (
    id           varchar(64) PRIMARY KEY,
    recipient_id varchar(64) NOT NULL,
    type         varchar(40) NOT NULL,
    title        varchar(120) NOT NULL DEFAULT '',
    body         varchar(300) NOT NULL DEFAULT '',
    link_type    varchar(20),
    link_id      varchar(64),
    is_read      boolean NOT NULL DEFAULT false,
    created_at   timestamptz NOT NULL DEFAULT now()
);

-- 收件匣查詢：依收件者 + 時間倒序；未讀數也走這條
CREATE INDEX IF NOT EXISTS "IX_notifications_recipient_id_created_at"
    ON notifications (recipient_id, created_at);

-- 與其餘 app 表一致：關閉 RLS（後端自授權、前端不直連 DB）
ALTER TABLE notifications DISABLE ROW LEVEL SECURITY;
