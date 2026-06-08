-- 在 Supabase SQL Editor 執行此檔案
-- 新增 rant_posts 缺少的欄位
ALTER TABLE rant_posts
ADD COLUMN IF NOT EXISTS hash_tags TEXT[] DEFAULT '{}',
ADD COLUMN IF NOT EXISTS image_data_url TEXT,
ADD COLUMN IF NOT EXISTS audio_data_url TEXT;

-- 新增 rant_replies 的 parent_reply_id（巢狀回覆）與媒體欄位
ALTER TABLE rant_replies
ADD COLUMN IF NOT EXISTS parent_reply_id VARCHAR(64) REFERENCES rant_replies(id) ON DELETE SET NULL,
ADD COLUMN IF NOT EXISTS image_data_url TEXT,
ADD COLUMN IF NOT EXISTS audio_data_url TEXT;

-- 加速子回覆查詢的索引
CREATE INDEX IF NOT EXISTS idx_rant_replies_parent ON rant_replies(parent_reply_id);
