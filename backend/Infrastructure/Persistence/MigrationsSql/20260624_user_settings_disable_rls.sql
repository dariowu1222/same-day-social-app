-- 在 Supabase SQL Editor 執行此檔案
-- 修正：user_settings 開了 RLS 但未給 app 角色 policy，導致 INSERT/UPDATE 被擋
--       （42501: new row violates row-level security policy for table "user_settings"）
-- 決策：與其餘 app 資料表一致關閉 RLS。授權由後端負責（callerId 比對），前端不直連 DB。

ALTER TABLE user_settings DISABLE ROW LEVEL SECURITY;
