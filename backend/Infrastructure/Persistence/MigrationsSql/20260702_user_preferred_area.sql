-- 在 Supabase SQL Editor 執行此檔案
-- 修正：User/UserRecord 有 PreferredArea（偏好縣市）欄位並已對應 preferred_area，
--       但 users 表尚未建此欄，導致任何撈完整 user 列的查詢報 42703（例：封鎖名單）。

ALTER TABLE users ADD COLUMN IF NOT EXISTS preferred_area varchar(40);
