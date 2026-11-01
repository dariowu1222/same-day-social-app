-- 在 Supabase SQL Editor 執行此檔案
-- 滑卡表態落地：左滑（略過）要能持久化，候選池才排得掉。
--
-- 重要前提：matches 的列在「產生候選」時就會被寫入，「列存在」不等於
-- 「使用者看過／滑過」。所以表態一定要靠明確欄位判斷：
--   右滑 = user_liked  （永久排除）
--   左滑 = passed_at   （冷卻期內排除，期滿回到牌堆）
-- 既有資料 passed_at 一律 NULL，所以這支 migration 不會讓任何人的牌堆變空。

ALTER TABLE public.matches
    ADD COLUMN IF NOT EXISTS passed_at timestamptz;

-- 候選池排除「已表態對象」用的部分索引。只收真的表態過的列，
-- 所以它的大小遠小於全表索引。
CREATE INDEX IF NOT EXISTS ix_matches_user_decided
    ON public.matches (user_id, matched_user_id)
    WHERE user_liked OR passed_at IS NOT NULL;
