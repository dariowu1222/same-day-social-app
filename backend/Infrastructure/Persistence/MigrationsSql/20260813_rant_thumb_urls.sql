-- 在 Supabase SQL Editor 執行
-- 樹洞圖片縮圖：列表只載縮圖大幅省頻寬（壓測前置優化），點開才載原圖。
-- 背景：Supabase 即時縮圖（/render/image/）為 Pro 方案功能，免費方案回 404 導致列表破圖，
--       故改為「上傳時就另存一張小圖」，不依賴付費的即時轉檔。
-- 舊資料沒有縮圖 → 欄位為空，前端自動回退顯示原圖，不會破圖。

-- 貼文多圖：縮圖與 image_data_urls 同索引對齊
ALTER TABLE public.rant_posts
    ADD COLUMN IF NOT EXISTS thumb_urls text[] NOT NULL DEFAULT '{}';

-- 回應單圖
ALTER TABLE public.rant_replies
    ADD COLUMN IF NOT EXISTS thumb_url text;
