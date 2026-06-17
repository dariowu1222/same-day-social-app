-- 在 Supabase SQL Editor 執行此檔案
-- users 個人資料欄位（gender 欄位本來就有，這裡只加新的；全部可空，性別必填由後端/前端把關）

ALTER TABLE users ADD COLUMN IF NOT EXISTS relationship      varchar(40);
ALTER TABLE users ADD COLUMN IF NOT EXISTS personality_tags  text[] NOT NULL DEFAULT '{}';
ALTER TABLE users ADD COLUMN IF NOT EXISTS appearance_tags   text[] NOT NULL DEFAULT '{}';
ALTER TABLE users ADD COLUMN IF NOT EXISTS height            integer;
ALTER TABLE users ADD COLUMN IF NOT EXISTS weight            integer;
ALTER TABLE users ADD COLUMN IF NOT EXISTS occupation        varchar(60);
ALTER TABLE users ADD COLUMN IF NOT EXISTS school            varchar(60);
ALTER TABLE users ADD COLUMN IF NOT EXISTS blood_type        varchar(10);
