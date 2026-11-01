-- 在 Supabase SQL Editor 執行此檔案
-- 聊天列表的釘選排序需要「什麼時候釘的」。
--
-- 不能重用 updated_at：UpdateMemberSetting 對備註名／靜音的任何變更都會覆寫它
-- （ChatService.UpdateMemberSetting），拿它排序會讓「改個靜音」就把順序打亂。

ALTER TABLE public.chat_member_settings
    ADD COLUMN IF NOT EXISTS pinned_at timestamptz;

-- 既有已釘選但沒有時間的，用 updated_at 當近似值，至少有個穩定順序
UPDATE public.chat_member_settings
SET pinned_at = updated_at
WHERE pinned = true AND pinned_at IS NULL;
