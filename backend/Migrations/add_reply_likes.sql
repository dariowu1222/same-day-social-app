-- 在 Supabase SQL Editor 執行此檔案
-- 新增回覆按讚數欄位
ALTER TABLE rant_replies
ADD COLUMN IF NOT EXISTS like_count INTEGER DEFAULT 0;
