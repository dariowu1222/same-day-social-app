-- 在 Supabase SQL Editor 執行此檔案
-- 清理：rant_posts 上有兩支覆蓋 (is_hidden, created_at) 的重複索引
--   - ix_rant_posts_hidden_created          ← 手寫於 20260522 初始 schema（小寫、未加引號），冗餘，刪除
--   - "IX_rant_posts_is_hidden_created_at"  ← EF 由 RantRecordConfigurations 宣告，保留
-- 保留 EF 那支，避免之後 EF 偵測到缺索引又重建。

DROP INDEX IF EXISTS ix_rant_posts_hidden_created;
