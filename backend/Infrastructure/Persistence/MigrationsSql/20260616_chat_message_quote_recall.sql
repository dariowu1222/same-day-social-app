-- 在 Supabase SQL Editor 執行此檔案
-- 聊天訊息：引用回覆欄位 + 收回旗標；content 改為 text 以容納圖片 data URL

-- content 由 varchar(2000) 放寬為 text（圖片訊息以 data URL 當內容，會超過 2000 字）
ALTER TABLE chat_messages ALTER COLUMN content TYPE text;

-- 引用回覆（denormalized：直接存來源名與原文片段，讀取免 join）
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS quoted_message_id   varchar(64);
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS quoted_sender_name  varchar(100);
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS quoted_content      varchar(2000);

-- 收回（不刪資料，標記後前端顯示「訊息已收回」、後端讀取時清空 content）
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS is_recalled boolean NOT NULL DEFAULT false;
