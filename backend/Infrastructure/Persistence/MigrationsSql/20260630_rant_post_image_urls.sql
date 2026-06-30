-- 在 Supabase SQL Editor 執行此檔案
-- 樹洞貼文多圖（Threads 風）：新增 image_data_urls text[]，並把既有單圖回填為單元素陣列。
-- EF 已宣告此欄位；此檔 idempotent，可重複執行。

ALTER TABLE rant_posts
    ADD COLUMN IF NOT EXISTS image_data_urls text[] NOT NULL DEFAULT '{}';

-- 回填：舊資料只有 image_data_url 時，補成單元素陣列，讓多圖顯示與舊單圖一致。
UPDATE rant_posts
SET image_data_urls = ARRAY[image_data_url]
WHERE image_data_url IS NOT NULL
  AND image_data_url <> ''
  AND (image_data_urls IS NULL OR cardinality(image_data_urls) = 0);
