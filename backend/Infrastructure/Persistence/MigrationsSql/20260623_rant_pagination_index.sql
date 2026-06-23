-- 在 Supabase SQL Editor 執行此檔案
-- 樹洞列表 cursor 分頁用索引（WHERE is_hidden=false AND created_at < cursor ORDER BY created_at DESC）
-- EF 已宣告此索引；若初始 schema 未建，這條 idempotent 補上。

CREATE INDEX IF NOT EXISTS ix_rant_posts_is_hidden_created_at
    ON rant_posts (is_hidden, created_at);
